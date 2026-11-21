using System.Collections;
using UnityEngine;

/// High-velocity anime slash wave effect spawned during melee sword swings.
/// Supports a dynamic 3-hit combo visual progression:
/// 1. Swift Horizontal Azure Crescent
/// 2. Rising Reverse Amber/Gold Arc
/// 3. Colossal Critical Cross-Cleave Finisher with sonic shockwaves and spark explosion!
public class SlashArcEffect : MonoBehaviour
{
    public float duration = 0.16f;
    public int comboStep = 0;
    public Color slashColor = new Color(0.35f, 0.85f, 1f);
    public Color coreColor = Color.white;
    public int sortingOrder = 35;

    public static SlashArcEffect Spawn(Vector3 origin, Vector2 direction, float reach = 1.3f, int order = 35, int combo = 0)
    {
        var go = new GameObject("SlashArc");
        go.transform.position = origin;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        var fx = go.AddComponent<SlashArcEffect>();
        fx.comboStep = combo;
        fx.sortingOrder = order;

        // Customise color and angle per combo step
        if (combo == 0) // Step 1: Swift Horizontal Azure Arc
        {
            fx.slashColor = new Color(0.3f, 0.85f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle - 45f);
            fx.transform.localScale = Vector3.one * reach;
        }
        else if (combo == 1) // Step 2: Rising Reverse Amber Arc
        {
            fx.slashColor = new Color(1f, 0.75f, 0.25f);
            // Flip Y so it sweeps upwards from bottom-right
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle + 45f);
            fx.transform.localScale = new Vector3(reach, -reach, 1f);
        }
        else // Step 3: CRITICAL CROSS CLEAVE FINISHER
        {
            fx.slashColor = new Color(1f, 0.95f, 0.5f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            fx.transform.localScale = Vector3.one * (reach * 1.55f);
            fx.duration = 0.22f;
        }

        return fx;
    }

    void Start()
    {
        StartCoroutine(AnimateSlash());
        StartCoroutine(SpawnSparks());

        if (comboStep == 2)
        {
            // Finisher sonic shockwave
            StartCoroutine(FinisherShockwave());
            CameraFollow.Shake(0.18f);
        }

        Destroy(gameObject, duration + 0.15f);
    }

    IEnumerator AnimateSlash()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = (comboStep == 2) ? AuraVFXLibrary.GetSlashCross() : AuraVFXLibrary.GetSlashCrescent();
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder;

        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Rapid explosive expansion, fading at tip
            float expand = Mathf.Lerp(0.7f, 1.35f, Mathf.Pow(t, 0.55f));
            transform.localScale = baseScale * expand;

            Color c = Color.Lerp(coreColor, slashColor, t * 1.4f);
            sr.color = new Color(c.r, c.g, c.b, (1f - t * t) * 0.95f);

            yield return null;
        }

        sr.enabled = false;
    }

    IEnumerator FinisherShockwave()
    {
        GameObject ring = new GameObject("FinisherShockwave");
        ring.transform.position = transform.position;

        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder - 1;

        float dur = 0.22f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            if (ring == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float s = Mathf.Lerp(0.4f, 2.5f, Mathf.Pow(t, 0.6f));
            ring.transform.localScale = new Vector3(s, s * 0.55f, 1f);
            sr.color = new Color(slashColor.r, slashColor.g, slashColor.b, (1f - t) * 0.9f);
            yield return null;
        }

        Destroy(ring);
    }

    IEnumerator SpawnSparks()
    {
        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();
        int count = (comboStep == 2) ? 8 : 4;
        Vector3 forward = transform.up;

        for (int i = 0; i < count; i++)
        {
            GameObject sp = new GameObject("SlashSpark");
            sp.transform.position = transform.position + forward * Random.Range(0.2f, 0.6f);

            var sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = sparkSprite;
            sr.color = Color.Lerp(Color.white, slashColor, Random.value);
            sr.sortingOrder = sortingOrder + 1;

            StartCoroutine(AnimateSpark(sp));
        }

        yield return null;
    }

    IEnumerator AnimateSpark(GameObject sp)
    {
        float speed = Random.Range(5f, 11f);
        float angle = Random.Range(-50f, 50f) * Mathf.Deg2Rad;
        Vector2 dir = transform.right * Mathf.Cos(angle) + transform.up * Mathf.Sin(angle);

        float life = Random.Range(0.14f, 0.26f);
        float elapsed = 0f;
        var sr = sp.GetComponent<SpriteRenderer>();

        while (elapsed < life)
        {
            if (sp == null) yield break;
            elapsed += Time.deltaTime;
            sp.transform.position += (Vector3)(dir * (speed * Time.deltaTime));

            float t = elapsed / life;
            sp.transform.localScale = Vector3.one * (0.4f * (1f - t));
            if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);

            yield return null;
        }

        if (sp != null) Destroy(sp);
    }

    /// Spawns an intense anime impact spark burst on hit
    public static void SpawnHitImpact(Vector3 at, Vector2 direction, int order = 40)
    {
        GameObject burst = new GameObject("MeleeHitBurst");
        burst.transform.position = at;

        var sr = burst.AddComponent<SpriteRenderer>();
        sr.sprite = AuraVFXLibrary.GetGlowDisc();
        sr.color = Color.white;
        sr.sortingOrder = order;

        CameraFollow.Shake(0.12f);

        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();
        int count = 6;
        for (int i = 0; i < count; i++)
        {
            GameObject sp = new GameObject("HitSpark");
            sp.transform.position = at;

            var spSr = sp.AddComponent<SpriteRenderer>();
            spSr.sprite = sparkSprite;
            spSr.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), Random.value);
            spSr.sortingOrder = order + 1;

            float ang = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
            Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-45f, 45f)) * direction;
            float spd = Random.Range(4f, 9f);
            sp.AddComponent<SparkMote>().Init(dir.normalized * spd, 0.22f);
        }

        var fader = burst.AddComponent<SimpleFader>();
        fader.Init(0.14f, 0.2f, 1.4f);
    }
}

public class SimpleFader : MonoBehaviour
{
    float duration;
    float startScale;
    float endScale;
    float elapsed;
    SpriteRenderer sr;

    public void Init(float dur, float sStart, float sEnd)
    {
        duration = dur;
        startScale = sStart;
        endScale = sEnd;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        float s = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = Vector3.one * s;
        if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
        if (elapsed >= duration) Destroy(gameObject);
    }
}

public class SparkMote : MonoBehaviour
{
    Vector2 vel;
    float life;
    float elapsed;
    SpriteRenderer sr;

    public void Init(Vector2 velocity, float lifetime)
    {
        vel = velocity;
        life = lifetime;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += (Vector3)(vel * Time.deltaTime);
        float t = elapsed / life;
        transform.localScale = Vector3.one * (0.35f * (1f - t));
        if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
        if (elapsed >= life) Destroy(gameObject);
    }
}

