using System.Collections;
using UnityEngine;

/// The Hollow Purple visual, built entirely from code - there is no purple beam
/// art in either sprite pack, and a procedural one scales to any length the
/// ability is tuned to.
///
/// Layers, back to front: a wide soft haze, a mid-purple body, a white-hot core,
/// plus a charge sphere that collapses before the beam fires and shockwave rings
/// that race down it. Purely cosmetic, so every client runs its own.
public class BeamEffect : MonoBehaviour
{
    public float length = 20f;
    public float width = 2.4f;
    public float chargeSeconds = 0.35f;
    public float holdSeconds = 0.45f;
    public float fadeSeconds = 0.5f;
    public int sortingOrder = 40;

    public Color inner = new Color(1f, 0.96f, 1f);
    public Color mid = new Color(0.72f, 0.28f, 1f);
    public Color outer = new Color(0.38f, 0.08f, 0.75f);

    static Sprite quad;

    Transform haze, body, core, charge;
    readonly Transform[] rings = new Transform[4];

    public static BeamEffect Spawn(Vector3 origin, Vector2 direction,
                                   float length, float width, int sortingOrder)
    {
        var go = new GameObject("HollowPurple");
        go.transform.position = origin;
        go.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        var beam = go.AddComponent<BeamEffect>();
        beam.length = length;
        beam.width = width;
        beam.sortingOrder = sortingOrder;
        return beam;
    }

    void Start()
    {
        haze = MakePart("Haze", outer, 0.35f);
        body = MakePart("Body", mid, 0.85f);
        core = MakePart("Core", inner, 1f);

        charge = MakePart("Charge", inner, 1f);
        charge.localScale = Vector3.zero;

        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = MakePart("Ring" + i, mid, 0.7f);
            rings[i].localScale = Vector3.zero;
        }

        StartCoroutine(Run());
    }

    Transform MakePart(string name, Color colour, float alpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Quad();
        sr.color = new Color(colour.r, colour.g, colour.b, alpha);
        sr.sortingOrder = sortingOrder;

        return go.transform;
    }

    IEnumerator Run()
    {
        // 1. Charge: a small sphere pinches shut before it lets go.
        float t = 0f;
        while (t < chargeSeconds)
        {
            t += Time.deltaTime;
            float k = t / chargeSeconds;

            float size = Mathf.Lerp(width * 2.2f, width * 0.35f, k * k);
            charge.localScale = new Vector3(size, size, 1f);
            charge.localPosition = Vector3.zero;

            SetBeam(0f);
            yield return null;
        }
        charge.localScale = Vector3.zero;

        // 2. Fire: the beam punches out along its length.
        float punch = 0.09f;
        t = 0f;
        while (t < punch)
        {
            t += Time.deltaTime;
            SetBeam(Mathf.SmoothStep(0f, 1f, t / punch));
            yield return null;
        }

        // 3. Hold, with rings racing down it.
        t = 0f;
        while (t < holdSeconds)
        {
            t += Time.deltaTime;
            SetBeam(1f);

            for (int i = 0; i < rings.Length; i++)
            {
                float travel = Mathf.Repeat(t * 2.6f + i / (float)rings.Length, 1f);
                rings[i].localPosition = new Vector3(travel * length, 0f, 0f);

                float ringSize = width * Mathf.Lerp(1.6f, 0.5f, travel);
                rings[i].localScale = new Vector3(ringSize * 0.25f, ringSize, 1f);
            }

            // Flicker the core so it reads as unstable rather than a static bar.
            float flicker = 1f + Mathf.Sin(Time.time * 60f) * 0.12f;
            core.localScale = new Vector3(length, width * 0.3f * flicker, 1f);

            yield return null;
        }

        // 4. Collapse.
        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float k = 1f - t / fadeSeconds;

            SetBeam(1f);
            SetAlpha(haze, 0.35f * k);
            SetAlpha(body, 0.85f * k);
            SetAlpha(core, k);

            foreach (Transform r in rings) SetAlpha(r, 0.7f * k);
            yield return null;
        }

        Destroy(gameObject);
    }

    void SetBeam(float extend)
    {
        float len = length * extend;
        float half = len * 0.5f;

        Set(haze, len, width * 1.9f, half);
        Set(body, len, width, half);
        Set(core, len, width * 0.3f, half);
    }

    static void Set(Transform t, float length, float thickness, float centre)
    {
        t.localScale = new Vector3(length, thickness, 1f);
        t.localPosition = new Vector3(centre, 0f, 0f);
    }

    static void SetAlpha(Transform t, float a)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        Color c = sr.color;
        sr.color = new Color(c.r, c.g, c.b, a);
    }

    /// One shared 1x1 sprite, scaled into every bar and ring.
    static Sprite Quad()
    {
        if (quad != null) return quad;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();

        quad = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        quad.name = "BeamQuad";
        return quad;
    }
}
