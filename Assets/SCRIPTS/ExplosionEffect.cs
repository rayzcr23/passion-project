using System.Collections;
using UnityEngine;

/// Visual and physical explosion effect with expanding fireball, flash, and shockwave.
public class ExplosionEffect : MonoBehaviour
{
    public float radius = 2.4f;
    public float duration = 0.45f;
    public Color coreColor = new Color(1f, 0.95f, 0.3f, 1f);
    public Color flameColor = new Color(1f, 0.4f, 0.1f, 0.9f);
    public Color smokeColor = new Color(0.25f, 0.22f, 0.2f, 0.6f);

    static Sprite circleSprite;

    public static ExplosionEffect Spawn(Vector3 position, float radius = 2.4f)
    {
        var go = new GameObject("ExplosionEffect");
        go.transform.position = position;
        var fx = go.AddComponent<ExplosionEffect>();
        fx.radius = radius;
        return fx;
    }

    void Start()
    {
        CameraFollow.Shake(0.35f);
        StartCoroutine(BlastRoutine());
        Destroy(gameObject, duration + 0.1f);
    }

    IEnumerator BlastRoutine()
    {
        GameObject burst = MakeCircle(coreColor, 50);
        burst.transform.position = transform.position;
        var sr = burst.GetComponent<SpriteRenderer>();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float currentScale = Mathf.Lerp(0.3f, radius * 2f, Mathf.Sin(t * Mathf.PI * 0.5f));
            burst.transform.localScale = Vector3.one * currentScale;

            Color c = (t < 0.3f)
                ? Color.Lerp(coreColor, flameColor, t / 0.3f)
                : Color.Lerp(flameColor, smokeColor, (t - 0.3f) / 0.7f);

            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;

            yield return null;
        }

        Destroy(burst);
    }

    static GameObject MakeCircle(Color color, int order)
    {
        if (circleSprite == null) circleSprite = GenerateCircleSprite();

        var go = new GameObject("BurstVisual");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static Sprite GenerateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float r = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01((r - dist) / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
