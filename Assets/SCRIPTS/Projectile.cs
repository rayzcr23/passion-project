using Unity.Netcode;
using UnityEngine;

/// Server spawns it, every client simulates the straight-line flight locally from
/// the replicated heading. No NetworkTransform, so a screen full of fireballs
/// costs almost no bandwidth.
/// Prefab needs: NetworkObject, Rigidbody2D (gravityScale 0), trigger Collider2D.
[RequireComponent(typeof(Rigidbody2D), typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    public float speed = 12f;
    public float lifetime = 3f;
    public int damage = 1;
    public LayerMask hitLayers = ~0;
    public GameObject impactEffect;

    [Tooltip("On: brothers can hit each other (duels, PvP). Off: co-op PvE only.")]
    public bool friendlyFire = true;

    readonly NetworkVariable<Vector2> heading = new NetworkVariable<Vector2>(Vector2.right);
    readonly NetworkVariable<int> sortingOrder = new NetworkVariable<int>(1);

    ulong shooterClientId;
    Rigidbody2D rb;
    SpriteRenderer sprite;
    float age;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
    }

    /// Server only. Call straight after NetworkObject.Spawn().
    public void ServerLaunch(Vector2 direction, ulong shooter)
    {
        shooterClientId = shooter;
        heading.Value = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        sortingOrder.Value = 1;
    }

    public override void OnNetworkSpawn()
    {
        Vector2 dir = heading.Value;

        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        rb.linearVelocity = dir * speed;

        if (sprite != null) sprite.sortingOrder = sortingOrder.Value;
    }

    void Update()
    {
        // Only the server retires it; despawn replicates to everyone.
        if (!IsServer) return;

        age += Time.deltaTime;
        if (age >= lifetime) NetworkObject.Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Damage is a server decision. Clients run this too but must not act on it.
        if (!IsServer) return;
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // Don't let a shot hit the player who fired it as it leaves the muzzle.
        NetworkObject hitObject = other.GetComponentInParent<NetworkObject>();
        if (hitObject != null && hitObject.OwnerClientId == shooterClientId) return;

        Health target = other.GetComponentInParent<Health>();
        if (target != null && !target.IsDead)
        {
            bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            bool allowed = (friendlyFire || !isPlayer) && (downed == null || !downed.IsDead);

            if (allowed) target.ServerTakeDamage(damage, shooterClientId);
        }

        if (impactEffect != null) SpawnImpactRpc(transform.position);

        NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SpawnImpactRpc(Vector3 position)
    {
        // Purely cosmetic, so each client makes its own - no need to network it.
        if (impactEffect != null) Instantiate(impactEffect, position, Quaternion.identity);
    }
}
