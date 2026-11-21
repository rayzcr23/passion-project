using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Multi-layered anime aura controller that wraps the character.
/// Provides a pulsing silhouette halo, rising ki particles, electric arcs,
/// ground suction rings, and afterimage ghost trails.
public class PlayerAuraEffect : MonoBehaviour
{
    SpriteRenderer body;
    SpriteRenderer auraBacking;
    GameObject auraHaloObj;
    GameObject groundRingObj;
    SpriteRenderer groundRingSr;

    public Color baseAuraColor = new Color(1f, 0.75f, 0.2f); // Golden Amber
    public Color maxAuraColor = new Color(1f, 0.2f, 0.05f);  // Blazing Crimson Sunfire

    bool isCharging;
    float currentCharge;
    bool hasPoppedMax;

    readonly List<GameObject> activeSparks = new List<GameObject>();
    float moteSpawnTimer;
    float arcSpawnTimer;

    void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();

        BuildAuraObjects();
    }

    void BuildAuraObjects()
    {
        // 1. Aura Backing Halo (rendered behind the body with slightly larger scale and pulsating glow)
        auraHaloObj = new GameObject("AuraHalo");
        auraHaloObj.transform.SetParent(transform, false);
        auraHaloObj.transform.localPosition = Vector3.zero;

        auraBacking = auraHaloObj.AddComponent<SpriteRenderer>();
        auraBacking.sortingOrder = (body != null ? body.sortingOrder : 10) - 1;
        auraBacking.color = new Color(baseAuraColor.r, baseAuraColor.g, baseAuraColor.b, 0f);

        // 2. Ground Energy Gathering Disc
        groundRingObj = new GameObject("AuraGroundRing");
        groundRingObj.transform.SetParent(transform, false);
        groundRingObj.transform.localPosition = new Vector3(0f, -0.35f, 0f);

        groundRingSr = groundRingObj.AddComponent<SpriteRenderer>();
        groundRingSr.sprite = AuraVFXLibrary.GetGlowDisc();
        groundRingSr.sortingOrder = (body != null ? body.sortingOrder : 10) - 2;
        groundRingSr.color = new Color(baseAuraColor.r, baseAuraColor.g, baseAuraColor.b, 0f);
        groundRingObj.transform.localScale = new Vector3(1.8f, 0.8f, 1f);

        auraHaloObj.SetActive(false);
        groundRingObj.SetActive(false);
    }

    /// Sets whether the character is actively charging an ability and the charge percentage [0..1].
    public void SetCharging(bool charging, float chargeRatio)
    {
        isCharging = charging;
        currentCharge = Mathf.Clamp01(chargeRatio);

        if (!charging)
        {
            hasPoppedMax = false;
            if (auraHaloObj != null) auraHaloObj.SetActive(false);
            if (groundRingObj != null) groundRingObj.SetActive(false);
            return;
        }

        if (auraHaloObj != null && !auraHaloObj.activeSelf) auraHaloObj.SetActive(true);
        if (groundRingObj != null && !groundRingObj.activeSelf) groundRingObj.SetActive(true);

        // Pop max charge visual burst when hitting 100%
        if (currentCharge >= 0.99f && !hasPoppedMax)
        {
            hasPoppedMax = true;
            TriggerMaxChargePop();
        }
    }

    public void SetAuraColors(Color baseCol, Color maxCol)
    {
        baseAuraColor = baseCol;
        maxAuraColor = maxCol;
    }

    public void ResetAuraColors()
    {
        baseAuraColor = new Color(1f, 0.75f, 0.2f);
        maxAuraColor = new Color(1f, 0.2f, 0.05f);
    }

    void Update()
    {
        if (!isCharging) return;

        Color currentCol = Color.Lerp(baseAuraColor, maxAuraColor, currentCharge);

        // Mirror body sprite, flip, and offset
        if (body != null && auraBacking != null)
        {
            auraBacking.sprite = body.sprite;
            auraBacking.flipX = body.flipX;

            float pulse = 1.15f + Mathf.Sin(Time.time * (12f + currentCharge * 18f)) * (0.08f + currentCharge * 0.12f);
            auraHaloObj.transform.localScale = Vector3.one * pulse;

            float alpha = Mathf.Lerp(0.45f, 0.95f, currentCharge);
            auraBacking.color = new Color(currentCol.r, currentCol.g, currentCol.b, alpha);
        }

        // Ground Gathering Ring pulse
        if (groundRingSr != null)
        {
            float ringPulse = 1.6f + Mathf.Sin(Time.time * 16f) * 0.3f * (1f - currentCharge * 0.4f);
            groundRingObj.transform.localScale = new Vector3(ringPulse, ringPulse * 0.45f, 1f);
            groundRingSr.color = new Color(currentCol.r, currentCol.g, currentCol.b, Mathf.Lerp(0.35f, 0.85f, currentCharge));
        }

        // Spawn Rising Ki / Flame Particles
        moteSpawnTimer += Time.deltaTime;
        float moteInterval = Mathf.Lerp(0.08f, 0.025f, currentCharge);
        if (moteSpawnTimer >= moteInterval)
        {
            moteSpawnTimer = 0f;
            SpawnKiMote(currentCol);
        }

        // Spawn Electric Lightning Arcs at high charge
        if (currentCharge > 0.45f)
        {
            arcSpawnTimer += Time.deltaTime;
            float arcInterval = Mathf.Lerp(0.18f, 0.04f, (currentCharge - 0.45f) / 0.55f);
            if (arcSpawnTimer >= arcInterval)
            {
                arcSpawnTimer = 0f;
                SpawnElectricArc(currentCol);
            }
        }
    }

    void SpawnKiMote(Color col)
    {
        Vector2 offset = Random.insideUnitCircle * 0.65f;
        Vector3 spawnPos = transform.position + new Vector3(offset.x, -0.2f + Random.Range(0f, 0.25f), 0f);

        GameObject mote = new GameObject("KiMote");
        mote.transform.position = spawnPos;

        var sr = mote.AddComponent<SpriteRenderer>();
        sr.sprite = Random.value > 0.4f ? AuraVFXLibrary.GetSparkStar() : AuraVFXLibrary.GetGlowDisc();
        sr.color = col;
        sr.sortingOrder = (body != null ? body.sortingOrder : 10) + 1;

        StartCoroutine(AnimateKiMote(mote, col));
    }

    IEnumerator AnimateKiMote(GameObject mote, Color col)
    {
        float life = Random.Range(0.35f, 0.65f);
        float elapsed = 0f;
        float riseSpeed = Random.Range(2.2f, 4.2f);
        float maxScale = Random.Range(0.25f, 0.55f);
        Vector3 origin = mote.transform.position;
        float wobbleFreq = Random.Range(8f, 16f);

        var sr = mote.GetComponent<SpriteRenderer>();

        while (elapsed < life)
        {
            if (mote == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / life;

            float y = origin.y + elapsed * riseSpeed;
            float x = origin.x + Mathf.Sin(elapsed * wobbleFreq) * 0.12f;
            mote.transform.position = new Vector3(x, y, origin.z);

            float scale = Mathf.Sin(t * Mathf.PI) * maxScale;
            mote.transform.localScale = Vector3.one * scale;

            sr.color = new Color(col.r, col.g, col.b, 1f - t);

            yield return null;
        }

        if (mote != null) Destroy(mote);
    }

    void SpawnElectricArc(Color col)
    {
        Vector3 offset = (Vector3)(Random.insideUnitCircle * 0.75f) + new Vector3(0f, 0.35f, 0f);
        GameObject arc = new GameObject("LightningArc");
        arc.transform.position = transform.position + offset;
        arc.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        arc.transform.localScale = new Vector3(Random.Range(0.3f, 0.7f), Random.Range(0.04f, 0.08f), 1f);

        var sr = arc.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetGlowDisc();
        sr.color = Color.white; // blinding white-hot lightning core
        sr.sortingOrder = (body != null ? body.sortingOrder : 10) + 2;

        Destroy(arc, 0.045f);
    }

    void TriggerMaxChargePop()
    {
        CameraFollow.Shake(0.35f);

        // Flash Ring on hitting 100%
        GameObject ring = new GameObject("MaxChargePopRing");
        ring.transform.position = transform.position;

        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetGlowDisc();
        sr.color = new Color(1f, 0.95f, 0.8f, 1f);
        sr.sortingOrder = (body != null ? body.sortingOrder : 10) + 3;

        StartCoroutine(PopRingRoutine(ring));
    }

    IEnumerator PopRingRoutine(GameObject ring)
    {
        float elapsed = 0f;
        float duration = 0.22f;
        var sr = ring.GetComponent<SpriteRenderer>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float size = Mathf.Lerp(0.5f, 3.8f, t);
            ring.transform.localScale = new Vector3(size, size * 0.6f, 1f);
            sr.color = new Color(1f, 0.85f, 0.3f, 1f - t);
            yield return null;
        }

        Destroy(ring);
    }

    /// Spawns a glowing ghost afterimage of the player's current sprite that dissolves over time.
    public void SpawnAfterimage(Color tint, float duration = 0.28f)
    {
        if (body == null || body.sprite == null) return;

        GameObject ghost = new GameObject("GhostAfterimage");
        ghost.transform.position = body.transform.position;
        ghost.transform.localScale = body.transform.lossyScale;

        var sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = body.sprite;
        sr.flipX = body.flipX;
        sr.sortingOrder = body.sortingOrder - 1;
        sr.color = tint;

        StartCoroutine(FadeGhostRoutine(ghost, tint, duration));
    }

    IEnumerator FadeGhostRoutine(GameObject ghost, Color tint, float duration)
    {
        float elapsed = 0f;
        var sr = ghost.GetComponent<SpriteRenderer>();

        while (elapsed < duration)
        {
            if (ghost == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            sr.color = new Color(tint.r, tint.g, tint.b, (1f - t) * tint.a);
            ghost.transform.localScale *= (1f + Time.deltaTime * 0.4f); // slight expansion as it dissolves

            yield return null;
        }

        if (ghost != null) Destroy(ghost);
    }

    void OnDisable()
    {
        SetCharging(false, 0f);
    }
}
