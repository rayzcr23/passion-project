using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Glacial Burst: An ancient forbidden art taught by the Fisherman after slaying the Giant Fish.
/// Equips directly into the player's Special Ability slot ([Q]), costing 35 POW just like Executioner's Leap.
///
/// When activated, summons a 360-degree ring of 12 razor-sharp crystalline ice spikes that hover
/// around the player before violently erupting outward in all directions, impaling and shattering through enemies.
public class GlacialBurstAbility : Ability
{
    [Header("Glacial Ring")]
    public float ringRadius = 1.6f;
    public int spikeCount = 12;
    public float summonDuration = 0.28f;
    public float spikeSpeed = 24f;
    public float maxRange = 13f;

    [Header("Damage")]
    public int spikeDamage = 35;
    public float knockback = 7.5f;
    public LayerMask hitLayers = ~0;
    public bool friendlyFire = false;

    protected override void Awake()
    {
        base.Awake();
        displayName = "Glacial Burst";
        cooldown = 1.4f;
        powerCost = 35f; // Costs 35 POW, identical to Executioner's Leap
        animatorTrigger = "Attack2";
    }

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        StartCoroutine(ServerBurstRoutine());
    }

    IEnumerator ServerBurstRoutine()
    {
        Vector2 origin = transform.position;

        // Matches the visual summon delay where the ice spikes materialise in a ring
        float summonRemaining = summonDuration;
        while (summonRemaining > 0f)
        {
            if (!TimeStopWorld.IsStopped) summonRemaining -= Time.deltaTime;
            yield return null;
        }

        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

        // Track enemies already hit so a large boss isn't damaged 12 times in 1 frame unless hit by distinct spikes
        HashSet<Health> hitTargets = new HashSet<Health>();

        // Fire 12 radial piercing sweeps
        float flyTime = maxRange / spikeSpeed;
        float elapsed = 0f;
        float prevDist = 0f;

        while (elapsed < flyTime)
        {
            if (TimeStopWorld.IsStopped)
            {
                yield return null;
                continue;
            }
            elapsed += Time.deltaTime;
            float currentDist = Mathf.Min(maxRange, elapsed * spikeSpeed);
            prevDist = currentDist;

            for (int i = 0; i < spikeCount; i++)
            {
                float angleDeg = (360f / spikeCount) * i;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                Vector2 checkPos = origin + dir * currentDist;
                Collider2D[] found = Physics2D.OverlapCircleAll(checkPos, 0.5f, hitLayers);

                foreach (var col in found)
                {
                    Health target = col.GetComponentInParent<Health>();
                    if (target == null || target.IsDead || target.gameObject == gameObject) continue;
                    if (hitTargets.Contains(target)) continue;

                    bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
                    if (!friendlyFire && isPlayer) continue;

                    PlayerDeath downed = target.GetComponent<PlayerDeath>();
                    if (downed != null && downed.IsDead) continue;

                    hitTargets.Add(target);

                    int finalDamage = ScaleDamage(spikeDamage);
                    target.ServerTakeDamage(finalDamage, OwnerClientId, dir);

                    if (knockback > 0f)
                    {
                        var receiver = target.GetComponent<KnockbackReceiver>();
                        if (receiver != null) receiver.ServerApply(dir, knockback);
                    }
                }
            }

            yield return null;
        }
    }

    protected override void OnClientEffects(Vector2 aimDirection)
    {
        var body = GetComponent<SpriteRenderer>();
        int order = body != null ? body.sortingOrder + 10 : 40;

        GlacialBurstEffect.Spawn(transform.position, ringRadius, spikeCount, spikeSpeed, maxRange, order);
    }
}
