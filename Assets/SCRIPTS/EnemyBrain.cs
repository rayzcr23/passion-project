using Unity.Netcode;
using UnityEngine;

/// Chase-and-hit enemy AI. All decisions run on the server only; clients just
/// watch the replicated transform and facing, so every player sees the same fight.
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class EnemyBrain : NetworkBehaviour
{
    [Header("Senses")]
    public float aggroRange = 6f;
    public float attackRange = 0.9f;
    public float giveUpRange = 10f;

    [Header("Movement")]
    public float moveSpeed = 2f;

    [Header("Attack")]
    public int damage = 5;
    public float attackCooldown = 1.2f;

    readonly NetworkVariable<bool> faceLeft = new NetworkVariable<bool>();

    Rigidbody2D rb;
    Health health;
    SpriteRenderer sprite;
    Health target;
    float nextAttackTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // Facing is cosmetic, so every client applies it from the replicated value.
        if (sprite != null) sprite.flipX = faceLeft.Value;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        if (health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (target == null || target.IsDead || Far(target, giveUpRange))
            target = FindNearestPlayer();

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

        faceLeft.Value = toTarget.x < 0f;

        if (distance > attackRange)
        {
            rb.linearVelocity = toTarget.normalized * moveSpeed;
            return;
        }

        // In range: stop and swing on cooldown.
        rb.linearVelocity = Vector2.zero;

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            target.ServerTakeDamage(damage, OwnerClientId);
        }
    }

    bool Far(Health h, float range)
    {
        return ((Vector2)h.transform.position - (Vector2)transform.position).sqrMagnitude > range * range;
    }

    /// Server-side. Players are the spawned PlayerObjects, so no scene scanning needed.
    Health FindNearestPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return null;

        Health best = null;
        float bestSqr = float.MaxValue;
        Vector2 here = transform.position;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            NetworkObject player = client.PlayerObject;
            if (player == null) continue;

            Health hp = player.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float sqr = ((Vector2)player.transform.position - here).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = hp; }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
