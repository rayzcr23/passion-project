using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Hollow Purple: a full-POW beam that erases everything in a long corridor.
///
/// Replaces the ground crack. Where that was a ring around you, this is a line
/// through the map - so it rewards lining enemies up rather than standing in
/// the middle of them, and reads completely differently on screen.
public class HollowPurpleAbility : Ability
{
    [Header("Beam")]
    public float length = 22f;
    public float width = 2.4f;

    [Tooltip("Delay between the charge starting and the damage landing, so the " +
         "beam visibly winds up before anything dies.")]
    public float chargeSeconds = 0.35f;

    [Header("Damage")]
    public LayerMask hitLayers = ~0;

    [Tooltip("Anything that is not a boss is simply erased.")]
    public bool oneShotsNonBosses = true;

    [Range(0f, 1f)] public float bossPercent = 0.35f;
    public int fallbackDamage = 500;
    public bool friendlyFire = false;

    [Header("Feel")]
    public float chargeShake = 0.35f;
    public float fireShake = 1f;
    public float knockback = 12f;

    [Header("Effects")]
    public GameObject impactPrefab;      // DeathPoof, scattered along the beam
    public int impactBursts = 6;
    public float impactScale = 1.8f;

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        StartCoroutine(FireRoutine(aimDirection));
    }

    IEnumerator FireRoutine(Vector2 aimDirection)
    {
        // Matches the visual's charge, so the damage lands as the beam punches out.
        if (chargeSeconds > 0f) yield return new WaitForSeconds(chargeSeconds);

        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        Vector2 origin = transform.position;
        Vector2 centre = origin + dir * (length * 0.5f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // A rotated box is the corridor: everything in the line, nothing beside it.
        Collider2D[] found = Physics2D.OverlapBoxAll(
            centre, new Vector2(length, width), angle, hitLayers);

        foreach (Collider2D col in found)
        {
            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.gameObject == gameObject) continue;

            bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
            if (!friendlyFire && isPlayer) continue;

            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            if (target.isBoss)
                target.ServerTakePercentDamage(bossPercent, OwnerClientId);
            else if (oneShotsNonBosses)
                target.ServerTakeDamage(Mathf.Max(target.Max, fallbackDamage), OwnerClientId, dir);
            else
                target.ServerTakeDamage(fallbackDamage, OwnerClientId, dir);

            if (knockback > 0f)
            {
                var receiver = target.GetComponent<KnockbackReceiver>();
                if (receiver != null) receiver.ServerApply(dir, knockback);
            }
        }
    }

    protected override void OnClientEffects(Vector2 aimDirection)
    {
        StartCoroutine(EffectRoutine(aimDirection));
    }

    IEnumerator EffectRoutine(Vector2 aimDirection)
    {
        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;

        var body = GetComponent<SpriteRenderer>();
        int order = body != null ? body.sortingOrder + 10 : 40;

        BeamEffect.Spawn(transform.position, dir, length, width, order);

        // Rumble while it charges, so the screen is already unsettled when it fires.
        float t = 0f;
        while (t < chargeSeconds)
        {
            t += Time.deltaTime;
            CameraFollow.Shake(chargeShake * Time.deltaTime * 6f);
            yield return null;
        }

        CameraFollow.Shake(fireShake);

        if (impactPrefab == null) yield break;

        // Detonations walk down the beam rather than all landing at once.
        for (int i = 0; i < impactBursts; i++)
        {
            float along = (i + 0.5f) / impactBursts * length;
            Vector2 offset = Random.insideUnitCircle * width * 0.4f;

            Vector3 at = transform.position
                       + (Vector3)(dir * along)
                       + new Vector3(offset.x, offset.y, 0f);

            GameObject fx = Instantiate(impactPrefab, at, Quaternion.identity);
            fx.transform.localScale = Vector3.one * impactScale;

            var sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.85f, 0.6f, 1f);

            CameraFollow.Shake(0.12f);
            yield return new WaitForSeconds(0.04f);
        }
    }

    void OnDrawGizmosSelected()
    {
        var loadout = GetComponent<AbilityLoadout>();
        Vector3 dir = loadout != null
            ? (Vector3)loadout.LastAttackDirection
            : Vector3.right;

        Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position + dir.normalized * (length * 0.5f),
            Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg),
            Vector3.one);

        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, width, 0.1f));
    }
}
