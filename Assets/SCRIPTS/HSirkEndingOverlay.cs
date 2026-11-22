using UnityEngine;

/// A short, local ending shown to every player when H-sirk is defeated.
public sealed class HSirkEndingOverlay : MonoBehaviour
{
    static HSirkEndingOverlay instance;
    const float FlashSeconds = 0.85f;
    const float EndingSeconds = 9.5f;

    float elapsed;
    bool showing;
    GUIStyle titleStyle;
    GUIStyle storyStyle;

    public static void Show()
    {
        if (instance == null)
        {
            var overlay = new GameObject("[H-sirk Ending]");
            instance = overlay.AddComponent<HSirkEndingOverlay>();
            DontDestroyOnLoad(overlay);
        }

        instance.elapsed = 0f;
        instance.showing = true;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (!showing) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= EndingSeconds) showing = false;
    }

    void OnGUI()
    {
        if (!showing) return;

        GUI.depth = -1000;
        float width = Screen.width;
        float height = Screen.height;

        // A full white flash marks the finishing blow, then dissolves into
        // a soft, readable ending card over the village.
        float flash = elapsed < 0.22f ? 1f :
            Mathf.Clamp01(1f - (elapsed - 0.22f) / (FlashSeconds - 0.22f));
        if (flash > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, flash);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        }

        float textIn = Mathf.Clamp01((elapsed - 0.6f) / 0.7f);
        float textOut = Mathf.Clamp01((EndingSeconds - elapsed) / 1.4f);
        float alpha = textIn * textOut;
        if (alpha <= 0f) { GUI.color = Color.white; return; }

        float cardWidth = Mathf.Min(width - 32f, 720f);
        float cardHeight = Mathf.Min(height - 36f, 240f);
        Rect card = new Rect((width - cardWidth) * 0.5f,
            (height - cardHeight) * 0.5f, cardWidth, cardHeight);

        GUI.color = new Color(0.03f, 0.07f, 0.09f, 0.83f * alpha);
        GUI.DrawTexture(card, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.89f, 0.55f, alpha);
        GUI.DrawTexture(new Rect(card.x + 18f, card.y + 17f, card.width - 36f, 2f), Texture2D.whiteTexture);

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            storyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        titleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.038f), 20, 34);
        titleStyle.normal.textColor = new Color(1f, 0.93f, 0.7f, alpha);
        storyStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.025f), 15, 22);
        storyStyle.normal.textColor = new Color(0.94f, 0.97f, 1f, alpha);

        GUI.color = Color.white;
        GUI.Label(new Rect(card.x + 20f, card.y + 32f, card.width - 40f, 62f),
            "H-SIRK HAS BEEN DEFEATED", titleStyle);
        GUI.Label(new Rect(card.x + 28f, card.y + 100f, card.width - 56f, card.height - 115f),
            "Peace returned to the world.\nThe villagers lived happily ever after.", storyStyle);
        GUI.color = Color.white;
    }
}
