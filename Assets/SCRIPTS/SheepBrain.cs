using Unity.Netcode;
using UnityEngine;

/// A passive critter: wanders near where it spawned, and bolts when hurt.
///
/// Deliberately not an EnemyBrain with damage set to zero - a sheep never needs
/// to find a target, and a passive mob that quietly runs a hostile AI every
/// physics step is waste you pay for on every sheep on the map.
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class SheepBrain : NetworkBehaviour
{
    [Header("Wander")]
    public float wanderRadius = 3f;
    public float moveSpeed = 0.7f;
    public float arriveDistance = 0.2f;
    public float pauseMin = 1.5f;
    public float pauseMax = 4.5f;

    [Header("Flee")]
    public float fleeSpeed = 2.8f;
    public float fleeSeconds = 3f;

    readonly NetworkVariable<bool> faceLeft = new NetworkVariable<bool>();

    Rigidbody2D rb;
    Health health;
    SpriteRenderer sprite;
    Animator anim;
    KnockbackReceiver recoil;

    Vector2 home;
    Vector2 target;
    float resumeTime;
    float fleeUntil;
    Vector2 fleeDirection;
    int lastKnownHealth = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        sprite = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        recoil = GetComponent<KnockbackReceiver>();
    }

    public override void OnNetworkSpawn()
    {
        home = transform.position;
        target = home;

        if (IsServer)
        {
            lastKnownHealth = health.Current;
            health.OnChanged += ServerNoteHealthChange;
            resumeTime = Time.time + Random.Range(pauseMin, pauseMax);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && health != null) health.OnChanged -= ServerNoteHealthChange;
    }

    void ServerNoteHealthChange(int current, int max)
    {
        if (current < lastKnownHealth)
        {
            fleeUntil = Time.time + fleeSeconds;

            // Run away from whoever is nearest - close enough to "who hit me"
            // without threading the attacker through the damage call.
            fleeDirection = AwayFromNearestPlayer();
        }
        lastKnownHealth = current;
    }

    void Update()
    {
        if (sprite != null) sprite.flipX = faceLeft.Value;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        if (health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            SetSpeed(0f);
            return;
        }

        if (recoil != null && recoil.IsRecovering)
        {
            SetSpeed(0f);
            return;
        }

        if (Time.time < fleeUntil)
        {
            Move(fleeDirection, fleeSpeed);
            return;
        }

        Vector2 here = transform.position;
        Vector2 toTarget = target - here;

        if (toTarget.magnitude <= arriveDistance)
        {
            rb.linearVelocity = Vector2.zero;
            SetSpeed(0f);

            if (Time.time >= resumeTime)
            {
                target = home + Random.insideUnitCircle * wanderRadius;
                resumeTime = Time.time + Random.Range(pauseMin, pauseMax);
            }
            return;
        }

        Move(toTarget.normalized, moveSpeed);
    }

    void Move(Vector2 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        rb.linearVelocity = direction * speed;
        SetSpeed(speed);

        bool wantsLeft = direction.x < 0f;
        if (faceLeft.Value != wantsLeft) faceLeft.Value = wantsLeft;
    }

    Vector2 AwayFromNearestPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return Random.insideUnitCircle.normalized;

        Vector2 here = transform.position;
        Vector2 best = Vector2.zero;
        float bestSqr = float.MaxValue;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            NetworkObject player = client.PlayerObject;
            if (player == null) continue;

            Vector2 delta = here - (Vector2)player.transform.position;
            float sqr = delta.sqrMagnitude;

            if (sqr < bestSqr) { bestSqr = sqr; best = delta; }
        }

        return best.sqrMagnitude > 0.0001f
            ? best.normalized
            : Random.insideUnitCircle.normalized;
    }

    void SetSpeed(float speed)
    {
        if (anim != null) anim.SetFloat("Speed", speed);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 1f, 0.6f, 0.4f);
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)home : transform.position, wanderRadius);
    }
}
