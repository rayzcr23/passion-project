using System.Collections;
using UnityEngine;

/// Cracked ground radiating from an impact, plus an expanding ring.
///
/// Built from stretched quads like the beam and the sword rain - cracks are
/// just thin rectangles at varied angles and lengths, which reads as broken
/// stone in a top-down view and needs no art.
public class ShockwaveEffect : MonoBehaviour
{
    public float radius = 2.3f;
    public int crackCount = 9;
    public float ringSeconds = 0.35f;
    public float crackSeconds = 0.9f;
    public int sortingOrder = 40;

    public Color ringColour = new Color(1f, 0.92f, 0.7f);
    public Color crackColour = new Color(0.25f, 0.18f, 0.12f);

    static Sprite quad;

    public static ShockwaveEffect Spawn(Vector3 at, float radius, int cracks, int order)
    {
        var go = new GameObject("Shockwave");
        go.transform.position = at;

        var fx = go.AddComponent<ShockwaveEffect>();
        fx.radius = radius;
        fx.crackCount = cracks;
        fx.sortingOrder = order;
        return fx;
    }

    void Start()
    {
        StartCoroutine(RingRoutine());
        SpawnCracks();

        Destroy(gameObject, Mathf.Max(ringSeconds, crackSeconds) + 0.1f);
    }

    IEnumerator RingRoutine()
    {
        GameObject ring = MakeQuad(ringColour, sortingOrder);
        var sr = ring.GetComponent<SpriteRenderer>();

        float elapsed = 0f;
        while (elapsed < ringSeconds)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / ringSeconds;

            float size = Mathf.Lerp(0.3f, radius * 2.4f, k);
            ring.transform.localScale = new Vector3(size, size * 0.5f, 1f);

            sr.color = new Color(ringColour.r, ringColour.g, ringColour.b, (1f - k) * 0.75f);
            yield return null;
        }

        Destroy(ring);
    }

    void SpawnCracks()
    {
        for (int i = 0; i < crackCount; i++)
        {
            float angle = (360f / crackCount) * i + Random.Range(-14f, 14f);
            float length = radius * Random.Range(0.55f, 1.15f);

            GameObject crack = MakeQuad(crackColour, sortingOrder - 1);

            crack.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            crack.transform.localPosition =
                Quaternion.Euler(0f, 0f, angle) * new Vector3(length * 0.5f, 0f, 0f);

            crack.transform.localScale = new Vector3(length, Random.Range(0.05f, 0.11f), 1f);

            StartCoroutine(FadeCrack(crack));
        }
    }

    IEnumerator FadeCrack(GameObject crack)
    {
        var sr = crack.GetComponent<SpriteRenderer>();
        Vector3 full = crack.transform.localScale;

        // Cracks race outward, then linger and fade.
        float grow = 0.12f;
        float elapsed = 0f;

        while (elapsed < grow)
        {
            elapsed += Time.deltaTime;
            crack.transform.localScale = new Vector3(
                full.x * (elapsed / grow), full.y, 1f);
            yield return null;
        }

        crack.transform.localScale = full;

        elapsed = 0f;
        while (elapsed < crackSeconds)
        {
            elapsed += Time.deltaTime;
            float k = 1f - elapsed / crackSeconds;

            sr.color = new Color(crackColour.r, crackColour.g, crackColour.b, k * 0.8f);
            yield return null;
        }
    }

    GameObject MakeQuad(Color colour, int order)
    {
        var go = new GameObject("Quad");
        go.transform.SetParent(transform, false);

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
        quad.name = "ShockwaveQuad";
        return quad;
    }
}
