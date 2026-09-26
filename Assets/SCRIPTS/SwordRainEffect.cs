using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// The visual for Rain of Swords: a golden aura flare, then blades falling from
/// off-screen in waves, each announced by a shadow on the ground before it lands.
///
/// Built in code like the beam was - the shadow tell is what makes it readable
/// rather than a flicker of sprites, and it needs no art beyond the blade the
/// ring of swords already uses.
public class SwordRainEffect : MonoBehaviour
{
    public Sprite bladeSprite;
    public GameObject impactPrefab;

    public float radius = 5.5f;
    public int swordCount = 34;
    public float duration = 2.6f;
    public int sortingOrder = 40;

    [Header("Fall")]
    public float fallHeight = 7f;
    public float fallSeconds = 0.32f;
    public float bladeScale = 0.85f;

    [Header("Aura")]
    public Color auraColour = new Color(1f, 0.88f, 0.45f);
    public float auraSeconds = 0.55f;

    static Sprite quad;
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
        StartCoroutine(AuraRoutine());
        StartCoroutine(RainRoutine());
    }

    /// A ring of light thrown out from the caster's feet, twice.
    IEnumerator AuraRoutine()
    {
        for (int pulse = 0; pulse < 2; pulse++)
        {
            GameObject ring = MakeQuad(auraColour, sortingOrder - 1);
            float elapsed = 0f;

            while (elapsed < auraSeconds)
            {
                elapsed += Time.deltaTime;
                float k = elapsed / auraSeconds;

                float size = Mathf.Lerp(0.4f, radius * 2.2f, k);
                ring.transform.localScale = new Vector3(size, size * 0.55f, 1f);

                var sr = ring.GetComponent<SpriteRenderer>();
                sr.color = new Color(auraColour.r, auraColour.g, auraColour.b, (1f - k) * 0.5f);

                yield return null;
            }

            Destroy(ring);
            yield return new WaitForSeconds(0.18f);
        }
    }

    IEnumerator RainRoutine()
    {
        float gap = duration / Mathf.Max(1, swordCount);

        for (int i = 0; i < swordCount; i++)
        {
            // Walk outward as the storm goes on, so it reads as spreading rather
            // than as random noise landing everywhere at once.
            float spread = Mathf.Lerp(0.25f, 1f, i / (float)swordCount);
            Vector2 offset = Random.insideUnitCircle.normalized *
                             Random.Range(0.1f, radius * spread);

            StartCoroutine(OneSword(transform.position + new Vector3(offset.x, offset.y, 0f)));

            yield return new WaitForSeconds(gap);
        }

        yield return new WaitForSeconds(fallSeconds + 0.6f);
        Destroy(gameObject);
    }

    IEnumerator OneSword(Vector3 landAt)
    {
        // The shadow is the tell: it says where and roughly when, so the hit is
        // read as inevitable rather than arbitrary.
        GameObject shadow = MakeQuad(new Color(0f, 0f, 0f, 0.45f), sortingOrder - 2);
        shadow.transform.position = landAt;

        GameObject blade = new GameObject("Blade");
        blade.transform.position = landAt + new Vector3(0f, fallHeight, 0f);
        blade.transform.localScale = Vector3.one * bladeScale;
        blade.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // point down

        var bladeSr = blade.AddComponent<SpriteRenderer>();
        bladeSr.sprite = bladeSprite;
        bladeSr.sortingOrder = sortingOrder + 2;
        bladeSr.color = new Color(1f, 0.95f, 0.75f);

        spawned.Add(shadow);
        spawned.Add(blade);

        float elapsed = 0f;
        while (elapsed < fallSeconds)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / fallSeconds;

            // Accelerating fall reads as weight.
            blade.transform.position = Vector3.Lerp(
                landAt + new Vector3(0f, fallHeight, 0f), landAt, k * k);

            float size = Mathf.Lerp(0.15f, 0.55f, k);
            shadow.transform.localScale = new Vector3(size, size * 0.45f, 1f);

            yield return null;
        }

        Destroy(shadow);
        Destroy(blade);

        if (impactPrefab != null)
        {
            GameObject impact = Instantiate(impactPrefab, landAt, Quaternion.identity);
            impact.transform.localScale = Vector3.one * 0.55f;

            var sr = impact.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 0.92f, 0.65f);
        }

        CameraFollow.Shake(0.06f);
    }

    GameObject MakeQuad(Color colour, int order)
    {
        var go = new GameObject("Quad");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Quad();
        sr.color = colour;
        sr.sortingOrder = order;

        return go;
    }

    static Sprite Quad()
    {
        if (quad != null) return quad;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        quad = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        quad.name = "SwordRainQuad";
        return quad;
    }

    void OnDestroy()
    {
        foreach (GameObject go in spawned)
            if (go != null) Destroy(go);
    }
}
