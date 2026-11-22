using Unity.Netcode;
using UnityEngine;

/// Thrown by the TNT Goblin. Travels in an arc, lands with a sizzling wick,
/// then explodes in an AoE blast with damage, knockback, and screen shake.
[RequireComponent(typeof(NetworkObject))]
public class DynamiteProjectile : NetworkBehaviour
{
    public float explosionRadius = 2.4f;
    public int damage = 45;
    public float knockback = 5.5f;
    public float flightDuration = 0.75f;
    public float fuseDuration = 1.0f;
    public LayerMask hitLayers = ~0;

    readonly NetworkVariable<Vector2> startPos = new NetworkVariable<Vector2>();
    readonly NetworkVariable<Vector2> targetPos = new NetworkVariable<Vector2>();

    SpriteRenderer sprite;
    float flightTimer;
    float fuseTimer;
    bool hasExploded;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    /// Server only: call immediately after NetworkObject.Spawn()
    public void ServerLaunch(Vector2 from, Vector2 to)
    {
        startPos.Value = from;
        targetPos.Value = to;
    }

    public override void OnNetworkSpawn()
    {
        transform.position = startPos.Value;
    }

    void Update()
    {
        if (TimeStopWorld.IsStopped) return;
        if (flightTimer < flightDuration)
        {
            flightTimer += Time.deltaTime;
            float t = Mathf.Clamp01(flightTimer / flightDuration);

            // Parabolic arc trajectory
            Vector2 horizontal = Vector2.Lerp(startPos.Value, targetPos.Value, t);
            float height = 4f * 1.8f * t * (1f - t);
            transform.position = new Vector3(horizontal.x, horizontal.y + height, 0f);

            // Tumble in the air
            transform.Rotate(0f, 0f, 720f * Time.deltaTime);

            if (t >= 1f)
            {
                transform.rotation = Quaternion.identity;
            }
        }
        else
        {
            // Sizzling on the ground
            fuseTimer += Time.deltaTime;

            // Flash rapidly as fuse runs down
            if (sprite != null)
            {
                float flashSpeed = Mathf.Lerp(8f, 25f, fuseTimer / fuseDuration);
                sprite.color = (Mathf.Sin(fuseTimer * flashSpeed) > 0f) ? Color.red : Color.white;
            }

            if (IsServer && fuseTimer >= fuseDuration && !hasExploded)
            {
                Explode();
            }
        }
    }

    void Explode()
    {
        if (!IsServer || hasExploded) return;
        hasExploded = true;

        Vector2 center = transform.position;
        Collider2D[] victims = Physics2D.OverlapCircleAll(center, explosionRadius, hitLayers);

        foreach (Collider2D col in victims)
        {
            Health hp = col.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.ServerTakeDamage(damage, 0);

                var receiver = hp.GetComponent<KnockbackReceiver>();
                if (receiver != null)
                {
                    Vector2 away = ((Vector2)hp.transform.position - center);
                    if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
                    receiver.ServerApply(away.normalized, knockback);
                }
            }
        }

        SpawnExplosionRpc(center);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SpawnExplosionRpc(Vector2 pos)
    {
        ExplosionEffect.Spawn(pos, explosionRadius);
    }
}
