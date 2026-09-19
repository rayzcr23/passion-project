using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// The Swordsman's M1. A swing is a wedge of damage in the aim direction, so one
/// animation rotated by AimPivot covers all 360 degrees - no directional attack
/// sets needed.
public class MeleeArcAbility : Ability
{
    [Header("Arc")]
    public Transform origin;                // usually the aim pivot
    public float range = 1.2f;
    public float arcDegrees = 120f;

    [Header("Damage")]
    public int damage = 10;
    public LayerMask hitLayers = ~0;
    public int maxTargets = 8;

    [Tooltip("On: brothers can hit each other (duels, PvP). Off: co-op PvE only.")]
    public bool friendlyFire = true;

    [Tooltip("Delay before the hit registers, so it lands when the blade is out " +
             "rather than the instant the key goes down.")]
    public float windupSeconds = 0.15f;

    [Header("Feel")]
    public float knockback = 2f;

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        StartCoroutine(SwingRoutine(aimDirection));
    }

    IEnumerator SwingRoutine(Vector2 aimDirection)
    {
        if (windupSeconds > 0f) yield return new WaitForSeconds(windupSeconds);

        // The object may have died or despawned during the windup.
        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

        Vector2 centre = origin != null ? (Vector2)origin.position : (Vector2)transform.position;
        Collider2D[] candidates = Physics2D.OverlapCircleAll(centre, range, hitLayers);

        float halfArc = arcDegrees * 0.5f;
        int hits = 0;

        foreach (Collider2D col in candidates)
        {
            if (hits >= maxTargets) break;

            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead) continue;

            // Never hit yourself. Other players only when friendly fire is on.
            if (target.gameObject == gameObject) continue;
            if (!friendlyFire && target.GetComponent<AbilityLoadout>() != null) continue;

            // Dead players are not punching bags.
            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            Vector2 toTarget = (Vector2)col.bounds.ClosestPoint(centre) - centre;
            if (toTarget.sqrMagnitude > 0.0001f &&
                Vector2.Angle(aimDirection, toTarget.normalized) > halfArc) continue;

            target.ServerTakeDamage(damage, OwnerClientId);

            if (knockback > 0f)
            {
                var rb = target.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                    rb.AddForce(aimDirection.normalized * knockback, ForceMode2D.Impulse);
            }

            hits++;
        }
    }

    // Draws the wedge in the Scene view so the range and angle can be tuned by eye.
    void OnDrawGizmosSelected()
    {
        Transform t = origin != null ? origin : transform;
        Vector3 facing = Application.isPlaying ? (Vector3)Vector2.right : t.right;

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(t.position, range);

        Vector3 a = Quaternion.Euler(0f, 0f, arcDegrees * 0.5f) * facing * range;
        Vector3 b = Quaternion.Euler(0f, 0f, -arcDegrees * 0.5f) * facing * range;
        Gizmos.DrawLine(t.position, t.position + a);
        Gizmos.DrawLine(t.position, t.position + b);
    }
}
