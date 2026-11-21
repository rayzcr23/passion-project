using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Server-controlled magic ball launched after the Skeleton Guardian's stomp.
/// It steers toward its chosen player and damages only player characters.
[RequireComponent(typeof(NetworkObject), typeof(SpriteRenderer))]
public class SkeletonHomingOrb : NetworkBehaviour
{
    public float speed = 7f;
    public float turnDegreesPerSecond = 330f;
    public float lifetime = 6f;
    public float hitRadius = 0.45f;
    public int damage = 38;
    public float knockback = 5f;

    NetworkObject target;
    SpriteRenderer sprite;
    Vector2 heading = Vector2.right;
    float age;
    float visualTime;
    bool resolved;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        if (sprite.sprite == null) sprite.sprite = AuraVFXLibrary.GetGlowDisc();
        sprite.sortingOrder = Mathf.Max(sprite.sortingOrder, 32);
        transform.localScale = Vector3.one * 0.85f;
    }

    /// Called on the server immediately after NetworkObject.Spawn().
    public void ServerLaunch(NetworkObject targetObject, Vector2 initialDirection)
    {
        if (!IsServer) return;
        target = targetObject;
        heading = initialDirection.sqrMagnitude > 0.001f
            ? initialDirection.normalized : Vector2.right;
    }

    void Update()
    {
        visualTime += Time.deltaTime;
        float pulse = 0.85f + 0.12f * Mathf.Sin(visualTime * 13f);
        transform.localScale = Vector3.one * pulse;
        sprite.color = Color.Lerp(
            new Color(0.6f, 0.22f, 1f, 0.9f),
            new Color(0.2f, 0.9f, 1f, 1f),
            0.5f + 0.5f * Mathf.Sin(visualTime * 8f));
    }

    void FixedUpdate()
    {
        if (!IsServer || resolved) return;

        age += Time.fixedDeltaTime;
        if (age >= lifetime)
        {
            Resolve(false, transform.position);
            return;
        }

        if (target != null)
        {
            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && !targetHealth.IsDead)
            {
                Vector2 desired = (Vector2)(target.transform.position - transform.position);
                if (desired.sqrMagnitude > 0.0001f)
                {
                    heading = (Vector2)Vector3.RotateTowards(
                        heading, desired.normalized,
                        turnDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                    heading.Normalize();
                }
            }
        }

        Vector2 from = transform.position;
        Vector2 step = heading * (speed * Time.fixedDeltaTime);
        Health victim = FindPlayerHit(from, step);
        if (victim != null)
        {
            victim.ServerTakeDamage(damage, PowerMeter.NoAttacker, heading);
            KnockbackReceiver receiver = victim.GetComponent<KnockbackReceiver>();
            if (receiver != null) receiver.ServerApply(heading, knockback);
            Resolve(true, victim.transform.position);
            return;
        }

        transform.position = from + step;
    }

    Health FindPlayerHit(Vector2 from, Vector2 step)
    {
        // The overlap catches a player already touching the orb, while the
        // cast catches players crossed between physics ticks.
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(from, hitRadius))
        {
            Health player = PlayerHealth(hit);
            if (player != null) return player;
        }

        if (step.sqrMagnitude < 0.000001f) return null;
        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(
                     from, hitRadius, step.normalized, step.magnitude))
        {
            Health player = PlayerHealth(hit.collider);
            if (player != null) return player;
        }
        return null;
    }

    static Health PlayerHealth(Collider2D collider)
    {
        if (collider == null) return null;
        Health health = collider.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return null;
        return health.GetComponent<AbilityLoadout>() != null ? health : null;
    }

    void Resolve(bool hitPlayer, Vector3 position)
    {
        if (resolved) return;
        resolved = true;
        if (hitPlayer) ImpactRpc(position);
        StartCoroutine(DespawnSoon());
    }

    IEnumerator DespawnSoon()
    {
        yield return new WaitForSeconds(0.1f);
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ImpactRpc(Vector3 position)
    {
        SlamImpactEffect.Spawn(position, 1.1f, 42,
            new Color(0.5f, 0.2f, 1f), new Color(0.55f, 0.95f, 1f));
        CameraFollow.Shake(0.1f);
    }
}
