using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Post-forge boss: a huge chipmunk that idles on chipmunk_0, swaps to
/// chipmunk_1 as a clear slash warning, then hides the hit behind heavy VFX.
[RequireComponent(typeof(Health), typeof(SpriteRenderer), typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ChipmunkBoss : NetworkBehaviour
{
    public enum State
    {
        Idle,
        Warning,
        Recovering,
        Defeated
    }

    [Header("Sprites")]
    public Sprite idleSprite;
    public Sprite warningSprite;

    [Header("Stats")]
    public string bossName = "CHIPMUNK, THE FORGE-EATER";
    public float bossScale = 4.2f;
    public int slashDamage = 95;

    [Header("Movement")]
    public float aggroRange = 35f;
    public float moveSpeed = 3.8f;

    [Header("Slash")]
    public float attackRange = 2.4f;
    public float slashReach = 1.35f;
    public float slashRadius = 2.2f;
    public float windupSeconds = 0.8f;
    public float recoverSeconds = 0.55f;
    public float attackCooldown = 1.7f;
    public float knockback = 9f;

    readonly NetworkVariable<int> currentState = new NetworkVariable<int>((int)State.Idle);
    readonly NetworkVariable<bool> faceLeft = new NetworkVariable<bool>();

    Rigidbody2D rb;
    Health health;
    SpriteRenderer sr;
    DamageFlash flash;
    KnockbackReceiver knockbackReceiver;
    Health target;

    float nextAttackTime;
    float hitstunUntil;
    int lastKnownHealth = -1;
    bool slashRoutineRunning;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
        flash = GetComponent<DamageFlash>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 8f;

        if (idleSprite == null || warningSprite == null) ResolveSprites();
        if (sr != null)
        {
            sr.sprite = idleSprite != null ? idleSprite : sr.sprite;
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 28);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer && rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        if (health != null)
        {
            health.creatureId = "chipmunk_boss";
            health.isBoss = true;

            if (IsServer)
            {
                lastKnownHealth = health.Current;
                health.OnChanged += ServerOnHealthChanged;
            }
        }

        transform.localScale = Vector3.one * bossScale;
        if (flash != null) flash.SetBaseScale(Vector3.one * bossScale);
        ApplyVisuals();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && health != null) health.OnChanged -= ServerOnHealthChanged;
    }

    void Update()
    {
        ApplyVisuals();
    }

    void FixedUpdate()
    {
        if (!IsServer || health == null || health.IsDead) return;
        if (TimeStopWorld.IsStopped)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        if (slashRoutineRunning || Time.time < hitstunUntil)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        if (knockbackReceiver != null && knockbackReceiver.IsRecovering) return;

        target = FindClosestPlayer();
        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
        float distance = toTarget.magnitude;

        if (distance > aggroRange)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
            faceLeft.Value = toTarget.x < 0f;

        if (distance > attackRange)
        {
            rb.linearVelocity = toTarget.normalized * moveSpeed;
            return;
        }

        rb.linearVelocity = Vector2.zero;
        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(ServerSlashRoutine(toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right));
        }
    }

    IEnumerator ServerSlashRoutine(Vector2 direction)
    {
        slashRoutineRunning = true;
        currentState.Value = (int)State.Warning;
        rb.linearVelocity = Vector2.zero;

        Vector2 slashCenter = (Vector2)transform.position + direction * slashReach;
        PlayWarningRpc(slashCenter, slashRadius);

        yield return WaitForUnfrozenSeconds(windupSeconds);

        if (health == null || health.IsDead)
        {
            slashRoutineRunning = false;
            yield break;
        }

        ServerDealSlashDamage(slashCenter, direction);
        PlaySlashRpc(transform.position, slashCenter, direction, slashRadius);

        currentState.Value = (int)State.Recovering;
        yield return WaitForUnfrozenSeconds(recoverSeconds);

        if (currentState.Value != (int)State.Defeated)
            currentState.Value = (int)State.Idle;

        slashRoutineRunning = false;
    }

    static IEnumerator WaitForUnfrozenSeconds(float duration)
    {
        while (duration > 0f || TimeStopWorld.IsStopped)
        {
            if (!TimeStopWorld.IsStopped) duration -= Time.deltaTime;
            yield return null;
        }
    }

    void ServerDealSlashDamage(Vector2 center, Vector2 direction)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, slashRadius);
        foreach (Collider2D hit in hits)
        {
            Health victim = hit.GetComponentInParent<Health>();
            if (victim == null || victim == health || victim.IsDead) continue;

            bool isPlayer = victim.GetComponent<AbilityLoadout>() != null;
            if (!isPlayer) continue;

            victim.ServerTakeDamage(slashDamage, PowerMeter.NoAttacker, direction);

            var receiver = victim.GetComponent<KnockbackReceiver>();
            if (receiver != null)
            {
                Vector2 away = ((Vector2)victim.transform.position - center).normalized;
                if (away.sqrMagnitude < 0.0001f) away = direction;
                receiver.ServerApply(away, knockback);
            }
        }
    }

    void ServerOnHealthChanged(int current, int max)
    {
        if (current < lastKnownHealth)
            hitstunUntil = Time.time + 0.15f;

        lastKnownHealth = current;

        if (current <= 0 && currentState.Value != (int)State.Defeated)
        {
            currentState.Value = (int)State.Defeated;
            PlayDefeatRpc(transform.position);
        }
    }

    public void PlayEmergence(Vector3 position)
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            PlayEmergenceRpc(position);
        else
            PlayEmergenceEffects(position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayEmergenceRpc(Vector3 position)
    {
        PlayEmergenceEffects(position);
    }

    void PlayEmergenceEffects(Vector3 position)
    {
        SummoningCircleEffect.Spawn(position, 3.2f, 1.4f, 20);
        SlamImpactEffect.Spawn(position, 2.6f, 35, new Color(0.95f, 0.55f, 0.18f), new Color(1f, 0.92f, 0.55f));
        ImpactFrameEffect.TriggerCustom(new Color(0.95f, 0.45f, 0.12f), 0.28f, 1.0f, false);
        CameraFollow.Shake(0.55f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayWarningRpc(Vector2 center, float radius)
    {
        SummoningCircleEffect.Spawn(center, radius, windupSeconds, 25);
        CameraFollow.Shake(0.12f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlaySlashRpc(Vector3 origin, Vector2 center, Vector2 direction, float radius)
    {
        SlashArcEffect.Spawn(origin + (Vector3)(direction * 0.65f), direction, radius, 45, 2);
        SlashArcEffect.SpawnHitImpact(center, direction, 50);
        SlamImpactEffect.Spawn(center, radius * 0.85f, 42, new Color(1f, 0.25f, 0.12f), Color.white);
        ImpactFrameEffect.TriggerCustom(new Color(1f, 0.18f, 0.1f), 0.18f, 1.25f, false);
        CameraFollow.Shake(0.48f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayDefeatRpc(Vector3 position)
    {
        SlamImpactEffect.Spawn(position, 3.5f, 40, new Color(0.6f, 0.35f, 0.15f), Color.white);
        CameraFollow.Shake(0.65f);
        BannerOverlay.Show("The Chipmunk Boss is defeated!");
        BlacksmithForgeNPC.NotifyChipmunkBossDefeated();
    }

    void ApplyVisuals()
    {
        if (sr == null) return;

        State state = (State)currentState.Value;
        sr.flipX = faceLeft.Value;
        sr.sprite = state == State.Warning && warningSprite != null
            ? warningSprite
            : (idleSprite != null ? idleSprite : sr.sprite);

        if (flash == null || !flash.IsPunching)
            transform.localScale = Vector3.one * bossScale;
    }

    void ResolveSprites()
    {
        Sprite fallback = null;
        foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite == null) continue;
            string n = sprite.name.ToLower();
            if (!n.Contains("chipmunk")) continue;

            if (fallback == null) fallback = sprite;
            if (n.EndsWith("_0")) idleSprite = sprite;
            if (n.EndsWith("_1")) warningSprite = sprite;
        }

        if (idleSprite == null) idleSprite = fallback;
        if (warningSprite == null) warningSprite = idleSprite;
    }

    Health FindClosestPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return null;

        Health best = null;
        float bestSqr = aggroRange * aggroRange;
        Vector2 here = transform.position;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Health hp = client.PlayerObject.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float sqr = ((Vector2)client.PlayerObject.transform.position - here).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = hp;
            }
        }

        return best;
    }

    void OnGUI()
    {
        if (health == null || health.IsDead || currentState.Value == (int)State.Defeated) return;

        float width = Mathf.Min(Screen.width * 0.62f, 540f);
        float height = 24f;
        float x = (Screen.width - width) * 0.5f;
        float y = 58f;
        float hpRatio = health.Max <= 0 ? 0f : Mathf.Clamp01(health.Current / (float)health.Max);

        GUI.Box(new Rect(x - 4f, y - 4f, width + 8f, height + 28f), "");

        var title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };

        GUI.color = new Color(1f, 0.78f, 0.2f);
        GUI.Label(new Rect(x, y - 2f, width, 18f), bossName, title);

        Rect bar = new Rect(x, y + 18f, width, height);
        GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.9f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        GUI.color = Color.Lerp(new Color(0.8f, 0.05f, 0.04f), new Color(0.95f, 0.55f, 0.12f), hpRatio);
        GUI.DrawTexture(new Rect(x + 2f, y + 20f, (width - 4f) * hpRatio, height - 4f), Texture2D.whiteTexture);

        GUI.color = Color.white;
        string hint = currentState.Value == (int)State.Warning ? "SLASH INCOMING" : "Watch for chipmunk_1";
        var hpStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(bar, string.Format("{0} / {1} HP   ({2})", health.Current, health.Max, hint), hpStyle);
        GUI.color = Color.white;
    }
}
