using System.Collections;
using UnityEngine;

/// Fullscreen Anime Impact Frame system.
/// Triggers stark, high-contrast black/white manga action frames, chromatic flashes,
/// micro-hitstop freeze frames, and visceral screen punches during massive ability beats.
public class ImpactFrameEffect : MonoBehaviour
{
    public static ImpactFrameEffect Instance { get; private set; }

    public enum Style
    {
        SuperRainOfSwords, // Celestial Golden manga impact frame
        SuperFinisher,     // Heavy black/white inverted grand finale impact
        ExecutionerSlam,   // Fiery crimson/amber earth shatter impact
        HollowPurple,      // Inverted negative space reality tear
        Custom
    }

    bool isPlaying;
    float timer;
    float totalDuration = 0.24f;
    int phase; // 0 = Invert 1 (Black/White), 1 = Invert 2 (White/Black), 2 = Chromatic Aura Glow, 3 = Fade
    Color auraTint = new Color(1f, 0.88f, 0.35f);
    bool useHitstop = true;
    float hitstopDuration = 0.055f;
    bool hitstopActive;
    float restoreTimeScale = 1f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// Triggers an anime impact frame sequence with customizable style, duration, and screen shake.
    public static void Trigger(Style style, float duration = 0.24f, float shake = 0.85f, bool hitstop = true)
    {
        EnsureInstance();
        Color tint = Color.white;
        switch (style)
        {
            case Style.SuperRainOfSwords:
                tint = new Color(1f, 0.88f, 0.35f); // Divine Gold
                break;
            case Style.SuperFinisher:
                tint = new Color(1f, 0.95f, 0.6f);  // Blinding Sun Gold
                break;
            case Style.ExecutionerSlam:
                tint = new Color(1f, 0.35f, 0.12f); // Fiery Crimson
                break;
            case Style.HollowPurple:
                tint = new Color(0.75f, 0.3f, 1f);  // Void Purple
                break;
        }

        Instance.PlaySequence(tint, duration, shake, hitstop);
    }

    /// Triggers an impact frame with arbitrary custom tint and settings.
    public static void TriggerCustom(Color tint, float duration = 0.22f, float shake = 0.8f, bool hitstop = true)
    {
        EnsureInstance();
        Instance.PlaySequence(tint, duration, shake, hitstop);
    }

    static void EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("[ImpactFrameOverlay]");
            Instance = go.AddComponent<ImpactFrameEffect>();
            DontDestroyOnLoad(go);
        }
    }

    void PlaySequence(Color tint, float duration, float shake, bool hitstop)
    {
        RestoreHitstopTime();

        auraTint = tint;
        totalDuration = Mathf.Max(0.12f, duration);
        useHitstop = hitstop;

        if (shake > 0f)
        {
            CameraFollow.Shake(shake);
        }

        StopAllCoroutines();
        StartCoroutine(SequenceRoutine());
    }

    void OnDisable()
    {
        RestoreHitstopTime();
    }

    IEnumerator SequenceRoutine()
    {
        isPlaying = true;
        timer = 0f;

        // Micro hitstop for that anime freeze-frame impact punch
        if (useHitstop)
        {
            restoreTimeScale = Time.timeScale > 0.1f ? Time.timeScale : 1f;
            hitstopActive = true;
            Time.timeScale = 0.05f;
            float realElapsed = 0f;
            while (realElapsed < hitstopDuration)
            {
                realElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            RestoreHitstopTime();
        }

        while (timer < totalDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = timer / totalDuration;

            if (progress < 0.2f) phase = 0;        // Black bg + White speedlines
            else if (progress < 0.42f) phase = 1;   // White bg + Black speedlines
            else if (progress < 0.75f) phase = 2;   // Chromatic Aura Burst
            else phase = 3;                         // Dissolve & fade back to normal

            yield return null;
        }

        isPlaying = false;
    }

    void RestoreHitstopTime()
    {
        if (!hitstopActive) return;

        Time.timeScale = restoreTimeScale > 0.1f ? restoreTimeScale : 1f;
        hitstopActive = false;
        restoreTimeScale = 1f;
    }

    void OnGUI()
    {
        if (!isPlaying) return;

        Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);
        Texture2D actionLines = AuraVFXLibrary.GetImpactActionLines();
        Texture2D shatter = AuraVFXLibrary.GetImpactShatter();

        float progress = Mathf.Clamp01(timer / totalDuration);

        switch (phase)
        {
            case 0:
                // --- PHASE 1: STARK PITCH BLACK WITH BLINDING WHITE SPEEDLINES ---
                GUI.color = Color.black;
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);

                GUI.color = Color.white;
                if (actionLines != null) GUI.DrawTexture(screenRect, actionLines);
                if (shatter != null) GUI.DrawTexture(screenRect, shatter);
                break;

            case 1:
                // --- PHASE 2: INVERTED BLINDING WHITE WITH PITCH BLACK SLASHES ---
                GUI.color = Color.white;
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);

                GUI.color = Color.black;
                if (actionLines != null) GUI.DrawTexture(screenRect, actionLines);
                if (shatter != null) GUI.DrawTexture(screenRect, shatter);
                break;

            case 2:
                // --- PHASE 3: RADIANT CHROMATIC AURA BLOOM ---
                float bloomAlpha = Mathf.Lerp(0.85f, 0.4f, (progress - 0.42f) / 0.33f);
                GUI.color = new Color(auraTint.r * 0.15f, auraTint.g * 0.15f, auraTint.b * 0.15f, bloomAlpha * 0.65f);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);

                GUI.color = new Color(auraTint.r, auraTint.g, auraTint.b, bloomAlpha);
                if (actionLines != null) GUI.DrawTexture(screenRect, actionLines);
                break;

            case 3:
                // --- PHASE 4: SMOOTH DECAY & VIGNETTE RETURN ---
                float fadeAlpha = (1f - progress) / 0.25f;
                GUI.color = new Color(auraTint.r, auraTint.g, auraTint.b, fadeAlpha * 0.4f);
                if (actionLines != null) GUI.DrawTexture(screenRect, actionLines);
                break;
        }

        GUI.color = Color.white;
    }
}
