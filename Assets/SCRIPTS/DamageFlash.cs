using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Tints a sprite when its Health drops. Works on anything with Health -
/// players, goblins, the boss later - and needs no setup beyond the component.
///
/// Driven by Health.OnChanged, which fires on every client, so everyone sees
/// every flash without a single extra network message.
[DisallowMultipleComponent]
public class DamageFlash : MonoBehaviour
{
    public Health health;
    public SpriteRenderer sprite;

    [Header("Flash")]
    public Color flashColor = new Color(1f, 0.3f, 0.3f);
    public float flashSeconds = 0.12f;
    public int flashCount = 2;

    [Header("Punch")]
    [Tooltip("Brief squash on impact. Scales the transform, so the health bar " +
         "punches along with the body.")]
    public bool punchScale = true;
    public float punchAmount = 0.18f;
    public float punchSeconds = 0.12f;

    [Header("Camera")]
    [Tooltip("Shake when THIS object is hit, only if it is the local player's " +
         "character. Leave at 0 for enemies.")]
    public float cameraShakeOnDamage = 0f;

    Color baseColor;
    Vector3 baseScale;
    int lastHealth = -1;
    Coroutine running;

    void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();

        if (sprite != null) baseColor = sprite.color;
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        if (health == null) return;
        lastHealth = health.Current;
        health.OnChanged += HandleChanged;
    }

    void OnDisable()
    {
        if (health != null) health.OnChanged -= HandleChanged;
        Restore();
    }

    void HandleChanged(int current, int max)
    {
        bool tookDamage = lastHealth >= 0 && current < lastHealth;
        lastHealth = current;

        if (!tookDamage) return;

        if (cameraShakeOnDamage > 0f)
        {
            // Only your own character shakes your camera.
            NetworkObject netObject = GetComponent<NetworkObject>();
            if (netObject == null || !netObject.IsSpawned || netObject.IsOwner)
                CameraFollow.Shake(cameraShakeOnDamage);
        }

        if (sprite == null) return;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        // Take only the RGB. Alpha belongs to PlayerDeath: a killing blow starts
        // this flash at full alpha, PlayerDeath then fades to 0.35, and restoring
        // a captured colour here would undo the fade a moment later.
        baseColor = sprite.color;

        if (punchScale) StartCoroutine(PunchRoutine());

        for (int i = 0; i < Mathf.Max(1, flashCount); i++)
        {
            SetRgb(flashColor);
            yield return new WaitForSeconds(flashSeconds / (flashCount * 2f));

            SetRgb(baseColor);
            yield return new WaitForSeconds(flashSeconds / (flashCount * 2f));
        }

        SetRgb(baseColor);
        running = null;
    }

    IEnumerator PunchRoutine()
    {
        float elapsed = 0f;
        while (elapsed < punchSeconds)
        {
            elapsed += Time.deltaTime;

            // Out and back within the window.
            float t = elapsed / punchSeconds;
            float curve = Mathf.Sin(t * Mathf.PI);

            transform.localScale = baseScale * (1f + punchAmount * curve);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    /// Writes colour while leaving whatever alpha is currently set intact.
    void SetRgb(Color rgb)
    {
        if (sprite == null) return;
        sprite.color = new Color(rgb.r, rgb.g, rgb.b, sprite.color.a);
    }

    void Restore()
    {
        SetRgb(baseColor);
        transform.localScale = baseScale;
    }
}
