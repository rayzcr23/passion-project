using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Plays a death performance instead of the body simply vanishing.
///
/// Health.OnDied fires on every client from the replicated value, so each one
/// runs this locally - the effect costs no network traffic and everyone sees it.
/// Health's despawnDelay gives the body time to finish before the server removes it.
public class DeathEffect : MonoBehaviour
{
    [Header("Effect")]
    public GameObject effectPrefab;
    public Vector3 effectOffset = Vector3.zero;
    public float effectScale = 0.8f;

    [Header("Body")]
    public SpriteRenderer sprite;
    public float fadeSeconds = 0.45f;
    public float shrinkTo = 0.6f;
    public float spinDegrees = 0f;

    [Tooltip("Stop the corpse being shoved around or dealing damage as it fades.")]
    public bool disablePhysicsOnDeath = true;

    Health health;
    bool played;

    void Awake()
    {
        health = GetComponent<Health>();
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (health != null) health.OnDied += Play;
    }

    void OnDisable()
    {
        if (health != null) health.OnDied -= Play;
    }

    void Play()
    {
        if (played) return;
        played = true;

        if (effectPrefab != null)
        {
            GameObject fx = Instantiate(effectPrefab, transform.position + effectOffset, Quaternion.identity);
            fx.transform.localScale = Vector3.one * effectScale;

            // Draw in front of whatever died, including up on the plateau.
            var fxSprite = fx.GetComponent<SpriteRenderer>();
            if (fxSprite != null && sprite != null) fxSprite.sortingOrder = sprite.sortingOrder + 3;
        }

        if (disablePhysicsOnDeath)
        {
            foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;

            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        if (sprite != null) StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        Color start = sprite.color;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSeconds);

            sprite.color = new Color(start.r, start.g, start.b, start.a * (1f - t));
            transform.localScale = Vector3.Lerp(startScale, startScale * shrinkTo, t);

            if (spinDegrees != 0f)
                transform.rotation = Quaternion.Euler(0f, 0f, spinDegrees * t);

            yield return null;
        }

        sprite.color = new Color(start.r, start.g, start.b, 0f);
    }
}
