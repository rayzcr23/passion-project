using Unity.Netcode;
using UnityEngine;

/// TNT Goblin: throws dynamite from a distance, and rushes at the player to
/// explode in a suicide attack when dropped to half health or below.
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class TNTGoblinBrain : NetworkBehaviour
{
    [Header("Senses")]
    public float aggroRange = 10f;
    public float minRange = 3.5f;
    public float maxRange = 6.5f;

    [Header("Movement")]
    public float normalSpeed = 2.2f;
    public float kamikazeSpeed = 5.2f;

    [Header("Bomb Attack")]
    public DynamiteProjectile dynamitePrefab;
    public float throwCooldown = 1.6f;

    [Header("Suicide Rush (<= 50% HP)")]
    public float suicideDetonationDistance = 1.2f;
    public float suicideFuseTime = 3.5f;
    public int suicideDamage = 70;
    public float suicideRadius = 4.2f;
    public float suicideKnockback = 6.5f;

    [Header("Hit Reaction")]
    public float hitstunSeconds = 0.2f;

    readonly NetworkVariable<bool> faceLeft = new NetworkVariable<bool>();
    readonly NetworkVariable<bool> isKamikaze = new NetworkVariable<bool>();

    Rigidbody2D rb;
    Health health;
    SpriteRenderer sprite;
    Animator anim;
    KnockbackReceiver knockbackReceiver;
    Health target;

    float nextThrowTime;
    float hitstunUntil;
    float suicideTimer;
    int lastKnownHealth = -1;
    bool hasSelfDestructed;
    bool localKamikaze;

    bool IsKamikazeActive => (NetworkObject != null && NetworkObject.IsSpawned) ? isKamikaze.Value : localKamikaze;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        sprite = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
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
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && health != null) health.OnChanged -= ServerNoteHealthChange;
    }

    void ServerNoteHealthChange(int current, int max)
    {
        if (current < lastKnownHealth) hitstunUntil = Time.time + hitstunSeconds;
        lastKnownHealth = current;

        // Check for 50% HP trigger
        if (!IsKamikazeActive && current > 0 && current <= max / 2)
        {
            TriggerKamikaze();
        }
    }

    void TriggerKamikaze()
    {
        localKamikaze = true;
        if (NetworkObject != null && NetworkObject.IsSpawned)
            isKamikaze.Value = true;
        suicideTimer = suicideFuseTime;
        if (NetworkObject != null && NetworkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            TriggerKamikazeVFXRpc();
        else
            CameraFollow.Shake(0.18f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void TriggerKamikazeVFXRpc()
    {
        CameraFollow.Shake(0.18f);
    }

    void Update()
    {
        // Replicated facing
        if (sprite != null && NetworkObject != null && NetworkObject.IsSpawned) sprite.flipX = faceLeft.Value;

        // Visual flash when in Kamikaze mode
        if (IsKamikazeActive && sprite != null)
        {
            float flash = Mathf.PingPong(Time.time * 12f, 1f);
            sprite.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.25f), flash);
        }
    }

    void FixedUpdate()
    {
        if ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) || health.IsDead || hasSelfDestructed) return;

        // Reeling from hitstun
        if (Time.time < hitstunUntil || (knockbackReceiver != null && knockbackReceiver.IsRecovering))
            return;

        target = FindClosestPlayer();
        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 myPos = transform.position;
        Vector2 targetPos = target.transform.position;
        Vector2 delta = targetPos - myPos;
        float dist = delta.magnitude;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            faceLeft.Value = delta.x < 0f;
        else if (sprite != null)
            sprite.flipX = delta.x < 0f;

        if (IsKamikazeActive)
        {
            // Kamikaze Rush: charge full speed straight at the player!
            suicideTimer -= Time.fixedDeltaTime;
            Vector2 dir = delta.normalized;
            rb.linearVelocity = dir * kamikazeSpeed;

            // Explode if close enough or if suicide timer ran out
            if (dist <= suicideDetonationDistance || suicideTimer <= 0f)
            {
                ServerSuicideDetonate();
            }
        }
        else
        {
            // Tactical Ranged Bomber: maintain distance
            if (dist > maxRange)
            {
                // Move closer
                rb.linearVelocity = delta.normalized * normalSpeed;
            }
            else if (dist < minRange)
            {
                // Retreat away from player
                rb.linearVelocity = -delta.normalized * (normalSpeed * 1.1f);
            }
            else
            {
                // In sweet spot: slow down or circle slightly
                rb.linearVelocity = Vector2.zero;
            }

            // Throw dynamite
            if (dist <= aggroRange && Time.time >= nextThrowTime)
            {
                nextThrowTime = Time.time + throwCooldown;
                ServerThrowDynamite(targetPos);
            }
        }
    }

    void ServerThrowDynamite(Vector2 targetPos)
    {
        if (dynamitePrefab == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
            {
                foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
                {
                    if (p.Prefab != null && p.Prefab.GetComponent<DynamiteProjectile>() != null)
                    {
                        dynamitePrefab = p.Prefab.GetComponent<DynamiteProjectile>();
                        break;
                    }
                }
            }
        }
        if (dynamitePrefab == null) return;

        // Slight spread / prediction
        Vector2 leadOffset = (Random.insideUnitCircle * 0.4f);
        Vector2 aimAt = targetPos + leadOffset;

        bool isFacingLeft = (NetworkObject != null && NetworkObject.IsSpawned) ? faceLeft.Value : (sprite != null && sprite.flipX);
        Vector2 spawnAt = (Vector2)transform.position + new Vector2(isFacingLeft ? -0.4f : 0.4f, 0.5f);
        DynamiteProjectile bomb = Instantiate(dynamitePrefab, spawnAt, Quaternion.identity);
        if (bomb.NetworkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            bomb.NetworkObject.Spawn(true);
        }
        bomb.ServerLaunch(spawnAt, aimAt);
    }

    void ServerSuicideDetonate()
    {
        if (hasSelfDestructed) return;
        hasSelfDestructed = true;

        Vector2 center = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, suicideRadius);

        foreach (Collider2D col in hits)
        {
            Health hp = col.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead && hp != health)
            {
                hp.ServerTakeDamage(suicideDamage, 0);

                var receiver = hp.GetComponent<KnockbackReceiver>();
                if (receiver != null)
                {
                    Vector2 away = (Vector2)hp.transform.position - center;
                    if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
                    receiver.ServerApply(away.normalized, suicideKnockback);
                }
            }
        }

        if (NetworkObject != null && NetworkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SpawnSuicideExplosionRpc(center);
        }
        else
        {
            ExplosionEffect.Spawn(center, suicideRadius);
            CameraFollow.Shake(0.5f);
        }

        // Despawn goblin
        health.ServerTakeDamage(9999, 0);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SpawnSuicideExplosionRpc(Vector2 pos)
    {
        ExplosionEffect.Spawn(pos, suicideRadius);
        CameraFollow.Shake(0.5f);
    }

    Health FindClosestPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return null;

        Health closest = null;
        float bestDistSqr = aggroRange * aggroRange;
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
}
