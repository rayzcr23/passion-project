using System.Collections;
using UnityEngine;

/// Visual water splash and ripple effect when the Giant Fish breaches or dives.
public class WaterSplashEffect : MonoBehaviour
{
    public float radius = 3.0f;
    public float duration = 0.6f;
    public Color waterColor = new Color(0.35f, 0.8f, 0.95f, 0.85f);
    public Color foamColor = new Color(0.9f, 0.98f, 1f, 0.95f);

    static Sprite quadSprite;

    public static WaterSplashEffect Spawn(Vector3 position, float radius = 3.0f)
    {
        var go = new GameObject("WaterSplashEffect");
        go.transform.position = position;
        var fx = go.AddComponent<WaterSplashEffect>();
        fx.radius = radius;
        return fx;
    }

    void Start()
    {
        StartCoroutine(SplashRoutine());
        Destroy(gameObject, duration + 0.2f);
    }

    IEnumerator SplashRoutine()
    {
        // Expanding ripple
        GameObject ripple = MakeCircle(waterColor, 25);
        ripple.transform.position = transform.position;
        var sr = ripple.GetComponent<SpriteRenderer>();

        // Flying droplets
        int dropletCount = 12;
        Transform[] droplets = new Transform[dropletCount];
        Vector2[] velocities = new Vector2[dropletCount];

        for (int i = 0; i < dropletCount; i++)
        {
            GameObject d = MakeCircle(foamColor, 35);
            d.transform.position = transform.position;
            d.transform.localScale = Vector3.one * Random.Range(0.2f, 0.45f);
            droplets[i] = d.transform;

            float angle = Random.Range(30f, 150f) * Mathf.Deg2Rad;
            float speed = Random.Range(4f, 9f);
            velocities[i] = new Vector2(Mathf.Cos(angle) * speed * (Random.value > 0.5f ? 1 : -1), Mathf.Sin(angle) * speed);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Expand and fade ripple
            float currentSize = Mathf.Lerp(0.5f, radius * 2f, Mathf.Sqrt(t));
            ripple.transform.localScale = new Vector3(currentSize, currentSize * 0.45f, 1f);
            sr.color = new Color(waterColor.r, waterColor.g, waterColor.b, (1f - t) * 0.7f);

            // Move droplets with gravity
            for (int i = 0; i < dropletCount; i++)
            {
                if (droplets[i] != null)
                {
                    velocities[i].y -= 18f * Time.deltaTime;
                    droplets[i].position += (Vector3)(velocities[i] * Time.deltaTime);
                }
            }

            yield return null;
        }

        Destroy(ripple);
        for (int i = 0; i < dropletCount; i++)
        {
            if (droplets[i] != null) Destroy(droplets[i].gameObject);
        }
    }

    static GameObject MakeCircle(Color color, int order)
    {
        if (quadSprite == null)
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float r = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float a = Mathf.Clamp01((r - dist) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            quadSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        var go = new GameObject("WaterVisual");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = quadSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }
}
