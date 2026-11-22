using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Server-authoritative final boss. Fireball volleys, dodgeable sky barrages,
/// a short hostile Time Stop, and continuous meteor pressure below half health.
[RequireComponent(typeof(NetworkObject), typeof(Health), typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class HSirkBoss : NetworkBehaviour
{
    public static HSirkBoss Active { get; private set; }
    public static bool IsTimeStopped => Active != null && Active.IsSpawned && Active.bossTimeStop.Value;

    public float moveSpeed = 3.4f;
    public float aggroRange = 26f;
    public float attackCooldown = 1.15f;
    public float bossScale = 2.8f;
    public HSirkFireball fireballPrefab;

    readonly NetworkVariable<bool> bossTimeStop = new NetworkVariable<bool>();
    readonly NetworkVariable<bool> facingLeft = new NetworkVariable<bool>();
    Health health;
    Rigidbody2D body;
    SpriteRenderer sprite;
    Sprite[] idleFrames;
    bool attacking;
    bool phaseTwoAnnounced;
    bool ultimateNext;
    float nextAttack;
    float nextTimeStop;
    float nextRain;
    float animationClock;

    void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        sprite.sortingOrder = Mathf.Max(sprite.sortingOrder, 30);
        idleFrames = Resources.LoadAll<Sprite>("HSirk/H Sirk Idle");
        System.Array.Sort(idleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (idleFrames.Length > 0) sprite.sprite = idleFrames[0];
    }

    public override void OnNetworkSpawn()
    {
        Active = this;
        if (!IsServer) body.bodyType = RigidbodyType2D.Kinematic;
        health.isBoss = true;
        health.creatureId = "hsirk_boss";
        transform.localScale = Vector3.one * bossScale;
        DamageFlash flash = GetComponent<DamageFlash>();
        if (flash != null) flash.SetBaseScale(transform.localScale);
        bossTimeStop.OnValueChanged += OnTimeStopChanged;
        if (IsServer)
        {
            health.OnDied += OnDied;
            nextTimeStop = Time.time + 11f;
            nextAttack = Time.time + 1.4f;
        }
        foreach (HSirkEncounter encounter in FindObjectsByType<HSirkEncounter>(FindObjectsSortMode.None))
            encounter.HideForBossFight();
        TimeStopVisuals.OnStateChanged(TimeStopWorld.IsStopped);
        if (IsServer) EntranceRpc();
    }

    public override void OnNetworkDespawn()
    {
        bossTimeStop.OnValueChanged -= OnTimeStopChanged;
        if (IsServer) health.OnDied -= OnDied;
        if (Active == this) Active = null;
        TimeStopVisuals.OnStateChanged(TimeStopWorld.IsStopped);
    }

    void OnTimeStopChanged(bool oldValue, bool newValue) =>
        TimeStopVisuals.OnStateChanged(TimeStopWorld.IsStopped);

    void Update()
    {
        if (health == null || health.IsDead) return;
        sprite.flipX = facingLeft.Value;
        if (!TimeStopWorld.IsStopped || IsTimeStopped)
        {
            animationClock += Time.deltaTime;
            if (idleFrames.Length > 0)
                sprite.sprite = idleFrames[Mathf.FloorToInt(animationClock * 6f) % idleFrames.Length];
        }
        if (!IsServer || TimeStopWorld.IsStopped) return;

        if (!phaseTwoAnnounced && health.Fraction <= 0.5f)
        {
            phaseTwoAnnounced = true;
            nextRain = Time.time + 0.25f;
            PhaseTwoRpc();
        }
        if (phaseTwoAnnounced && Time.time >= nextRain)
        {
            nextRain = Time.time + 0.8f;
            Health target = ClosestPlayer();
            if (target != null)
                StartCoroutine(MeteorAt((Vector2)target.transform.position + Random.insideUnitCircle * 2.1f, 0.72f));
        }
    }

    void FixedUpdate()
    {
        if (!IsServer || health.IsDead) return;
        if (TimeStopWorld.IsStopped || attacking)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Health target = ClosestPlayer();
        if (target == null) { body.linearVelocity = Vector2.zero; return; }
        Vector2 toward = (Vector2)target.transform.position - (Vector2)transform.position;
        if (toward.sqrMagnitude > 0.01f) facingLeft.Value = toward.x < 0f;

        if (Time.time >= nextTimeStop)
        {
            StartCoroutine(TimeStopAttack(target));
            return;
        }
        if (Time.time >= nextAttack)
        {
            if (ultimateNext) StartCoroutine(SkyBarrage());
            else StartCoroutine(HomingVolley(target, phaseTwoAnnounced ? 5 : 3));
            ultimateNext = !ultimateNext;
            return;
        }
        float distance = toward.magnitude;
        body.linearVelocity = distance > 5.5f ? toward.normalized * moveSpeed :
                              distance < 3f ? -toward.normalized * moveSpeed * 0.7f : Vector2.zero;
    }

    IEnumerator HomingVolley(Health target, int count)
    {
        attacking = true;
        body.linearVelocity = Vector2.zero;
        CastEffectRpc(transform.position, false);
        yield return WaitForWorld(0.55f);
        for (int i = 0; i < count && !health.IsDead; i++)
        {
            if (target == null || target.IsDead) target = ClosestPlayer();
            if (target == null) break;
            Vector2 toward = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            float angle = (i - (count - 1) * 0.5f) * 19f;
            Vector2 heading = Quaternion.Euler(0f, 0f, angle) * toward;
            HSirkFireball shot = MakeFireball((Vector2)transform.position + heading * 1.1f);
            if (shot != null) shot.ServerLaunchHoming(target.GetComponent<NetworkObject>(), heading);
            yield return WaitForWorld(0.16f);
        }
        EndAttack();
    }

    IEnumerator SkyBarrage()
    {
        attacking = true;
        body.linearVelocity = Vector2.zero;
        CastEffectRpc(transform.position, true);
        for (int wave = 0; wave < (phaseTwoAnnounced ? 5 : 4) && !health.IsDead; wave++)
        {
            Health target = ClosestPlayer();
            if (target == null) break;
            Vector2 center = target.transform.position;
            int count = phaseTwoAnnounced ? 9 : 7;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + wave * 0.37f) * Mathf.PI * 2f / count;
                float radius = i == 0 ? 0f : 2.1f + (i % 3) * 1.45f;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                StartCoroutine(MeteorAt(point, 0.78f));
            }
            yield return WaitForWorld(0.55f);
        }
        yield return WaitForWorld(0.4f);
        EndAttack();
    }

    IEnumerator MeteorAt(Vector2 point, float warning)
    {
        MeteorWarningRpc(point, warning);
        yield return WaitForWorld(warning);
        if (health.IsDead) yield break;
        HSirkFireball meteor = MakeFireball(point + Vector2.up * 8f);
        if (meteor != null) meteor.ServerLaunchMeteor(point);
    }

    IEnumerator TimeStopAttack(Health target)
    {
        attacking = true;
        body.linearVelocity = Vector2.zero;
        TimeStopWarningRpc();
        yield return WaitForWorld(0.95f);
        if (health.IsDead) yield break;
        bossTimeStop.Value = true;
        // The shots hang in place with the players, then rush forward together.
        for (int i = 0; i < (phaseTwoAnnounced ? 7 : 5); i++)
        {
            if (target == null || target.IsDead) target = ClosestPlayer();
            if (target == null) break;
            Vector2 toward = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            Vector2 heading = Quaternion.Euler(0f, 0f, (i - 2) * 16f) * toward;
            HSirkFireball shot = MakeFireball((Vector2)transform.position + heading * (1.1f + i * 0.18f));
            if (shot != null) shot.ServerLaunchHoming(target.GetComponent<NetworkObject>(), heading);
        }
        float remaining = 1.25f;
        while (remaining > 0f && !health.IsDead)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        bossTimeStop.Value = false;
        nextTimeStop = Time.time + (phaseTwoAnnounced ? 10f : 13f);
        EndAttack();
    }

    static IEnumerator WaitForWorld(float seconds)
    {
        while (seconds > 0f || TimeStopWorld.IsStopped)
        {
            if (!TimeStopWorld.IsStopped) seconds -= Time.deltaTime;
            yield return null;
        }
    }

    void EndAttack()
    {
        attacking = false;
        nextAttack = Time.time + (phaseTwoAnnounced ? attackCooldown * 0.72f : attackCooldown);
    }

    HSirkFireball MakeFireball(Vector2 position)
    {
        if (fireballPrefab == null) fireballPrefab = Resources.Load<HSirkFireball>("PREFABS/HSirkFireball");
        if (fireballPrefab == null) return null;
        HSirkFireball shot = Instantiate(fireballPrefab, position, Quaternion.identity);
        shot.NetworkObject.Spawn(true);
        return shot;
    }

    Health ClosestPlayer()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null) return null;
        Health nearest = null;
        float best = aggroRange * aggroRange;
        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Health candidate = client.PlayerObject.GetComponent<Health>();
            if (candidate == null || candidate.IsDead) continue;
            float distance = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distance < best) { best = distance; nearest = candidate; }
        }
        return nearest;
    }

    void OnDied()
    {
        StopAllCoroutines();
        bossTimeStop.Value = false;
        body.linearVelocity = Vector2.zero;
        foreach (HSirkFireball shot in FindObjectsByType<HSirkFireball>(FindObjectsSortMode.None))
            if (shot != null && shot.IsSpawned) shot.NetworkObject.Despawn();
        DefeatRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void EntranceRpc()
    {
        BannerOverlay.Show("H-SIRK, CONQUEROR OF VILLAGES");
        var circle = SummoningCircleEffect.Spawn(transform.position, 3.8f, 1.7f, 27);
        circle.primaryColor = new Color(1f, 0.18f, 0.07f);
        circle.secondaryColor = new Color(1f, 0.7f, 0.12f);
        CameraFollow.Shake(0.65f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void CastEffectRpc(Vector2 position, bool ultimate)
    {
        var circle = SummoningCircleEffect.Spawn(position, ultimate ? 4f : 2.5f, 0.8f, 28);
        circle.primaryColor = new Color(1f, 0.2f, 0.06f);
        circle.secondaryColor = new Color(1f, 0.8f, 0.2f);
        if (ultimate) BannerOverlay.Show("FIRESTORM - FIND THE GAPS!");
    }

    [Rpc(SendTo.ClientsAndHost)]
    void MeteorWarningRpc(Vector2 point, float duration)
    {
        var circle = SummoningCircleEffect.Spawn(point, 1.55f, duration + 0.15f, 26);
        circle.primaryColor = new Color(1f, 0.12f, 0.05f);
        circle.secondaryColor = new Color(1f, 0.72f, 0.08f);
        circle.stopWithWorld = true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void TimeStopWarningRpc()
    {
        BannerOverlay.Show("H-SIRK IS BENDING TIME!");
        ImpactFrameEffect.TriggerCustom(new Color(1f, 0.2f, 0.08f), 0.25f, 0.5f, false);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PhaseTwoRpc()
    {
        BannerOverlay.Show("THE SKY IS BURNING - PHASE TWO!");
        ImpactFrameEffect.TriggerCustom(new Color(1f, 0.18f, 0.02f), 0.4f, 0.75f, false);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void DefeatRpc(Vector2 point)
    {
        SlamImpactEffect.Spawn(point, 4.5f, 42, new Color(1f, 0.18f, 0.05f), Color.white);
        CameraFollow.Shake(0.75f);
        HSirkEndingOverlay.Show();
    }

    void OnGUI()
    {
        if (health == null || !IsSpawned || health.IsDead) return;
        float width = Mathf.Min(Screen.width * 0.65f, 580f);
        float x = (Screen.width - width) * 0.5f;
        float y = 55f;
        float fraction = health.Fraction;
        GUI.depth = 100;
        GUI.color = new Color(0.09f, 0.015f, 0.015f, 0.92f);
        GUI.DrawTexture(new Rect(x - 7f, y - 6f, width + 14f, 50f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.75f, 0.24f);
        var title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 15
        };
        GUI.Label(new Rect(x, y - 2f, width, 22f),
                  phaseTwoAnnounced || fraction <= 0.5f ? "H-SIRK  -  THE BURNING SKY" : "H-SIRK  -  CONQUEROR OF VILLAGES", title);
        GUI.color = new Color(0.25f, 0.09f, 0.06f);
        GUI.DrawTexture(new Rect(x, y + 23f, width, 15f), Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(1f, 0.08f, 0.04f), new Color(1f, 0.58f, 0.09f), fraction);
        GUI.DrawTexture(new Rect(x + 2f, y + 25f, (width - 4f) * fraction, 11f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
