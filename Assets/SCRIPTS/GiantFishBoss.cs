using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Giant Fish Boss (Leviathan) that surfaces at a set point in the water,
/// provides a clear damage window, and periodically leaps across the pond.
/// While surfaced or airborne, it is completely vulnerable to arrows from the player's Bow.
[RequireComponent(typeof(Health), typeof(SpriteRenderer), typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class GiantFishBoss : NetworkBehaviour
{
    [Header("Sprites")]
    public Sprite frameMouthClosed;
    public Sprite frameMouthOpen;

    [Header("Stats")]
    public string bossName = "LEVIATHAN - TERROR OF THE DEEP";
    public float jumpHeight = 4.5f;
    public float jumpDuration = 2.2f;
    public float surfacedDuration = 5.5f; // Time the boss stays exposed on the water at the set point!
    public float submergedDuration = 1.2f;
    public float bossScale = 8.5f; // Colossal Leviathan scale!

    [Header("Water Arena")]
    public Vector2 waterCenter = new Vector2(40f, 2f);
    public float waterRadiusX = 6.5f;
    public float waterRadiusY = 3.2f;

    [Header("Attacks")]
    public int splashDamage = 55;
    public float splashRadius = 5.2f;
    public int waterSpitDamage = 35;
    public float waterSpitRadius = 2.2f;
    public float spitInterval = 1.4f;

    public enum State
    {
        Surfaced,   // Floating at waterCenter - primary damage window!
        Submerged,  // Diving under before leap
        Airborne,   // Leaping arc across pond
        Splashdown, // Crash landing shockwave
        Defeated
    }

    readonly NetworkVariable<int> currentState = new NetworkVariable<int>((int)State.Surfaced);
    readonly NetworkVariable<Vector2> jumpOrigin = new NetworkVariable<Vector2>();
    readonly NetworkVariable<Vector2> jumpTarget = new NetworkVariable<Vector2>();
    readonly NetworkVariable<float> jumpProgress = new NetworkVariable<float>();
    readonly NetworkVariable<bool> isMovingRight = new NetworkVariable<bool>(false);

    Health health;
    SpriteRenderer sr;
    Collider2D col;
    Rigidbody2D rb;
    DamageFlash flash;

    float stateTimer;
    float mouthAnimTimer;
    float spitTimer;
    float splashParticleTimer;
    bool mouthOpen;
    bool hasSpitThisJump;

    public static GiantFishBoss Active { get; private set; }

    void Awake()
    {
        health = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        flash = GetComponent<DamageFlash>();

        if (bossScale < 8.0f) bossScale = 8.5f;
        transform.localScale = Vector3.one * bossScale;
        if (flash != null) flash.SetBaseScale(Vector3.one * bossScale);

        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Auto-configure collider for generous hit detection
        if (col == null)
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.9f;
            circle.isTrigger = true;
            col = circle;
        }
        else if (col is CircleCollider2D circleCol)
        {
            circleCol.radius = Mathf.Max(circleCol.radius, 0.9f);
            circleCol.isTrigger = true;
        }

        // Ignore water tilemap collisions
        var tilemaps = Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapCollider2D>(FindObjectsSortMode.None);
        foreach (var tm in tilemaps)
        {
            if (tm != null && tm.gameObject.name.ToLower().Contains("water"))
            {
                Physics2D.IgnoreCollision(col, tm, true);
            }
        }

        if (frameMouthClosed == null || frameMouthOpen == null)
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s == null) continue;
                string n = s.name.ToLower();
                if (n.Contains("fish") || n.Contains("boss"))
                {
                    if (frameMouthClosed == null) frameMouthClosed = s;
                    else if (frameMouthOpen == null && s != frameMouthClosed) frameMouthOpen = s;
                }
            }
            if (frameMouthClosed == null && sr != null && sr.sprite != null)
                frameMouthClosed = sr.sprite;
            if (frameMouthOpen == null)
                frameMouthOpen = frameMouthClosed;
        }
    }

    public override void OnNetworkSpawn()
    {
        Active = this;
        if (health != null)
        {
            health.creatureId = "giant_fish";
            health.isBoss = true;
            if (IsServer) health.OnChanged += ServerOnHealthChanged;
        }

        if (bossScale < 8.0f) bossScale = 8.5f;
        transform.localScale = Vector3.one * bossScale;
        if (flash != null) flash.SetBaseScale(Vector3.one * bossScale);

        transform.position = (Vector3)waterCenter;
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        if (Active == this) Active = null;
        if (IsServer && health != null) health.OnChanged -= ServerOnHealthChanged;
    }

    void ServerOnHealthChanged(int cur, int max)
    {
        if (cur <= 0 && currentState.Value != (int)State.Defeated)
        {
            currentState.Value = (int)State.Defeated;
            TriggerDefeatRpc();
        }
    }

    void Update()
    {
        State st = (State)currentState.Value;

        if (st == State.Defeated)
        {
            sr.color = Color.Lerp(sr.color, Color.clear, Time.deltaTime * 2f);
            if (col != null) col.enabled = false;
            return;
        }

        // FISH BOSS.png naturally faces LEFT.
        // Therefore: moving right -> flipX = true (faces right). moving left -> flipX = false (faces left).
        if (sr != null)
        {
            sr.flipX = isMovingRight.Value;
        }

        if (IsServer)
        {
            ServerTick(st);
        }

        UpdateVisualsForState(st);
    }

    void UpdateVisualsForState(State st)
    {
        // Enforce giant boss scale whenever not squashing from damage flash
        if (flash == null || !flash.IsPunching)
        {
            if (transform.localScale.x < bossScale * 0.9f)
            {
                transform.localScale = Vector3.one * bossScale;
            }
        }

        if (st == State.Surfaced)
        {
            // Floating and bobbing at the set water center point
            float bobY = Mathf.Sin(Time.time * 2.8f) * 0.22f;
            transform.position = new Vector3(waterCenter.x, waterCenter.y + bobY, 0f);
            transform.rotation = Quaternion.identity;

            // Mouth animation
            AnimateMouth(0.25f);

            // Water ripples around surfaced boss
            splashParticleTimer += Time.deltaTime;
            if (splashParticleTimer >= 0.8f)
            {
                splashParticleTimer = 0f;
                WaterSplashEffect.Spawn(transform.position + new Vector3(Random.Range(-1.2f, 1.2f), -0.5f, 0f), 1.6f);
            }

            // Visible and vulnerable!
            if (col != null) col.enabled = true;
            if (sr != null) sr.enabled = true;
        }
        else if (st == State.Airborne)
        {
            float t = jumpProgress.Value;
            Vector2 horiz = Vector2.Lerp(jumpOrigin.Value, jumpTarget.Value, t);
            float arcY = 4f * jumpHeight * t * (1f - t);
            transform.position = new Vector3(horiz.x, horiz.y + arcY, 0f);

            AnimateMouth(0.15f);

            // Tilt along trajectory correctly matching head-forward
            float deltaX = jumpTarget.Value.x - jumpOrigin.Value.x;
            float tangentY = 4f * jumpHeight * (1f - 2f * t);

            float angle;
            if (deltaX >= 0f)
            {
                // Moving right (flipped sprite): nose points along (deltaX, tangentY)
                angle = Mathf.Atan2(tangentY, Mathf.Max(0.1f, deltaX)) * Mathf.Rad2Deg;
            }
            else
            {
                // Moving left (unflipped sprite): nose points along (deltaX, tangentY)
                angle = Mathf.Atan2(-tangentY, -deltaX) * Mathf.Rad2Deg;
            }

            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -40f, 40f));

            if (col != null) col.enabled = true;
            if (sr != null) sr.enabled = true;
        }
        else if (st == State.Splashdown)
        {
            transform.rotation = Quaternion.identity;
            if (col != null) col.enabled = true;
            if (sr != null) sr.enabled = true;
        }
        else // Submerged
        {
            transform.rotation = Quaternion.identity;
            if (col != null) col.enabled = false;
            if (sr != null) sr.enabled = false;
        }
    }

    void AnimateMouth(float interval)
    {
        mouthAnimTimer += Time.deltaTime;
        if (mouthAnimTimer >= interval)
        {
            mouthAnimTimer = 0f;
            mouthOpen = !mouthOpen;
            if (sr != null && frameMouthClosed != null && frameMouthOpen != null)
            {
                sr.sprite = mouthOpen ? frameMouthOpen : frameMouthClosed;
            }
        }
    }

    void ServerTick(State st)
    {
        stateTimer += Time.deltaTime;

        if (st == State.Surfaced)
        {
            // Face towards the player while surfaced
            var player = FindClosestPlayer();
            if (player != null)
            {
                isMovingRight.Value = (player.transform.position.x > waterCenter.x);
            }

            // Periodically spit water toward shore
            spitTimer += Time.deltaTime;
            if (spitTimer >= spitInterval)
            {
                spitTimer = 0f;
                ServerSpitWater();
            }

            // Stay on the water for surfacedDuration, then dive
            if (stateTimer >= surfacedDuration)
            {
                ServerStartDive();
            }
        }
        else if (st == State.Submerged)
        {
            if (stateTimer >= submergedDuration)
            {
                ServerStartJump();
            }
        }
        else if (st == State.Airborne)
        {
            float t = stateTimer / jumpDuration;
            jumpProgress.Value = Mathf.Clamp01(t);

            if (t >= 0.5f && !hasSpitThisJump)
            {
                hasSpitThisJump = true;
                ServerSpitWater();
            }

            if (t >= 1f)
            {
                ServerSplashdown();
            }
        }
        else if (st == State.Splashdown)
        {
            if (stateTimer >= 0.6f)
            {
                ServerReturnToSurface();
            }
        }
    }

    void ServerStartDive()
    {
        stateTimer = 0f;
        currentState.Value = (int)State.Submerged;
        PlayBreachSplashRpc(waterCenter);
    }

    void ServerReturnToSurface()
    {
        stateTimer = 0f;
        spitTimer = 0f;
        transform.position = (Vector3)waterCenter;
        currentState.Value = (int)State.Surfaced;
        PlayBreachSplashRpc(waterCenter);
    }

    void ServerStartJump()
    {
        stateTimer = 0f;
        hasSpitThisJump = false;

        // Leap from one side of the pond to the other
        float angleA = Random.Range(130f, 230f) * Mathf.Deg2Rad; // West side
        float angleB = Random.Range(-40f, 60f) * Mathf.Deg2Rad;   // East side

        // Randomize direction
        bool leapEast = (Random.value > 0.5f);
        Vector2 start = waterCenter + new Vector2(Mathf.Cos(leapEast ? angleA : angleB) * waterRadiusX, Mathf.Sin(leapEast ? angleA : angleB) * waterRadiusY);
        Vector2 end = waterCenter + new Vector2(Mathf.Cos(leapEast ? angleB : angleA) * waterRadiusX, Mathf.Sin(leapEast ? angleB : angleA) * waterRadiusY);

        jumpOrigin.Value = start;
        jumpTarget.Value = end;
        jumpProgress.Value = 0f;
        isMovingRight.Value = (end.x > start.x);

        currentState.Value = (int)State.Airborne;

        PlayBreachSplashRpc(start);
    }

    void ServerSplashdown()
    {
        stateTimer = 0f;
        currentState.Value = (int)State.Splashdown;

        Vector2 splashPos = jumpTarget.Value;
        PlaySplashdownRpc(splashPos);

        // AoE splash damage to players standing on the immediate bank
        Collider2D[] hits = Physics2D.OverlapCircleAll(splashPos, splashRadius);
        foreach (Collider2D c in hits)
        {
            Health hp = c.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead && hp != health)
            {
                bool isPlayer = hp.GetComponent<AbilityLoadout>() != null;
                if (isPlayer)
                {
                    hp.ServerTakeDamage(splashDamage, 0);
                    var rec = hp.GetComponent<KnockbackReceiver>();
                    if (rec != null)
                    {
                        Vector2 away = ((Vector2)hp.transform.position - splashPos).normalized;
                        rec.ServerApply(away, 5f);
                    }
                }
            }
        }
    }

    void ServerSpitWater()
    {
        Vector2 myPos = transform.position;
        var player = FindClosestPlayer();
        Vector2 aimTarget = player != null ? (Vector2)player.transform.position : waterCenter + new Vector2(isMovingRight.Value ? 8f : -8f, Random.Range(-2f, 4f));

        Vector2 mainDir = (aimTarget - myPos).normalized;
        float[] angles = new float[] { -22f, 0f, 22f };

        foreach (float angle in angles)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * mainDir;
            Vector2 targetLandPos = myPos + dir * Random.Range(6f, 10f);

            StartCoroutine(WaterBlastRoutine(targetLandPos));
        }
    }

    IEnumerator WaterBlastRoutine(Vector2 landPos)
    {
        yield return new WaitForSeconds(0.25f);

        PlaySpitSFXRpc(landPos);

        if (!IsServer) yield break;

        Collider2D[] hits = Physics2D.OverlapCircleAll(landPos, waterSpitRadius);
        foreach (Collider2D c in hits)
        {
            Health hp = c.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead && hp != health)
            {
                bool isPlayer = hp.GetComponent<AbilityLoadout>() != null;
                if (isPlayer)
                {
                    hp.ServerTakeDamage(waterSpitDamage, 0);
                    var rec = hp.GetComponent<KnockbackReceiver>();
                    if (rec != null)
                    {
                        Vector2 away = ((Vector2)hp.transform.position - landPos).normalized;
                        rec.ServerApply(away, 6f);
                    }
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayBreachSplashRpc(Vector2 pos)
    {
        WaterSplashEffect.Spawn(pos, 3.5f);
        CameraFollow.Shake(0.22f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlaySplashdownRpc(Vector2 pos)
    {
        WaterSplashEffect.Spawn(pos, 4.2f);
        CameraFollow.Shake(0.35f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlaySpitSFXRpc(Vector2 pos)
    {
        WaterSplashEffect.Spawn(pos, waterSpitRadius);
        CameraFollow.Shake(0.12f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void TriggerDefeatRpc()
    {
        WaterSplashEffect.Spawn(transform.position, 5.5f);
        CameraFollow.Shake(0.6f);

        if (BlacksmithVillageEvent.Instance != null)
        {
            BlacksmithVillageEvent.Instance.OnFishDefeated();
        }
    }

    void UpdateVisuals()
    {
        if (sr != null && frameMouthClosed != null)
            sr.sprite = frameMouthClosed;
    }

    Health FindClosestPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return null;

        Health closest = null;
        float bestDistSqr = 400f;
        Vector2 myPos = transform.position;

        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Health hp = client.PlayerObject.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float d2 = ((Vector2)hp.transform.position - myPos).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                closest = hp;
            }
        }
        return closest;
    }

    void OnGUI()
    {
        if (health == null || health.IsDead || currentState.Value == (int)State.Defeated) return;

        // Boss Health Bar UI at top center
        float barWidth = Mathf.Min(Screen.width * 0.6f, 500f);
        float barHeight = 26f;
        float x = (Screen.width - barWidth) * 0.5f;
        float y = 20f;

        float hpRatio = (float)health.Current / Mathf.Max(1, health.Max);

        // Background
        GUI.Box(new Rect(x - 4, y - 4, barWidth + 8, barHeight + 28), "");

        // Title
        GUI.color = new Color(1f, 0.85f, 0.2f);
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };
        GUI.Label(new Rect(x, y - 2, barWidth, 18), bossName, titleStyle);

        // Bar outline & fill
        Rect barRect = new Rect(x, y + 18, barWidth, barHeight);
        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        // Health fill
        GUI.color = Color.Lerp(Color.red, new Color(0.2f, 0.7f, 1f), hpRatio);
        GUI.DrawTexture(new Rect(x + 2, y + 20, (barWidth - 4) * hpRatio, barHeight - 4), Texture2D.whiteTexture);

        // Status text
        GUI.color = Color.white;
        var hpStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        State st = (State)currentState.Value;
        string hint;
        if (st == State.Surfaced) hint = "SURFACED - SHOOT WITH BOW NOW!";
        else if (st == State.Airborne) hint = "LEAPING - AIRBORNE TARGET!";
        else hint = "DIVING...";

        GUI.Label(barRect, string.Format("{0} / {1} HP   ({2})", health.Current, health.Max, hint), hpStyle);
        GUI.color = Color.white;
    }
}
