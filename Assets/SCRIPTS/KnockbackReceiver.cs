using Unity.Netcode;
using UnityEngine;

/// Applies knockback to an object whose movement is owner-authoritative.
///
/// The server cannot just AddForce a player's Rigidbody2D: the owning client
/// controls that transform, so the push is overwritten by the next
/// NetworkTransform update and nothing visibly happens. The impulse has to be
/// handed to the owner to apply itself.
///
/// It also briefly suspends movement input, because Movement assigns
/// linearVelocity every FixedUpdate and would cancel the impulse on the very
/// next step - the same reason enemy knockback needed hitstun.
[RequireComponent(typeof(Rigidbody2D))]
public class KnockbackReceiver : NetworkBehaviour
{
    [Tooltip("How long movement input is suppressed so the push can actually move you.")]
    public float recoverSeconds = 0.2f;

    Rigidbody2D rb;
    float recoverUntil;

    public bool IsRecovering => Time.time < recoverUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// Call on the server. Routes to whoever actually simulates this body.
    public void ServerApply(Vector2 direction, float force)
    {
        if (direction.sqrMagnitude < 0.000001f || force <= 0f) return;
        Vector2 impulse = direction.normalized * force;

        // Server-owned bodies (enemies) can be pushed directly.
        if (!IsSpawned || OwnerClientId == NetworkManager.ServerClientId)
        {
            Apply(impulse);
            return;
        }

        ApplyRpc(impulse);
    }

    [Rpc(SendTo.Owner)]
    void ApplyRpc(Vector2 impulse)
    {
        Apply(impulse);
    }

    void Apply(Vector2 impulse)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;

        recoverUntil = Time.time + recoverSeconds;

        // Clear first so a push against your current run does not cancel out.
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(impulse, ForceMode2D.Impulse);
    }
}
