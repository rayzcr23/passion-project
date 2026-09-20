using Unity.Netcode;
using UnityEngine;

/// The Swordsman's Super: cracks the ground around him.
///
/// The PRD specifies this as a percentage of health rather than flat damage -
/// 50% to normal enemies, 60 to bosses - which is why Health has
/// ServerTakePercentDamage. Percentage damage means it stays relevant against a
/// 500hp boss without trivialising ordinary goblins.
public class GroundCrackAbility : Ability
{
    [Header("Area")]
    public float radius = 4f;
    public LayerMask hitLayers = ~0;

    [Header("Damage (fraction of max health)")]
    [Range(0f, 1f)] public float normalPercent = 0.5f;
    [Range(0f, 1f)] public float bossPercent = 0.6f;

    [Tooltip("Off by default - a 50% nuke that also hits your brothers is not a Super.")]
    public bool friendlyFire = false;

    [Header("Effect")]
    public GameObject effectPrefab;
    public int effectBursts = 5;
    public float effectScale = 2.2f;
    public float shake = 0.7f;

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        Collider2D[] found = Physics2D.OverlapCircleAll(transform.position, radius, hitLayers);

        foreach (Collider2D col in found)
        {
            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.gameObject == gameObject) continue;

            bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
            if (!friendlyFire && isPlayer) continue;

            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            float percent = target.isBoss ? bossPercent : normalPercent;
            target.ServerTakePercentDamage(percent, OwnerClientId);

            // Shove everything outward so the crack reads as force, not just numbers.
            Vector2 away = (Vector2)target.transform.position - (Vector2)transform.position;
            if (away.sqrMagnitude > 0.0001f)
            {
                var receiver = target.GetComponent<KnockbackReceiver>();
                if (receiver != null) receiver.ServerApply(away.normalized, 6f);
            }
        }
    }

    protected override void OnClientEffects(Vector2 aimDirection)
    {
        CameraFollow.Shake(shake);

        if (effectPrefab == null) return;

        // Scatter bursts across the area rather than one poof in the middle, so
        // the size of the Super is legible.
        for (int i = 0; i < Mathf.Max(1, effectBursts); i++)
        {
            Vector2 offset = i == 0
                ? Vector2.zero
                : Random.insideUnitCircle * radius * 0.8f;

            GameObject fx = Instantiate(effectPrefab,
                transform.position + new Vector3(offset.x, offset.y, 0f),
                Quaternion.identity);

            fx.transform.localScale = Vector3.one * effectScale;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
