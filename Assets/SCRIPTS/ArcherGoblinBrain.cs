using Unity.Netcode;
using UnityEngine;

/// Archer Goblin: a ranged sniper that keeps its distance and pelts players with
/// arrows. It strafes laterally between shots to make itself harder to pin down,
/// and retreats fast when a player gets too close.
///
/// Uses the same Arrow prefab as the player but at lower damage and no friendly fire.
/// Because ArrowProjectile handles all hit logic, the goblin doesn't deal damage directly.
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class ArcherGoblinBrain : NetworkBehaviour
{
    [Header("Senses")]
    public float aggroRange     = 24f;
    public float idealRange     = 16f;   // sweet-spot distance
    public float tooCloseRange  = 8f;    // retreat threshold
    public float giveUpRange    = 30f;

    [Header("Movement")]
    public float moveSpeed      = 3.0f;
    public float retreatSpeed   = 5.0f;
    public float strafeSpeed    = 2.2f;

    [Header("Arrow Attack")]
    public ArrowProjectile arrowPrefab;
    public float shootCooldown  = 1.4f;
    public int   arrowDamage    = 28;
    public float arrowSpeed     = 26f;
    public float arrowLifetime  = 2.8f;
    [Tooltip("Lead prediction factor (0 = shoot at feet, 1 = full prediction).")]
    public float aimLeadFactor  = 0.65f;

    [Header("Hit Reaction")]
    public float hitstunSeconds = 0.2f;

    // ── network ───────────────────────────────────────────────────────────
    readonly NetworkVariable<bool> faceLeft = new NetworkVariable<bool>();

    // ── runtime ───────────────────────────────────────────────────────────
    Rigidbody2D       rb;
    Health            health;
    SpriteRenderer    sprite;
    Animator          anim;
    KnockbackReceiver knockbackReceiver;
    Health            target;

    float nextShootTime;
    float hitstunUntil;
    int   lastKnownHealth = -1;
    int   strafeSign      = 1;
    float nextStrafeFlip;

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        rb                = GetComponent<Rigidbody2D>();
        health            = GetComponent<Health>();
        sprite            = GetComponent<SpriteRenderer>();
        anim              = GetComponent<Animator>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        // 20% chance to drop iron ore when killed — tracked by QuestManager.
        if (GetComponent<IronDrop>() == null)
            gameObject.AddComponent<IronDrop>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer && rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        if (IsServer && health != null)
        {
            lastKnownHealth = health.Current;
            health.OnChanged += ServerNoteHealthChange;
            strafeSign = (Random.value > 0.5f) ? 1 : -1;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && health != null) health.OnChanged -= ServerNoteHealthChange;
    }

    void ServerNoteHealthChange(int current, int max)
    {
        if (current < lastKnownHealth)
            hitstunUntil = Time.time + hitstunSeconds;
        lastKnownHealth = current;
    }

    // ── cosmetic (all clients) ────────────────────────────────────────────

    void Update()
    {
        if (sprite != null && NetworkObject != null && NetworkObject.IsSpawned)
            sprite.flipX = faceLeft.Value;
    }

    // ── AI (server only) ──────────────────────────────────────────────────

    void FixedUpdate()
    {
        if ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            || health.IsDead) return;

        // Hitstun / knockback recovery
        if (Time.time < hitstunUntil || (knockbackReceiver != null && knockbackReceiver.IsRecovering))
        {
            rb.linearVelocity = Vector2.zero;
            SetSpeed(0f);
            return;
        }

        target = FindClosestPlayer();
        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            SetSpeed(0f);
            return;
        }

        Vector2 myPos    = transform.position;
        Vector2 tarPos   = target.transform.position;
        Vector2 toTarget = tarPos - myPos;
        float   dist     = toTarget.magnitude;

        // Face the target
        bool wantsLeft = toTarget.x < 0f;
        if (NetworkObject != null && NetworkObject.IsSpawned)
            faceLeft.Value = wantsLeft;
        else if (sprite != null)
            sprite.flipX   = wantsLeft;

        // ── Movement logic ────────────────────────────────────────────────
        if (dist > aggroRange)
        {
            rb.linearVelocity = Vector2.zero;
            SetSpeed(0f);
        }
        else if (dist < tooCloseRange)
        {
            // Retreat away from the player
            rb.linearVelocity = -toTarget.normalized * retreatSpeed;
            SetSpeed(retreatSpeed);
        }
        else if (dist > idealRange)
        {
            // Close in to ideal range
            rb.linearVelocity = toTarget.normalized * moveSpeed;
            SetSpeed(moveSpeed);
        }
        else
        {
            // In sweet spot – strafe laterally to dodge
            if (Time.time >= nextStrafeFlip)
            {
                strafeSign     = -strafeSign;
                nextStrafeFlip = Time.time + Random.Range(1.2f, 2.4f);
            }
            Vector2 perp      = new Vector2(-toTarget.normalized.y, toTarget.normalized.x) * strafeSign;
            rb.linearVelocity = perp * strafeSpeed;
            SetSpeed(strafeSpeed);
        }

        // ── Shoot ─────────────────────────────────────────────────────────
        if (dist <= aggroRange && Time.time >= nextShootTime)
        {
            nextShootTime = Time.time + shootCooldown;
            ServerShootArrow(myPos, tarPos, target);
        }
    }

    // ─────────────────────────────────────────────────────────────────────

    void ServerShootArrow(Vector2 from, Vector2 targetPos, Health targetHealth)
    {
        // Auto-discover the Arrow prefab from the network prefab list if not assigned
        if (arrowPrefab == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
            {
                foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
                {
                    if (p.Prefab != null && p.Prefab.GetComponent<ArrowProjectile>() != null)
                    {
                        arrowPrefab = p.Prefab.GetComponent<ArrowProjectile>();
                        break;
                    }
                }
            }
        }
        if (arrowPrefab == null) return;

        // Lead prediction so the arrow doesn't always miss moving targets
        Vector2 aimAt = targetPos;
        if (targetHealth != null)
        {
            var tarRb = targetHealth.GetComponent<Rigidbody2D>();
            if (tarRb != null)
            {
                float travelTime = Vector2.Distance(from, targetPos) / Mathf.Max(1f, arrowSpeed);
                aimAt = targetPos + tarRb.linearVelocity * travelTime * aimLeadFactor;
            }
        }

        // Tiny spread so it's not a perfect hitscan
        aimAt += Random.insideUnitCircle * 0.3f;

        Vector2 direction = (aimAt - from).normalized;
        Vector2 spawnPos  = from + direction * 0.5f + Vector2.up * 0.3f;

        ArrowProjectile arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        arrow.speed       = arrowSpeed;
        arrow.lifetime    = arrowLifetime;
        arrow.damage      = arrowDamage;
        arrow.friendlyFire = false;

        if (arrow.NetworkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            arrow.NetworkObject.Spawn(true);

        // Pass the arrow's own NetworkObject as the "shooter" so it can never
        // hit itself; PowerMeter.NoAttacker = no kill-credit for any player
        arrow.ServerLaunch(direction, PowerMeter.NoAttacker, arrowDamage, arrow.NetworkObject);

        PlayShootVFXRpc(spawnPos);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayShootVFXRpc(Vector2 pos)
    {
        CameraFollow.Shake(0.04f);
    }

    // ─────────────────────────────────────────────────────────────────────

    Health FindClosestPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return null;

        Health  best    = null;
        float   bestSqr = giveUpRange * giveUpRange;
        Vector2 here    = transform.position;

        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Health hp = client.PlayerObject.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float d2 = ((Vector2)hp.transform.position - here).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = hp; }
        }
        return best;
    }

    void SetSpeed(float speed)
    {
        if (anim != null) anim.SetFloat("Speed", speed);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, idealRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, tooCloseRange);
    }
}
