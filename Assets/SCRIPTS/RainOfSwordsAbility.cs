using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Rain of Swords: the Special escalated. The ring of blades is six swords
/// orbiting you; this is fifty falling out of the sky.
///
/// Damage lands in waves rather than per-blade, because trying to match a
/// server hit to a particular client-side sprite would mean replicating every
/// blade's landing spot. The waves are timed to the storm, so it reads as the
/// swords doing it.
public class RainOfSwordsAbility : Ability
{
    [Header("Area")]
    public float radius = 5.5f;
    public LayerMask hitLayers = ~0;

    [Header("Storm")]
    public int swordCount = 34;
    public float duration = 2.6f;
    public int waves = 6;

    [Header("Damage per wave, as a fraction of max health")]
    [Range(0f, 1f)] public float normalPercent = 0.22f;
    [Range(0f, 1f)] public float bossPercent = 0.055f;

    [Tooltip("Floor, so a wave always hurts something with a small health pool.")]
    public int minimumDamage = 6;

    public bool friendlyFire = false;
    public float knockback = 3f;

    [Header("Effect")]
    public Sprite bladeSprite;
    public GameObject impactPrefab;
    public float openingShake = 0.55f;

    Vector3 origin;

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        // Captured at the moment of casting: the storm stays where it was called
        // down, so you can walk out of your own Super.
        origin = transform.position;
        StartCoroutine(StormRoutine());
    }

    IEnumerator StormRoutine()
    {
        float gap = duration / Mathf.Max(1, waves);

        for (int wave = 0; wave < waves; wave++)
        {
            if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

            // Centred where it was cast, not on the caster: the storm stays put
            // and you can walk out of your own Super.
            DamageWave();
            yield return new WaitForSeconds(gap);
        }
    }

    void DamageWave()
    {
        Collider2D[] found = Physics2D.OverlapCircleAll(origin, radius, hitLayers);

        foreach (Collider2D col in found)
        {
            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.gameObject == gameObject) continue;

            bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
            if (!friendlyFire && isPlayer) continue;

            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            float percent = target.isBoss ? bossPercent : normalPercent;
            int damage = Mathf.Max(minimumDamage, Mathf.RoundToInt(target.Max * percent));

            Vector2 away = (Vector2)target.transform.position - (Vector2)origin;
            target.ServerTakeDamage(ScaleDamage(damage), OwnerClientId,
                away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up);

            if (knockback > 0f)
            {
                var receiver = target.GetComponent<KnockbackReceiver>();
                if (receiver != null && away.sqrMagnitude > 0.0001f)
                    receiver.ServerApply(away.normalized, knockback);
            }
        }
    }

    protected override void OnClientEffects(Vector2 aimDirection)
    {
        var body = GetComponent<SpriteRenderer>();
        int order = body != null ? body.sortingOrder + 10 : 40;

        SwordRainEffect.Spawn(transform.position, bladeSprite, impactPrefab,
                              radius, swordCount, duration, order);

        CameraFollow.Shake(openingShake);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
