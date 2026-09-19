using UnityEngine;

/// Plays a sprite sequence once and deletes itself. Deliberately not an Animator:
/// a one-shot effect does not need a controller asset, a state machine, or a
/// NetworkObject - every client spawns its own copy locally.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 14f;
    public bool loop = false;
    public bool destroyWhenDone = true;

    [Tooltip("Fades out across the last portion of the sequence. 0 disables.")]
    [Range(0f, 1f)]
    public float fadeOutFraction = 0.35f;

    SpriteRenderer sprite;
    float elapsed;
    Color baseColor;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseColor = sprite.color;

        if (frames != null && frames.Length > 0) sprite.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        elapsed += Time.deltaTime;

        float total = frames.Length / Mathf.Max(framesPerSecond, 0.01f);
        float progress = elapsed / total;

        if (progress >= 1f)
        {
            if (loop) { elapsed = 0f; progress = 0f; }
            else
            {
                if (destroyWhenDone) Destroy(gameObject);
                return;
            }
        }

        int index = Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1);
        sprite.sprite = frames[index];

        if (fadeOutFraction > 0f && progress > 1f - fadeOutFraction)
        {
            float t = (progress - (1f - fadeOutFraction)) / fadeOutFraction;
            sprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t));
        }
    }
}
