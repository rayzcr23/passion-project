using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Executioner's Leap: launch forward to the target cursor position, hang, and come down blade-first.
///
/// Hold to aim at the cursor and charge up devastating power: charging expands the landing
/// slam radius and multiplies the impact damage. Jump distance adapts directly to wherever
/// the cursor sits (clamped within range), displaying a live ground reticle and trajectory.
///
/// Movement is owner-authoritative, so the server asks the owner to perform the leap travel,
/// waits out the arc, and then applies the charged AoE slam damage server-side.
public class LeapAbility : Ability
{
    [Header("Leap Targeting")]
    [Tooltip("Maximum distance you can leap from current position.")]
    public float maxRange = 7.5f;

    [Tooltip("Minimum distance so you don't leap in place if aiming at your feet.")]
    public float minRange = 0.8f;

    [Tooltip("Fallback distance if maxRange is not specified.")]
    public float distance = 7.5f;

    [Tooltip("Travel time through the air.")]
    public float airSeconds = 0.42f;

    [Tooltip("How high the sprite rises. Purely visual - the collider never leaves " +
             "the ground, so you cannot leap over walls.")]
    public float arcHeight = 1.1f;

    [Header("Charging")]
    [Tooltip("Seconds required to reach 100% full charge.")]
    public float maxChargeTime = 0.85f;

    [Tooltip("Movement speed multiplier while holding/charging the leap.")]
    public float chargeMoveSpeedMultiplier = 0.45f;

    [Tooltip("Damage multiplier at 100% full charge.")]
    public float maxChargeDamageMultiplier = 1.8f;

    [Tooltip("Slam radius multiplier at 100% full charge.")]
    public float maxChargeRadiusMultiplier = 1.4f;

    [Tooltip("Maximum seconds you can hold before the leap auto-releases.")]
    public float maxHoldSeconds = 3.5f;

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

    [Header("Reticle Colors")]
    public Color indicatorColor = new Color(1f, 0.7f, 0.2f, 0.85f);
    public Color indicatorReadyColor = new Color(1f, 0.25f, 0.1f, 1f);

    public bool IsLeaping { get; private set; }
    public bool IsCharging { get; private set; }
    public float ChargeRatio { get; private set; }

    public override bool SupportsCharging => true;

    SpriteRenderer body;
    Rigidbody2D rb;
    LeapIndicator indicator;
    float chargeStartTime;

    Movement movement;
    PlayerAuraEffect aura;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<Movement>();
        aura = GetComponent<PlayerAuraEffect>() ?? gameObject.AddComponent<PlayerAuraEffect>();

        if (maxRange <= 0f) maxRange = distance > 0f ? distance : 7.5f;
    }

    public override void OnChargeStart(Vector2 pointerWorld)
    {
        if (!IsOwner || !IsReady || !CanAfford || IsLeaping) return;

        IsCharging = true;
        chargeStartTime = Time.time;
        ChargeRatio = 0f;

        if (aura != null) aura.SetCharging(true, 0f);

        if (indicator == null)
            indicator = LeapIndicator.Create(body != null ? body.sortingOrder + 10 : 35);

        UpdateAimAndIndicator(pointerWorld);
        indicator.Show();
    }

    public override void OnChargeUpdate(Vector2 pointerWorld)
    {
        if (!IsOwner || !IsCharging) return;

        float elapsed = Time.time - chargeStartTime;
        ChargeRatio = Mathf.Clamp01(elapsed / maxChargeTime);

        if (aura != null) aura.SetCharging(true, ChargeRatio);

        UpdateAimAndIndicator(pointerWorld);

        // Auto-release if held beyond maximum allowed hold time
        if (elapsed >= maxHoldSeconds)
        {
            OnChargeRelease(pointerWorld);
        }
    }

    public override void OnChargeRelease(Vector2 pointerWorld)
    {
        if (!IsOwner || !IsCharging) return;

        IsCharging = false;
        if (aura != null) aura.SetCharging(false, 0f);
        if (indicator != null) indicator.Hide();

        if (!IsReady || !CanAfford || IsLeaping) return;

        float finalCharge = ChargeRatio;
        Vector2 targetPos = ClampTarget(pointerWorld);

        CommitCooldown();
        ExecuteLeapServerRpc(targetPos, finalCharge);

        // Responsive client-side prediction: start travel immediately on owner
        if (!IsLeaping)
        {
            StartCoroutine(TravelRoutine(targetPos, finalCharge));
        }
    }

    public override void OnChargeCancel()
    {
        if (!IsCharging) return;
        IsCharging = false;
        ChargeRatio = 0f;
        if (aura != null) aura.SetCharging(false, 0f);
        if (indicator != null) indicator.Hide();
    }

    public Vector2 ClampTarget(Vector2 pointerWorld)
    {
        Vector2 origin = transform.position;
        Vector2 delta = pointerWorld - origin;
        float dist = delta.magnitude;

        if (dist < minRange) dist = minRange;
        if (dist > maxRange) dist = maxRange;

        Vector2 defaultDir = movement != null ? movement.LastMoveDirection : Vector2.right;
        Vector2 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : defaultDir;
        return origin + dir * dist;
    }

    void UpdateAimAndIndicator(Vector2 pointerWorld)
    {
        Vector2 targetPos = ClampTarget(pointerWorld);
        if (indicator != null)
        {
            float currentRadius = impactRadius * Mathf.Lerp(1f, maxChargeRadiusMultiplier, ChargeRatio);
            indicator.UpdateVisual(transform.position, targetPos, currentRadius, ChargeRatio,
                                   ChargeRatio >= 1f, indicatorColor, indicatorReadyColor);
        }
    }

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        // Fallback if triggered directly via TryActivate rather than hold/release
        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        Vector2 target = (Vector2)transform.position + dir * maxRange;
        StartCoroutine(ServerRoutine(target, 0f));
    }

    [Rpc(SendTo.Server)]
    void ExecuteLeapServerRpc(Vector2 targetPos, float charge)
    {
        if (powerCost > 0f)
        {
            if (meter == null || !meter.ServerTrySpend(powerCost)) return;
        }

        StartCoroutine(ServerRoutine(targetPos, charge));
        PlayEffectsRpc(targetPos - (Vector2)transform.position);
    }

    IEnumerator ServerRoutine(Vector2 targetPos, float charge)
    {
        LeapOwnerRpc(targetPos, charge);

        // Wait out the travel arc
        AbilityLoadout traveler = GetComponent<AbilityLoadout>();
        float travelRemaining = airSeconds;
        while (travelRemaining > 0f)
        {
            if (!TimeStopWorld.IsFrozenPlayer(traveler)) travelRemaining -= Time.deltaTime;
            yield return null;
        }

        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

        float slamRadius = impactRadius * Mathf.Lerp(1f, maxChargeRadiusMultiplier, charge);
        int slamDamage = Mathf.RoundToInt(damage * Mathf.Lerp(1f, maxChargeDamageMultiplier, charge));
        float slamKnockback = knockback * Mathf.Lerp(1f, 1.4f, charge);

        Vector2 centre = (Vector2)transform.position;
        if (Vector2.Distance(centre, targetPos) > 1.5f)
        {
            centre = targetPos;
        }

        Collider2D[] found = Physics2D.OverlapCircleAll(centre, slamRadius, hitLayers);

        foreach (Collider2D col in found)
        {
            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.gameObject == gameObject) continue;

            bool isPlayer = target.GetComponent<AbilityLoadout>() != null;
            if (!friendlyFire && isPlayer) continue;

            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            Vector2 away = (Vector2)target.transform.position - centre;
            Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;

            target.ServerTakeDamage(ScaleDamage(slamDamage), OwnerClientId, dir);

            var receiver = target.GetComponent<KnockbackReceiver>();
            if (receiver != null && slamKnockback > 0f) receiver.ServerApply(dir, slamKnockback);
        }

        int cracks = Mathf.RoundToInt(crackCount * Mathf.Lerp(1f, 1.6f, charge));
        float shake = landShake * Mathf.Lerp(1f, 1.6f, charge);
        LandedRpc(centre, slamRadius, cracks, shake);
    }

    /// Only the owner moves - anyone else would fight NetworkTransform.
    [Rpc(SendTo.Owner)]
    void LeapOwnerRpc(Vector2 targetPos, float charge)
    {
        if (!IsLeaping)
        {
            StartCoroutine(TravelRoutine(targetPos, charge));
        }
    }

    IEnumerator TravelRoutine(Vector2 targetPos, float charge)
    {
        AbilityLoadout traveler = GetComponent<AbilityLoadout>();
        Vector3 start = transform.position;
        Vector3 end = new Vector3(targetPos.x, targetPos.y, start.z);

        IsLeaping = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Explosive Takeoff Sonic Boom & sparks at launch position
        SpawnTakeoffVFX(start, charge);
        CameraFollow.Shake(0.18f + 0.15f * charge);

        bool hasSeparateBody = body != null && body.transform != transform;
        Vector3 spriteRest = hasSeparateBody ? body.transform.localPosition : Vector3.zero;
        float elapsed = 0f;
        float afterimageTimer = 0f;

        while (elapsed < airSeconds)
        {
            if (TimeStopWorld.IsFrozenPlayer(traveler))
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                yield return null;
                continue;
            }
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / airSeconds);

            Vector3 groundPos = Vector3.Lerp(start, end, k);
            float lift = Mathf.Sin(k * Mathf.PI) * arcHeight;

            if (hasSeparateBody)
            {
                transform.position = groundPos;
                if (rb != null)
                {
                    rb.position = (Vector2)groundPos;
                    rb.linearVelocity = Vector2.zero;
                }
                body.transform.localPosition = spriteRest + new Vector3(0f, lift, 0f);
            }
            else
            {
                Vector3 airPos = groundPos + new Vector3(0f, lift, 0f);
                transform.position = airPos;
                if (rb != null)
                {
                    rb.position = (Vector2)airPos;
                    rb.linearVelocity = Vector2.zero;
                }
            }

            // High-octane anime ghost afterimages during leap
            afterimageTimer += Time.deltaTime;
            if (afterimageTimer >= 0.035f)
            {
                afterimageTimer = 0f;
                Color ghostCol = Color.Lerp(new Color(1f, 0.75f, 0.2f, 0.8f), new Color(1f, 0.25f, 0.05f, 0.9f), charge);
                if (aura != null) aura.SpawnAfterimage(ghostCol, 0.28f);
            }

            yield return null;
        }

        transform.position = end;
        if (rb != null)
        {
            rb.position = (Vector2)end;
            rb.linearVelocity = Vector2.zero;
        }
        if (hasSeparateBody) body.transform.localPosition = spriteRest;

        IsLeaping = false;
    }

    void SpawnTakeoffVFX(Vector3 at, float charge)
    {
        // 1. Takeoff sonic boom ring
        GameObject ring = new GameObject("TakeoffBoom");
        ring.transform.position = at;

        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        sr.color = Color.white;
        sr.sortingOrder = body != null ? body.sortingOrder + 2 : 35;

        StartCoroutine(AnimateTakeoffBoom(ring, sr, charge));

        // 2. Launch upward sparks
        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();
        int count = Mathf.RoundToInt(Mathf.Lerp(6, 12, charge));
        for (int i = 0; i < count; i++)
        {
            GameObject sp = new GameObject("TakeoffSpark");
            sp.transform.position = at + (Vector3)(Random.insideUnitCircle * 0.2f);
            var spSr = sp.AddComponent<SpriteRenderer>();
            spSr.sprite = sparkSprite;
            spSr.color = Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.1f), Random.value);
            spSr.sortingOrder = body != null ? body.sortingOrder + 3 : 36;
            StartCoroutine(AnimateTakeoffSpark(sp));
        }
    }

    IEnumerator AnimateTakeoffBoom(GameObject ring, SpriteRenderer sr, float charge)
    {
        float dur = 0.25f;
        float elapsed = 0f;
        float maxScale = Mathf.Lerp(2.2f, 3.8f, charge);

        while (elapsed < dur)
        {
            if (ring == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float s = Mathf.Lerp(0.3f, maxScale, Mathf.Pow(t, 0.6f));
            ring.transform.localScale = new Vector3(s, s * 0.5f, 1f);
            sr.color = new Color(1f, 0.7f, 0.2f, (1f - t) * 0.9f);
            yield return null;
        }

        if (ring != null) Destroy(ring);
    }

    IEnumerator AnimateTakeoffSpark(GameObject sp)
    {
        float ang = Random.Range(45f, 135f) * Mathf.Deg2Rad;
        float spd = Random.Range(4f, 8f);
        Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
        float dur = Random.Range(0.2f, 0.38f);
        float elapsed = 0f;
        var sr = sp.GetComponent<SpriteRenderer>();

        while (elapsed < dur)
        {
            if (sp == null) yield break;
            elapsed += Time.deltaTime;
            vel.y -= 15f * Time.deltaTime;
            sp.transform.position += (Vector3)(vel * Time.deltaTime);

            float t = elapsed / dur;
            sp.transform.localScale = Vector3.one * (0.35f * (1f - t));
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
            yield return null;
        }

        if (sp != null) Destroy(sp);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void LandedRpc(Vector3 at, float radius, int cracks, float shake)
    {
        // 1. SLAM ANIME IMPACT FRAME
        ImpactFrameEffect.Trigger(ImpactFrameEffect.Style.ExecutionerSlam, 0.22f, shake * 1.6f, true);

        // 2. GRAND SLAM CRATER & FIERY SHOCKWAVE ERUPTIONS
        SlamImpactEffect.Spawn(at, radius, body != null ? body.sortingOrder + 5 : 40);

        if (impactPrefab != null)
        {
            GameObject fx = Instantiate(impactPrefab, at, Quaternion.identity);
            float scale = impactRadius > 0f ? (radius / impactRadius) : 1f;
            fx.transform.localScale = Vector3.one * (scale * 1.4f);
        }

        ShockwaveEffect.Spawn(at, radius, cracks,
                              body != null ? body.sortingOrder + 5 : 40);
    }

    void OnDisable()
    {
        OnChargeCancel();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (indicator != null)
        {
            indicator.Dismiss();
            indicator = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, maxRange);
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, impactRadius);
    }
}
