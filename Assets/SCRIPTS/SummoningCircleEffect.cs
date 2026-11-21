using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Celestial Runic Summoning Array that appears on the ground during Rain of Swords.
/// Displays concentric counter-rotating rune rings, a ground energy disc, and rising golden motes.
public class SummoningCircleEffect : MonoBehaviour
{
    public float radius = 5.5f;
    public float duration = 3.2f;
    public Color primaryColor = new Color(1f, 0.88f, 0.35f);
    public Color secondaryColor = new Color(1f, 0.65f, 0.15f);
    public int sortingOrder = 5;

    GameObject outerRing;
    GameObject innerRing;
    GameObject groundDisc;
    readonly List<GameObject> motes = new List<GameObject>();

    public static SummoningCircleEffect Spawn(Vector3 position, float radius, float duration, int order = 5)
    {
        var go = new GameObject("SummoningCircle");
        go.transform.position = position;

        var circle = go.AddComponent<SummoningCircleEffect>();
        circle.radius = radius;
        circle.duration = duration;
        circle.sortingOrder = order;
        return circle;
    }

    void Start()
    {
        BuildVisuals();
        StartCoroutine(AnimateRoutine());
        StartCoroutine(MotesRoutine());
    }

    void BuildVisuals()
    {
        Sprite runeSprite = AuraVFXLibrary.GetRunicCircle();
        Sprite glowSprite = AuraVFXLibrary.GetGlowDisc();

        // 1. Soft glowing ground disc
        groundDisc = new GameObject("GroundDisc");
        groundDisc.transform.SetParent(transform, false);
        var discSr = groundDisc.AddComponent<SpriteRenderer>();
        discSr.sprite = glowSprite;
        discSr.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.28f);
        discSr.sortingOrder = sortingOrder - 2;
        groundDisc.transform.localScale = new Vector3(radius * 2.4f, radius * 1.3f, 1f);

        // 2. Primary outer rune ring
        outerRing = new GameObject("OuterRuneRing");
        outerRing.transform.SetParent(transform, false);
        var outerSr = outerRing.AddComponent<SpriteRenderer>();
        outerSr.sprite = runeSprite;
        outerSr.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.85f);
        outerSr.sortingOrder = sortingOrder;
        outerRing.transform.localScale = Vector3.zero;

        // 3. Counter-rotating inner array
        innerRing = new GameObject("InnerRuneRing");
        innerRing.transform.SetParent(transform, false);
        var innerSr = innerRing.AddComponent<SpriteRenderer>();
        innerSr.sprite = runeSprite;
        innerSr.color = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.7f);
        innerSr.sortingOrder = sortingOrder + 1;
        innerRing.transform.localScale = Vector3.zero;
    }

    IEnumerator AnimateRoutine()
    {
        float elapsed = 0f;
        float expandTime = 0.35f;
        float targetScale = radius * 1.85f;

        Vector3 targetOuter = new Vector3(targetScale, targetScale * 0.58f, 1f);
        Vector3 targetInner = new Vector3(targetScale * 0.65f, targetScale * 0.38f, 1f);

        var outerSr = outerRing.GetComponent<SpriteRenderer>();
        var innerSr = innerRing.GetComponent<SpriteRenderer>();
        var discSr = groundDisc.GetComponent<SpriteRenderer>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float life = elapsed / duration;

            // Expand at start
            if (elapsed < expandTime)
            {
                float t = elapsed / expandTime;
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                outerRing.transform.localScale = targetOuter * ease;
                innerRing.transform.localScale = targetInner * ease;
            }
            else
            {
                // Pulsing breath
                float pulse = 1f + Mathf.Sin(elapsed * 6f) * 0.04f;
                outerRing.transform.localScale = targetOuter * pulse;
                innerRing.transform.localScale = targetInner * (2f - pulse);
            }

            // Counter-rotation
            outerRing.transform.Rotate(0f, 0f, 40f * Time.deltaTime);
            innerRing.transform.Rotate(0f, 0f, -65f * Time.deltaTime);

            // Fade out in final 0.6s
            if (elapsed > duration - 0.6f)
            {
                float fade = (duration - elapsed) / 0.6f;
                outerSr.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, fade * 0.85f);
                innerSr.color = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, fade * 0.7f);
                discSr.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, fade * 0.28f);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    IEnumerator MotesRoutine()
    {
        Sprite spark = AuraVFXLibrary.GetSparkStar();
        float interval = 0.08f;
        float timer = 0f;

        while (timer < duration - 0.4f)
        {
            timer += interval;

            Vector2 offset = Random.insideUnitCircle * (radius * 0.9f);
            Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y * 0.55f, 0f);

            GameObject mote = new GameObject("RuneMote");
            mote.transform.position = spawnPos;

            var sr = mote.AddComponent<SpriteRenderer>();
            sr.sprite = spark;
            sr.color = Color.Lerp(primaryColor, secondaryColor, Random.value);
            sr.sortingOrder = sortingOrder + 2;

            StartCoroutine(AnimateMote(mote));

            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator AnimateMote(GameObject mote)
    {
        float life = Random.Range(0.6f, 1.1f);
        float elapsed = 0f;
        float riseSpeed = Random.Range(1.8f, 3.4f);
        float startScale = Random.Range(0.25f, 0.5f);
        Vector3 initialPos = mote.transform.position;
        float wobble = Random.Range(1f, 3f);
        float wobbleSpeed = Random.Range(4f, 8f);

        var sr = mote.GetComponent<SpriteRenderer>();
        Color baseCol = sr.color;

        while (elapsed < life)
        {
            if (mote == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / life;

            float y = initialPos.y + elapsed * riseSpeed;
            float x = initialPos.x + Mathf.Sin(elapsed * wobbleSpeed) * 0.15f * wobble;
            mote.transform.position = new Vector3(x, y, initialPos.z);

            float scale = Mathf.Sin(t * Mathf.PI) * startScale;
            mote.transform.localScale = Vector3.one * scale;

            sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, 1f - t);

            yield return null;
        }

        if (mote != null) Destroy(mote);
    }

    void OnDestroy()
    {
        foreach (var m in motes)
            if (m != null) Destroy(m);
    }
}
