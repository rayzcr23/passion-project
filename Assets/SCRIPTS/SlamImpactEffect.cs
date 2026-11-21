using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Grand slam impact effect for Executioner's Leap landing.
/// Spawns fiery blast waves, sonic boom shockwave rings, erupting jagged stone spires,
/// molten fracture cracks, and a geyser of sparks and debris.
public class SlamImpactEffect : MonoBehaviour
{
    public float radius = 2.8f;
    public Color fireColor = new Color(1f, 0.45f, 0.1f);
    public Color hotCoreColor = new Color(1f, 0.95f, 0.7f);
    public int sortingOrder = 35;

    public static SlamImpactEffect Spawn(Vector3 at, float radius, int order = 35)
    {
        var go = new GameObject("SlamImpact");
        go.transform.position = at;

        var slam = go.AddComponent<SlamImpactEffect>();
        slam.radius = radius;
        slam.sortingOrder = order;
        return slam;
    }

    public static SlamImpactEffect Spawn(Vector3 at, float radius, int order, Color fireColor, Color coreColor)
    {
        var go = new GameObject("SlamImpact");
        go.transform.position = at;

        var slam = go.AddComponent<SlamImpactEffect>();
        slam.radius = radius;
        slam.sortingOrder = order;
        slam.fireColor = fireColor;
        slam.hotCoreColor = coreColor;
        return slam;
    }

    void Start()
    {
        StartCoroutine(BlastWaveRoutine());
        StartCoroutine(SonicBoomRoutine());
        StartCoroutine(RockSpiresRoutine());
        StartCoroutine(MoltenCracksRoutine());
        StartCoroutine(EruptionSparksRoutine());

        Destroy(gameObject, 2.2f);
    }

    IEnumerator BlastWaveRoutine()
    {
        // 1. Fast hot core ring
        GameObject innerRing = new GameObject("InnerHotRing");
        innerRing.transform.SetParent(transform, false);
        var innerSr = innerRing.AddComponent<SpriteRenderer>();
        innerSr.sprite = AuraVFXLibrary.GetGlowDisc();
        innerSr.color = hotCoreColor;
        innerSr.sortingOrder = sortingOrder + 3;

        // 2. Heavy outer fire blast
        GameObject outerRing = new GameObject("OuterFireRing");
        outerRing.transform.SetParent(transform, false);
        var outerSr = outerRing.AddComponent<SpriteRenderer>();
        outerSr.sprite = AuraVFXLibrary.GetGlowDisc();
        outerSr.color = fireColor;
        outerSr.sortingOrder = sortingOrder + 1;

        float duration = 0.38f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float innerSize = Mathf.Lerp(0.8f, radius * 2.0f, Mathf.Pow(t, 0.65f));
            innerRing.transform.localScale = new Vector3(innerSize, innerSize * 0.5f, 1f);
            innerSr.color = new Color(hotCoreColor.r, hotCoreColor.g, hotCoreColor.b, (1f - t) * 0.95f);

            float outerSize = Mathf.Lerp(1.2f, radius * 3.2f, Mathf.Pow(t, 0.75f));
            outerRing.transform.localScale = new Vector3(outerSize, outerSize * 0.5f, 1f);
            outerSr.color = new Color(fireColor.r, fireColor.g, fireColor.b, (1f - t) * 0.75f);

            yield return null;
        }

        Destroy(innerRing);
        Destroy(outerRing);
    }

    IEnumerator SonicBoomRoutine()
    {
        GameObject ring = new GameObject("SlamSonicBoom");
        ring.transform.SetParent(transform, false);

        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder + 4;

        float duration = 0.32f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float size = Mathf.Lerp(0.5f, radius * 3.4f, Mathf.Pow(t, 0.55f));
            ring.transform.localScale = new Vector3(size, size * 0.55f, 1f);
            sr.color = Color.Lerp(Color.white, fireColor, t);
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.95f);

            yield return null;
        }

        Destroy(ring);
    }

    /// Erupts jagged stone spires from beneath the earth encircling the crater rim!
    IEnumerator RockSpiresRoutine()
    {
        Sprite spireSprite = AuraVFXLibrary.GetRockSpike();
        int spireCount = 8;
        float rimRadius = radius * 0.85f;

        for (int i = 0; i < spireCount; i++)
        {
            float angleDeg = (360f / spireCount) * i + Random.Range(-12f, 12f);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * rimRadius, Mathf.Sin(rad) * rimRadius * 0.6f, 0f);

            GameObject spire = new GameObject("CraterRockSpire_" + i);
            spire.transform.position = transform.position + offset;

            // Tilt outward from center
            float tilt = (offset.x > 0f ? -1f : 1f) * Random.Range(10f, 22f);
            spire.transform.rotation = Quaternion.Euler(0f, 0f, tilt);

            var sr = spire.AddComponent<SpriteRenderer>();
            sr.sprite = spireSprite;
            sr.sortingOrder = sortingOrder + (offset.y < 0 ? 5 : 2); // Depth sort correctly
            sr.color = Color.white;

            StartCoroutine(AnimateSpire(spire, Random.Range(1.1f, 1.6f)));
        }

        yield return null;
    }

    IEnumerator AnimateSpire(GameObject spire, float targetScale)
    {
        // Phase 1: Rapid violent upward eruption (0.07s)
        float eruptTime = 0.08f;
        float elapsed = 0f;

        while (elapsed < eruptTime)
        {
            if (spire == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / eruptTime;
            float sY = Mathf.Lerp(0f, targetScale, Mathf.Pow(t, 0.5f));
            float sX = Mathf.Lerp(targetScale * 0.5f, targetScale, t);
            spire.transform.localScale = new Vector3(sX, sY, 1f);
            yield return null;
        }

        // Phase 2: Stand firm glowing hot (0.85s)
        yield return new WaitForSeconds(0.85f);

        // Phase 3: Crumble / sink back into ground
        float crumbleTime = 0.35f;
        elapsed = 0f;
        var sr = spire.GetComponent<SpriteRenderer>();

        while (elapsed < crumbleTime)
        {
            if (spire == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / crumbleTime;

            float sY = Mathf.Lerp(targetScale, 0f, t * t);
            spire.transform.localScale = new Vector3(targetScale * (1f - t * 0.3f), sY, 1f);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t);

            yield return null;
        }

        if (spire != null) Destroy(spire);
    }

    IEnumerator MoltenCracksRoutine()
    {
        int count = 10;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-15f, 15f);
            float length = radius * Random.Range(0.7f, 1.3f);

            GameObject crack = new GameObject("MoltenCrack");
            crack.transform.SetParent(transform, false);

            crack.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            crack.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * new Vector3(length * 0.5f, 0f, 0f);
            crack.transform.localScale = new Vector3(length, Random.Range(0.08f, 0.16f), 1f);

            var sr = crack.AddComponent<SpriteRenderer>();
            sr.sprite = AuraVFXLibrary.GetGlowDisc();
            sr.color = hotCoreColor;
            sr.sortingOrder = sortingOrder - 1;

            StartCoroutine(FadeCrackRoutine(sr));
        }

        yield return null;
    }

    IEnumerator FadeCrackRoutine(SpriteRenderer sr)
    {
        float hotTime = 0.22f;
        float coolTime = 0.85f;
        float elapsed = 0f;

        // Hot molten stage
        while (elapsed < hotTime)
        {
            if (sr == null) yield break;
            elapsed += Time.deltaTime;
            sr.color = Color.Lerp(hotCoreColor, fireColor, elapsed / hotTime);
            yield return null;
        }

        // Cool down to charred dark stone then fade
        elapsed = 0f;
        Color charred = new Color(0.2f, 0.12f, 0.08f);
        while (elapsed < coolTime)
        {
            if (sr == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / coolTime;
            sr.color = Color.Lerp(fireColor, charred, t * 1.5f);
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.8f);
            yield return null;
        }

        if (sr != null) Destroy(sr.gameObject);
    }

    IEnumerator EruptionSparksRoutine()
    {
        int count = 18;
        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();

        for (int i = 0; i < count; i++)
        {
            GameObject spark = new GameObject("EruptionSpark");
            spark.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.4f);

            var sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = sparkSprite;
            sr.color = Color.Lerp(hotCoreColor, fireColor, Random.value);
            sr.sortingOrder = sortingOrder + 6;

            StartCoroutine(AnimateSpark(spark));
        }

        yield return null;
    }

    IEnumerator AnimateSpark(GameObject spark)
    {
        float angle = Random.Range(30f, 150f) * Mathf.Deg2Rad;
        float speed = Random.Range(6.5f, 13f);
        Vector2 velocity = new Vector2(Mathf.Cos(angle) * (Random.value > 0.5f ? 1f : -1f), Mathf.Sin(angle)) * speed;
        float gravity = 18f;
        float life = Random.Range(0.35f, 0.65f);
        float elapsed = 0f;
        float size = Random.Range(0.35f, 0.65f);

        var sr = spark.GetComponent<SpriteRenderer>();

        while (elapsed < life)
        {
            if (spark == null) yield break;

            elapsed += Time.deltaTime;
            velocity.y -= gravity * Time.deltaTime;
            spark.transform.position += (Vector3)(velocity * Time.deltaTime);

            float t = elapsed / life;
            spark.transform.localScale = Vector3.one * (size * (1f - t * 0.5f));
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);

            yield return null;
        }

        if (spark != null) Destroy(spark);
    }
}

