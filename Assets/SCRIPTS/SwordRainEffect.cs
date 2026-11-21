using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// High-octane anime visual effect for Rain of Swords.
/// Features opening and finisher Impact Frames, celestial summoning arrays,
/// dimensional warp gates, blazing comet-tailed blade falls, embedded blade resonance,
/// and a climax Colossal Divine Sword of Judgement drop.
public class SwordRainEffect : MonoBehaviour
{
    public Sprite bladeSprite;
    public GameObject impactPrefab;

    public float radius = 5.5f;
    public int swordCount = 34;
    public float duration = 2.6f;
    public int sortingOrder = 40;

    [Header("Fall")]
    public float fallHeight = 8.5f;
    public float fallSeconds = 0.26f;
    public float bladeScale = 1.0f;

    [Header("Aura")]
    public Color auraColour = new Color(1f, 0.88f, 0.45f);
    public Color hotCoreColour = new Color(1f, 0.98f, 0.85f);
    public float auraSeconds = 0.55f;

    readonly List<GameObject> spawned = new List<GameObject>();

    public static SwordRainEffect Spawn(Vector3 at, Sprite blade, GameObject impact,
                                        float radius, int count, float duration, int order)
    {
        var go = new GameObject("SwordRain");
        go.transform.position = at;

        var fx = go.AddComponent<SwordRainEffect>();
        fx.bladeSprite = blade;
        fx.impactPrefab = impact;
        fx.radius = radius;
        fx.swordCount = count;
        fx.duration = duration;
        fx.sortingOrder = order;
        return fx;
    }

    void Start()
    {
        // Always ensure we use the radiant holy Excalibur sprite
        if (bladeSprite == null || bladeSprite.name.Contains("Arrow"))
        {
            bladeSprite = AuraVFXLibrary.GetHolySword();
        }

        // 1. OPENING ANIME IMPACT FRAME
        ImpactFrameEffect.Trigger(ImpactFrameEffect.Style.SuperRainOfSwords, 0.26f, 1.0f, true);

        // 2. CELESTIAL RUNIC SUMMONING ARRAY
        SummoningCircleEffect.Spawn(transform.position, radius, duration + 1.2f, sortingOrder - 10);

        StartCoroutine(AuraRoutine());
        StartCoroutine(RainRoutine());
    }

    /// Expanding golden divine shockwaves cast from the center.
    IEnumerator AuraRoutine()
    {
        Sprite glow = AuraVFXLibrary.GetGlowDisc();

        for (int pulse = 0; pulse < 3; pulse++)
        {
            GameObject ring = new GameObject("SuperAuraRing");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.zero;

            var sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = glow;
            sr.sortingOrder = sortingOrder - 2;

            StartCoroutine(AnimateAuraPulse(ring, sr, pulse));
            yield return new WaitForSeconds(0.16f);
        }
    }

    IEnumerator AnimateAuraPulse(GameObject ring, SpriteRenderer sr, int pulseIndex)
    {
        float elapsed = 0f;
        float pTime = auraSeconds + pulseIndex * 0.08f;

        while (elapsed < pTime)
        {
            if (ring == null) yield break;
            elapsed += Time.deltaTime;
            float k = elapsed / pTime;

            float size = Mathf.Lerp(0.5f, radius * 2.5f, Mathf.Pow(k, 0.75f));
            ring.transform.localScale = new Vector3(size, size * 0.55f, 1f);

            sr.color = new Color(auraColour.r, auraColour.g, auraColour.b, (1f - k) * 0.8f);
            yield return null;
        }

        if (ring != null) Destroy(ring);
    }

    IEnumerator RainRoutine()
    {
        float gap = duration / Mathf.Max(1, swordCount);

        for (int i = 0; i < swordCount; i++)
        {
            float spread = Mathf.Lerp(0.2f, 1f, i / (float)swordCount);
            Vector2 offset = Random.insideUnitCircle.normalized *
                             Random.Range(0.1f, radius * spread);

            Vector3 landPos = transform.position + new Vector3(offset.x, offset.y * 0.75f, 0f);
            StartCoroutine(OneSword(landPos, false));

            yield return new WaitForSeconds(gap);
        }

        // --- CLIMAX: COLOSSAL JUDGMENT FINISHER ---
        yield return StartCoroutine(ColossalJudgmentFinisher());

        yield return new WaitForSeconds(1.2f);
        Destroy(gameObject);
    }

    IEnumerator OneSword(Vector3 landAt, bool isFinisher)
    {
        // 1. Ground Runic Warning Reticle
        GameObject reticle = new GameObject("SwordReticle");
        reticle.transform.position = landAt;

        var reticleSr = reticle.AddComponent<SpriteRenderer>();
        reticleSr.sprite = AuraVFXLibrary.GetGlowDisc();
        reticleSr.sortingOrder = sortingOrder - 3;
        reticleSr.color = new Color(auraColour.r, auraColour.g, auraColour.b, 0.6f);

        // 2. Sky Dimensional Gate (Golden Warp Rift)
        Vector3 riftPos = landAt + new Vector3(0f, fallHeight, 0f);
        StartCoroutine(SpawnDimensionalGate(riftPos));

        // 3. Falling Blade with blazing comet streak
        GameObject blade = new GameObject("Blade");
        blade.transform.position = riftPos;
        blade.transform.localScale = Vector3.one * (bladeScale * (isFinisher ? 1.5f : 1f));

        float tilt = Random.Range(-12f, 12f);
        // Holy sword sprite points upward by default, so 180 points downward
        blade.transform.rotation = Quaternion.Euler(0f, 0f, 180f + tilt);

        var bladeSr = blade.AddComponent<SpriteRenderer>();
        bladeSr.sprite = bladeSprite != null ? bladeSprite : AuraVFXLibrary.GetHolySword();
        bladeSr.sortingOrder = sortingOrder + 4;
        bladeSr.color = isFinisher ? Color.white : new Color(1f, 0.98f, 0.88f);

        // 4. Comet Energy Streak attached behind the falling sword
        GameObject streak = new GameObject("CometStreak");
        streak.transform.SetParent(blade.transform, false);
        streak.transform.localPosition = new Vector3(0f, 0.5f, 0f); // trails behind hilt/guard
        streak.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        streak.transform.localScale = new Vector3(1.1f, 2.8f, 1f);

        var streakSr = streak.AddComponent<SpriteRenderer>();
        streakSr.sprite = AuraVFXLibrary.GetCometStreak();
        streakSr.color = new Color(auraColour.r, auraColour.g, auraColour.b, 0.9f);
        streakSr.sortingOrder = sortingOrder + 2;

        spawned.Add(reticle);
        spawned.Add(blade);

        float elapsed = 0f;
        while (elapsed < fallSeconds)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / fallSeconds;

            // Exponential acceleration reads as titanic weight
            blade.transform.position = Vector3.Lerp(riftPos, landAt, k * k);

            float rSize = Mathf.Lerp(1.4f, 0.45f, k);
            reticle.transform.localScale = new Vector3(rSize, rSize * 0.45f, 1f);
            reticleSr.color = new Color(auraColour.r, auraColour.g, auraColour.b, Mathf.Lerp(0.3f, 0.95f, k));

            yield return null;
        }

        Destroy(reticle);
        Destroy(streak);

        // --- GROUND IMPACT ---
        if (impactPrefab != null)
        {
            GameObject impact = Instantiate(impactPrefab, landAt, Quaternion.identity);
            impact.transform.localScale = Vector3.one * (0.65f * (isFinisher ? 1.5f : 1f));

            var sr = impact.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = isFinisher ? Color.white : new Color(1f, 0.95f, 0.7f);
        }

        // Mini shockwave ring on blade impact
        StartCoroutine(MiniShockwave(landAt, isFinisher ? 2.5f : 1.4f));

        // Flying holy spark motes
        StartCoroutine(ImpactSparks(landAt, isFinisher ? 8 : 4));

        CameraFollow.Shake(isFinisher ? 0.35f : 0.08f);

        // --- EMBEDDED BLADE RESONANCE ---
        // Blade sticks into ground vibrating with holy light before dissipating!
        blade.transform.position = landAt + new Vector3(0f, 0.25f, 0f);
        StartCoroutine(EmbeddedBladeRoutine(blade, bladeSr, tilt));
    }

    IEnumerator SpawnDimensionalGate(Vector3 pos)
    {
        GameObject gate = new GameObject("DimensionalGate");
        gate.transform.position = pos;

        var sr = gate.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetDimensionalRift();
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder + 6;

        float duration = 0.45f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (gate == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scaleX = Mathf.Sin(t * Mathf.PI) * 1.5f;
            float scaleY = Mathf.Sin(t * Mathf.PI) * 0.9f;
            gate.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            sr.color = Color.Lerp(Color.white, auraColour, t);
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Sin(t * Mathf.PI));

            yield return null;
        }

        Destroy(gate);
    }

    IEnumerator EmbeddedBladeRoutine(GameObject blade, SpriteRenderer bladeSr, float baseTilt)
    {
        float embedTime = 0.45f;
        float elapsed = 0f;

        while (elapsed < embedTime)
        {
            if (blade == null) yield break;
            elapsed += Time.deltaTime;

            // Intense high-frequency vibration upon piercing earth
            float vib = Mathf.Sin(elapsed * 60f) * Mathf.Lerp(3.5f, 0f, elapsed / embedTime);
            blade.transform.rotation = Quaternion.Euler(0f, 0f, 180f + baseTilt + vib);

            // Dissolve fade out near end
            if (elapsed > embedTime - 0.2f)
            {
                float fade = (embedTime - elapsed) / 0.2f;
                bladeSr.color = new Color(1f, 0.95f, 0.7f, fade);
            }

            yield return null;
        }

        if (blade != null) Destroy(blade);
    }

    /// The Climax: A colossal golden Divine Sword of Judgement drops into the center!
    IEnumerator ColossalJudgmentFinisher()
    {
        Vector3 center = transform.position;

        // 1. Giant celestial gate opening in the clouds
        Vector3 highRift = center + new Vector3(0f, fallHeight + 2f, 0f);
        GameObject titanGate = new GameObject("TitanDimensionalGate");
        titanGate.transform.position = highRift;

        var gateSr = titanGate.AddComponent<SpriteRenderer>();
        gateSr.sprite = AuraVFXLibrary.GetDimensionalRift();
        gateSr.color = Color.white;
        gateSr.sortingOrder = sortingOrder + 12;
        titanGate.transform.localScale = new Vector3(4.5f, 2.2f, 1f);

        // Warning pillar of divine light
        GameObject lightPillar = new GameObject("LightPillar");
        lightPillar.transform.position = center + new Vector3(0f, (fallHeight + 2f) * 0.5f, 0f);
        var pillarSr = lightPillar.AddComponent<SpriteRenderer>();
        pillarSr.sprite = AuraVFXLibrary.GetGlowDisc();
        pillarSr.color = new Color(hotCoreColour.r, hotCoreColour.g, hotCoreColour.b, 0.65f);
        pillarSr.sortingOrder = sortingOrder - 1;
        lightPillar.transform.localScale = new Vector3(1.2f, (fallHeight + 2f) * 1.5f, 1f);

        yield return new WaitForSeconds(0.18f);

        // 2. Colossal Greatsword plunges down
        GameObject titanBlade = new GameObject("ColossalTitanBlade");
        titanBlade.transform.position = highRift;
        titanBlade.transform.localScale = Vector3.one * 3.4f;
        titanBlade.transform.rotation = Quaternion.Euler(0f, 0f, 180f);

        var titanSr = titanBlade.AddComponent<SpriteRenderer>();
        titanSr.sprite = bladeSprite != null ? bladeSprite : AuraVFXLibrary.GetHolySword();
        titanSr.sortingOrder = sortingOrder + 15;
        titanSr.color = Color.white;

        // Massive comet tail behind titan blade
        GameObject titanTail = new GameObject("TitanTail");
        titanTail.transform.SetParent(titanBlade.transform, false);
        titanTail.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        titanTail.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        titanTail.transform.localScale = new Vector3(1.6f, 4.2f, 1f);

        var tailSr = titanTail.AddComponent<SpriteRenderer>();
        tailSr.sprite = AuraVFXLibrary.GetCometStreak();
        tailSr.color = new Color(hotCoreColour.r, hotCoreColour.g, hotCoreColour.b, 0.95f);
        tailSr.sortingOrder = sortingOrder + 14;

        float titanFallDuration = 0.24f;
        float elapsed = 0f;

        while (elapsed < titanFallDuration)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / titanFallDuration;
            titanBlade.transform.position = Vector3.Lerp(highRift, center, k * k);
            yield return null;
        }

        Destroy(titanGate);
        Destroy(lightPillar);

        // --- TITAN IMPACT: HITSTOP IMPACT FRAME & ATOMIC SHOCKWAVE ---
        ImpactFrameEffect.Trigger(ImpactFrameEffect.Style.SuperFinisher, 0.35f, 1.8f, true);
        CameraFollow.Shake(0.7f);

        StartCoroutine(GrandFinisherNova());
        StartCoroutine(MiniShockwave(center, radius * 1.8f));
        StartCoroutine(ImpactSparks(center, 24));

        // Ring of 8 embedded holy blades bursting out radially
        int surroundCount = 8;
        for (int b = 0; b < surroundCount; b++)
        {
            float ang = (360f / surroundCount) * b;
            float r = radius * 0.65f;
            Vector3 pos = center + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, Mathf.Sin(ang * Mathf.Deg2Rad) * r * 0.7f, 0f);

            GameObject subBlade = new GameObject("SurroundBlade_" + b);
            subBlade.transform.position = pos + new Vector3(0f, 0.3f, 0f);
            subBlade.transform.localScale = Vector3.one * 1.1f;
            subBlade.transform.rotation = Quaternion.Euler(0f, 0f, 180f + Random.Range(-15f, 15f));

            var sSr = subBlade.AddComponent<SpriteRenderer>();
            sSr.sprite = bladeSprite != null ? bladeSprite : AuraVFXLibrary.GetHolySword();
            sSr.sortingOrder = sortingOrder + 5;
            sSr.color = hotCoreColour;

            StartCoroutine(EmbeddedBladeRoutine(subBlade, sSr, 0f));
        }

        // Titan blade embeds deeply, vibrates violently, then dissolves in golden light
        titanBlade.transform.position = center + new Vector3(0f, 0.5f, 0f);
        Destroy(titanTail);
        StartCoroutine(EmbeddedBladeRoutine(titanBlade, titanSr, 0f));
    }

    IEnumerator MiniShockwave(Vector3 at, float maxRadius)
    {
        GameObject ring = new GameObject("BladeHitRing");
        ring.transform.position = at;

        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        sr.color = hotCoreColour;
        sr.sortingOrder = sortingOrder + 1;

        float ringDuration = 0.28f;
        float elapsed = 0f;

        while (elapsed < ringDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / ringDuration;
            float size = Mathf.Lerp(0.2f, maxRadius, Mathf.Pow(t, 0.65f));
            ring.transform.localScale = new Vector3(size, size * 0.5f, 1f);
            sr.color = new Color(auraColour.r, auraColour.g, auraColour.b, (1f - t) * 0.95f);
            yield return null;
        }

        Destroy(ring);
    }

    IEnumerator ImpactSparks(Vector3 at, int count)
    {
        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();

        for (int i = 0; i < count; i++)
        {
            GameObject spark = new GameObject("HolySpark");
            spark.transform.position = at;

            var sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = sparkSprite;
            sr.color = Color.Lerp(auraColour, hotCoreColour, Random.value);
            sr.sortingOrder = sortingOrder + 5;

            StartCoroutine(AnimateHolySpark(spark));
        }

        yield return null;
    }

    IEnumerator AnimateHolySpark(GameObject spark)
    {
        float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
        float speed = Random.Range(5f, 11f);
        Vector2 velocity = new Vector2(Mathf.Cos(angle) * (Random.value > 0.5f ? 1f : -1f), Mathf.Sin(angle)) * speed;
        float gravity = 16f;
        float life = Random.Range(0.28f, 0.55f);
        float elapsed = 0f;
        float size = Random.Range(0.3f, 0.6f);

        var sr = spark.GetComponent<SpriteRenderer>();

        while (elapsed < life)
        {
            if (spark == null) yield break;

            elapsed += Time.deltaTime;
            velocity.y -= gravity * Time.deltaTime;
            spark.transform.position += (Vector3)(velocity * Time.deltaTime);

            float t = elapsed / life;
            spark.transform.localScale = Vector3.one * (size * (1f - t));
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);

            yield return null;
        }

        if (spark != null) Destroy(spark);
    }

    IEnumerator GrandFinisherNova()
    {
        GameObject nova = new GameObject("GrandFinisherNova");
        nova.transform.position = transform.position;

        var sr = nova.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetGlowDisc();
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder + 10;

        float novaDuration = 0.55f;
        float elapsed = 0f;

        while (elapsed < novaDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / novaDuration;
            float size = Mathf.Lerp(1f, radius * 3.5f, Mathf.Pow(t, 0.55f));
            nova.transform.localScale = new Vector3(size, size * 0.58f, 1f);

            sr.color = Color.Lerp(Color.white, auraColour, t);
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.95f);
            yield return null;
        }

        Destroy(nova);
    }

    void OnDestroy()
    {
        foreach (GameObject go in spawned)
            if (go != null) Destroy(go);
    }
}

