using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// High-octane anime visual effect for Glacial Burst.
/// 1. Sub-Zero anime impact frame.
/// 2. Dual counter-rotating hexagonal snowflake mandala array on the ground with frost mist.
/// 3. Ring of 12 crystalline ice spikes connected by a crackling crystalline energy polygon cage.
/// 4. Explosive 360-degree radial launch with sonic boom freeze shockwaves.
/// 5. Shattering crystal bursts leaving sub-zero frostbite patches on the ground.
public class GlacialBurstEffect : MonoBehaviour
{
    public float ringRadius = 1.8f;
    public int spikeCount = 12;
    public float summonDuration = 0.32f;
    public float flySpeed = 26f;
    public float maxRange = 14f;
    public int sortingOrder = 40;

    public Color iceCoreColor = Color.white;
    public Color iceBodyColor = new Color(0.35f, 0.88f, 1f);
    public Color frostAuraColor = new Color(0.1f, 0.55f, 0.95f);

    LineRenderer energyCage;
    static Material lineMat;

    public static GlacialBurstEffect Spawn(Vector3 origin, float radius = 1.8f, int count = 12,
                                           float speed = 26f, float range = 14f, int order = 40)
    {
        var go = new GameObject("GlacialBurstEffect");
        go.transform.position = origin;

        var fx = go.AddComponent<GlacialBurstEffect>();
        fx.ringRadius = radius;
        fx.spikeCount = count;
        fx.flySpeed = speed;
        fx.maxRange = range;
        fx.sortingOrder = order;
        return fx;
    }

    static Material GetLineMaterial()
    {
        if (lineMat == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Hidden/Internal-Colored");
            lineMat = new Material(s);
        }
        return lineMat;
    }

    void Start()
    {
        // 1. SUB-ZERO ANIME IMPACT FRAME
        ImpactFrameEffect.TriggerCustom(new Color(0.35f, 0.88f, 1f), 0.22f, 0.75f, true);

        // 2. DUAL GROUND FROST MANDALA ARRAY
        StartCoroutine(DualSnowflakeRoutine());

        // 3. RING OF ICE SPIKES FORMATION & LAUNCH
        StartCoroutine(SpikesRingRoutine());

        Destroy(gameObject, 2.8f);
    }

    /// Dual counter-rotating sacred snowflake mandalas on the ground
    IEnumerator DualSnowflakeRoutine()
    {
        Sprite runeSprite = AuraVFXLibrary.GetSnowflakeRune();
        Sprite glowSprite = AuraVFXLibrary.GetGlowDisc();

        // 1. Outer clockwise mandala
        GameObject outerRune = new GameObject("OuterSnowflakeRune");
        outerRune.transform.SetParent(transform, false);
        outerRune.transform.localPosition = new Vector3(0f, -0.15f, 0f);

        var outerSr = outerRune.AddComponent<SpriteRenderer>();
        outerSr.sprite = runeSprite;
        outerSr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, 0.85f);
        outerSr.sortingOrder = sortingOrder - 5;

        // 2. Inner counter-clockwise mandala
        GameObject innerRune = new GameObject("InnerSnowflakeRune");
        innerRune.transform.SetParent(transform, false);
        innerRune.transform.localPosition = new Vector3(0f, -0.15f, 0f);

        var innerSr = innerRune.AddComponent<SpriteRenderer>();
        innerSr.sprite = runeSprite;
        innerSr.color = Color.Lerp(iceBodyColor, Color.white, 0.45f);
        innerSr.sortingOrder = sortingOrder - 4;

        // 3. Sub-zero mist floor disc
        GameObject mist = new GameObject("FrostMistDisc");
        mist.transform.SetParent(transform, false);
        mist.transform.localPosition = new Vector3(0f, -0.15f, 0f);

        var mistSr = mist.AddComponent<SpriteRenderer>();
        mistSr.sprite = glowSprite;
        mistSr.color = new Color(frostAuraColor.r, frostAuraColor.g, frostAuraColor.b, 0.4f);
        mistSr.sortingOrder = sortingOrder - 6;

        float duration = 1.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float outerScale = Mathf.Lerp(0.4f, ringRadius * 2.8f, Mathf.Min(1f, elapsed / 0.22f));
            outerRune.transform.localScale = new Vector3(outerScale, outerScale * 0.58f, 1f);
            innerRune.transform.localScale = new Vector3(outerScale * 0.55f, outerScale * 0.55f * 0.58f, 1f);
            mist.transform.localScale = new Vector3(outerScale * 1.35f, outerScale * 0.75f, 1f);

            // Counter-rotation
            outerRune.transform.Rotate(0f, 0f, 45f * Time.deltaTime);
            innerRune.transform.Rotate(0f, 0f, -80f * Time.deltaTime);

            if (elapsed > duration - 0.5f)
            {
                float fade = (duration - elapsed) / 0.5f;
                outerSr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, fade * 0.85f);
                innerSr.color = new Color(innerSr.color.r, innerSr.color.g, innerSr.color.b, fade * 0.85f);
                mistSr.color = new Color(frostAuraColor.r, frostAuraColor.g, frostAuraColor.b, fade * 0.4f);
            }

            yield return null;
        }

        Destroy(outerRune);
        Destroy(innerRune);
        Destroy(mist);
    }

    IEnumerator SpikesRingRoutine()
    {
        Sprite spikeSprite = AuraVFXLibrary.GetIceSpike();
        Sprite cometSprite = AuraVFXLibrary.GetCometStreak();

        List<Transform> spikeTransforms = new List<Transform>();
        List<Vector2> spikeDirections = new List<Vector2>();

        Vector3 center = transform.position;

        // Create crackling crystalline energy cage line renderer
        GameObject cageObj = new GameObject("CrystallineEnergyCage");
        cageObj.transform.SetParent(transform, false);
        energyCage = cageObj.AddComponent<LineRenderer>();
        energyCage.material = GetLineMaterial();
        energyCage.useWorldSpace = true;
        energyCage.loop = true;
        energyCage.startWidth = 0.06f;
        energyCage.endWidth = 0.06f;
        energyCage.sortingOrder = sortingOrder + 1;
        energyCage.positionCount = spikeCount;

        // Phase 1: Materialise 12 ice spikes in a circle around the caster
        for (int i = 0; i < spikeCount; i++)
        {
            float angleDeg = (360f / spikeCount) * i;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject spike = new GameObject("IceSpike_" + i);
            spike.transform.position = center + (Vector3)(dir * 0.3f);
            // Spike points outward
            spike.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
            spike.transform.localScale = Vector3.zero;

            var sr = spike.AddComponent<SpriteRenderer>();
            sr.sprite = spikeSprite;
            sr.sortingOrder = sortingOrder + 3;

            spikeTransforms.Add(spike.transform);
            spikeDirections.Add(dir);
        }

        // Animate materialisation, energy cage connection, and vibration
        float summonElapsed = 0f;
        while (summonElapsed < summonDuration)
        {
            summonElapsed += Time.deltaTime;
            float k = Mathf.Clamp01(summonElapsed / summonDuration);
            float currentDist = Mathf.Lerp(0.4f, ringRadius, Mathf.Pow(k, 0.7f));

            for (int i = 0; i < spikeCount; i++)
            {
                if (spikeTransforms[i] == null) continue;
                Vector2 dir = spikeDirections[i];
                float jitter = Mathf.Sin(Time.time * 50f + i) * 0.04f * k;
                Vector3 spikePos = center + (Vector3)(dir * (currentDist + jitter));
                spikeTransforms[i].position = spikePos;
                spikeTransforms[i].localScale = Vector3.one * Mathf.Lerp(0.2f, 1.35f, k);

                if (energyCage != null)
                {
                    energyCage.SetPosition(i, spikePos);
                }
            }

            if (energyCage != null)
            {
                float flicker = (Mathf.Sin(Time.time * 40f) + 1f) * 0.5f;
                Color cageCol = Color.Lerp(iceBodyColor, Color.white, flicker);
                cageCol.a = k * 0.85f;
                energyCage.startColor = cageCol;
                energyCage.endColor = cageCol;
            }

            yield return null;
        }

        // Shatter the energy cage upon launch
        if (cageObj != null) Destroy(cageObj);

        // Phase 2: RADIAL LAUNCH!
        CameraFollow.Shake(0.55f);
        StartCoroutine(LaunchBlastRing(center));

        for (int i = 0; i < spikeCount; i++)
        {
            if (spikeTransforms[i] != null)
            {
                StartCoroutine(FlySpikeRoutine(spikeTransforms[i], spikeDirections[i], cometSprite));
            }
        }
    }

    IEnumerator LaunchBlastRing(Vector3 center)
    {
        // 1. Sonic Boom Ring
        GameObject boom = new GameObject("FrostSonicBoom");
        boom.transform.position = center;
        var boomSr = boom.AddComponent<SpriteRenderer>();
        boomSr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        boomSr.color = Color.white;
        boomSr.sortingOrder = sortingOrder + 2;

        // 2. Glowing Frost Core Disc
        GameObject core = new GameObject("FrostCoreDisc");
        core.transform.position = center;
        var coreSr = core.AddComponent<SpriteRenderer>();
        coreSr.sprite = AuraVFXLibrary.GetGlowDisc();
        coreSr.color = iceBodyColor;
        coreSr.sortingOrder = sortingOrder + 1;

        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float boomSize = Mathf.Lerp(0.6f, ringRadius * 4.2f, Mathf.Pow(t, 0.55f));
            boom.transform.localScale = new Vector3(boomSize, boomSize * 0.55f, 1f);
            boomSr.color = Color.Lerp(Color.white, iceBodyColor, t);
            boomSr.color = new Color(boomSr.color.r, boomSr.color.g, boomSr.color.b, (1f - t) * 0.95f);

            float coreSize = Mathf.Lerp(0.4f, ringRadius * 2.2f, Mathf.Pow(t, 0.65f));
            core.transform.localScale = new Vector3(coreSize, coreSize * 0.5f, 1f);
            coreSr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, (1f - t) * 0.8f);

            yield return null;
        }

        Destroy(boom);
        Destroy(core);
    }

    IEnumerator FlySpikeRoutine(Transform spike, Vector2 dir, Sprite cometSprite)
    {
        // Add frost comet trail behind the flying ice lance
        GameObject trail = new GameObject("FrostTrail");
        trail.transform.SetParent(spike, false);
        trail.transform.localPosition = new Vector3(0f, -0.65f, 0f); // trails behind base
        trail.transform.localRotation = Quaternion.identity;
        trail.transform.localScale = new Vector3(1.2f, 2.8f, 1f);

        var trailSr = trail.AddComponent<SpriteRenderer>();
        trailSr.sprite = cometSprite;
        trailSr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, 0.9f);
        trailSr.sortingOrder = sortingOrder + 1;

        Vector3 startPos = spike.position;
        float travelled = 0f;
        float moteTimer = 0f;

        while (travelled < maxRange)
        {
            if (spike == null) yield break;

            float step = flySpeed * Time.deltaTime;
            spike.position += (Vector3)(dir * step);
            travelled += step;

            // Spawn drifting frost crystals along flight path
            moteTimer += Time.deltaTime;
            if (moteTimer >= 0.025f)
            {
                moteTimer = 0f;
                SpawnTrailMote(spike.position);
            }

            // Check if colliding with enemies
            Collider2D hit = Physics2D.OverlapCircle(spike.position, 0.35f);
            if (hit != null && hit.gameObject != gameObject)
            {
                Health target = hit.GetComponentInParent<Health>();
                if (target != null && !target.IsDead && target.GetComponent<AbilityLoadout>() == null)
                {
                    // Shatter on enemy hit!
                    ShatterIceSpike(spike.position, dir);
                    Destroy(spike.gameObject);
                    yield break;
                }
            }

            yield return null;
        }

        if (spike != null)
        {
            ShatterIceSpike(spike.position, dir);
            Destroy(spike.gameObject);
        }
    }

    void SpawnTrailMote(Vector3 at)
    {
        GameObject mote = new GameObject("FrostMote");
        mote.transform.position = at + (Vector3)(Random.insideUnitCircle * 0.18f);

        var sr = mote.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetSparkStar();
        sr.color = Color.Lerp(Color.white, iceBodyColor, Random.value);
        sr.sortingOrder = sortingOrder + 2;

        StartCoroutine(FadeMoteRoutine(mote));
    }

    IEnumerator FadeMoteRoutine(GameObject mote)
    {
        float life = Random.Range(0.2f, 0.38f);
        float elapsed = 0f;
        var sr = mote.GetComponent<SpriteRenderer>();
        Color startCol = sr.color;

        while (elapsed < life)
        {
            if (mote == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            sr.color = new Color(startCol.r, startCol.g, startCol.b, 1f - t);
            mote.transform.localScale = Vector3.one * ((1f - t) * 0.4f);
            yield return null;
        }

        if (mote != null) Destroy(mote);
    }

    void ShatterIceSpike(Vector3 at, Vector2 incomingDir)
    {
        CameraFollow.Shake(0.12f);

        // Flash burst ring
        GameObject burst = new GameObject("FrostBurst");
        burst.transform.position = at;

        var burstSr = burst.AddComponent<SpriteRenderer>();
        burstSr.sprite = AuraVFXLibrary.GetGlowDisc();
        burstSr.color = Color.white;
        burstSr.sortingOrder = sortingOrder + 4;

        StartCoroutine(FadeBurstRoutine(burst));

        // Sub-zero frostbite ground patch
        StartCoroutine(FrostbiteGroundPatch(at));

        // Scatter 8 sharp diamond frost shards
        Sprite shardSprite = AuraVFXLibrary.GetFrostShard();
        int shardCount = 8;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject("IceShard");
            shard.transform.position = at;

            var sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = shardSprite;
            sr.color = Color.Lerp(Color.white, iceBodyColor, Random.value);
            sr.sortingOrder = sortingOrder + 5;

            StartCoroutine(AnimateShardRoutine(shard, incomingDir));
        }
    }

    IEnumerator FrostbiteGroundPatch(Vector3 at)
    {
        GameObject patch = new GameObject("FrostbitePatch");
        patch.transform.position = at;

        var sr = patch.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetSnowflakeRune();
        sr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, 0.8f);
        sr.sortingOrder = sortingOrder - 2;

        float duration = 0.9f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (patch == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scale = Mathf.Lerp(0.3f, 1.4f, Mathf.Min(1f, elapsed / 0.15f));
            patch.transform.localScale = new Vector3(scale, scale * 0.55f, 1f);

            if (elapsed > duration - 0.4f)
            {
                float fade = (duration - elapsed) / 0.4f;
                sr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, fade * 0.8f);
            }

            yield return null;
        }

        if (patch != null) Destroy(patch);
    }

    IEnumerator FadeBurstRoutine(GameObject burst)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        var sr = burst.GetComponent<SpriteRenderer>();

        while (elapsed < duration)
        {
            if (burst == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float size = Mathf.Lerp(0.3f, 1.8f, t);
            burst.transform.localScale = Vector3.one * size;
            sr.color = new Color(iceBodyColor.r, iceBodyColor.g, iceBodyColor.b, 1f - t);
            yield return null;
        }

        if (burst != null) Destroy(burst);
    }

    IEnumerator AnimateShardRoutine(GameObject shard, Vector2 incomingDir)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float speed = Random.Range(4f, 10f);
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + incomingDir * 2.5f;
        float gravity = 15f;
        float life = Random.Range(0.28f, 0.52f);
        float elapsed = 0f;
        float rotSpeed = Random.Range(-360f, 360f);

        var sr = shard.GetComponent<SpriteRenderer>();
        Color startCol = sr.color;

        while (elapsed < life)
        {
            if (shard == null) yield break;
            elapsed += Time.deltaTime;
            vel.y -= gravity * Time.deltaTime;
            shard.transform.position += (Vector3)(vel * Time.deltaTime);
            shard.transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

            float t = elapsed / life;
            sr.color = new Color(startCol.r, startCol.g, startCol.b, 1f - t);
            shard.transform.localScale = Vector3.one * ((1f - t * 0.4f) * 0.55f);

            yield return null;
        }

        if (shard != null) Destroy(shard);
    }
}
