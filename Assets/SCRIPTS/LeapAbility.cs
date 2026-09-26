using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Executioner's Leap: launch forward, hang, and come down blade-first.
///
/// Movement is owner-authoritative, so the server cannot simply move the player -
/// it asks the owner to perform the jump, waits out the arc, then applies the
/// landing damage itself at wherever they actually ended up. Damage stays on the
/// server; only the travel is trusted to the client, which is already true of
/// every step they take.
public class LeapAbility : Ability
{
    [Header("Leap")]
    public float distance = 4.2f;
    public float airSeconds = 0.42f;

    [Tooltip("How high the sprite rises. Purely visual - the collider never leaves " +
         "the ground, so you cannot leap over walls.")]
    public float arcHeight = 1.1f;

    [Header("Landing")]
    public float impactRadius = 2.3f;
    public int damage = 26;
    public LayerMask hitLayers = ~0;
    public float knockback = 9f;
    public bool friendlyFire = false;

    [Header("Feel")]
    public float landShake = 0.5f;
    public GameObject impactPrefab;
    public int crackCount = 9;

    public bool IsLeaping { get; private set; }

    SpriteRenderer body;
    Rigidbody2D rb;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        StartCoroutine(ServerRoutine(aimDirection));
    }

    IEnumerator ServerRoutine(Vector2 aimDirection)
    {
        LeapOwnerRpc(aimDirection);

        // Wait out the arc, then hit whatever is under them when they land.
        yield return new WaitForSeconds(airSeconds);

        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

        Vector2 centre = transform.position;
        Collider2D[] found = Physics2D.OverlapCircleAll(centre, impactRadius, hitLayers);

        foreach (Collider2D col in found)
        {
            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.gameObject == gameObject) continue;

            bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
            if (!friendlyFire && isPlayer) continue;

            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            Vector2 away = (Vector2)target.transform.position - centre;
            Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : aimDirection;

            target.ServerTakeDamage(ScaleDamage(damage), OwnerClientId, dir);

            var receiver = target.GetComponent<KnockbackReceiver>();
            if (receiver != null && knockback > 0f) receiver.ServerApply(dir, knockback);
        }

        LandedRpc(centre);
    }

    /// Only the owner moves - anyone else would fight NetworkTransform.
    [Rpc(SendTo.Owner)]
    void LeapOwnerRpc(Vector2 aimDirection)
    {
        StartCoroutine(TravelRoutine(aimDirection));
    }

    IEnumerator TravelRoutine(Vector2 aimDirection)
    {
        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;

        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(dir * distance);

        IsLeaping = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Vector3 spriteRest = body != null ? body.transform.localPosition : Vector3.zero;
        float elapsed = 0f;

        while (elapsed < airSeconds)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / airSeconds);

            transform.position = Vector3.Lerp(start, end, k);

            // Height is faked by lifting the sprite, not the transform: lifting the
            // root would take the collider with it and you would fly over walls.
            if (body != null)
            {
                float lift = Mathf.Sin(k * Mathf.PI) * arcHeight;
                body.transform.localPosition = spriteRest + new Vector3(0f, lift, 0f);
            }

            yield return null;
        }

        if (body != null) body.transform.localPosition = spriteRest;
        IsLeaping = false;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void LandedRpc(Vector3 at)
    {
        CameraFollow.Shake(landShake);

        if (impactPrefab != null)
        {
            GameObject fx = Instantiate(impactPrefab, at, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 1.3f;
        }

        ShockwaveEffect.Spawn(at, impactRadius, crackCount,
                              body != null ? body.sortingOrder + 5 : 40);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, impactRadius);
    }
}
