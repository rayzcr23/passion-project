using UnityEngine;

/// Displays prominent quest banners and boss alert notifications on screen.
public class BannerOverlay : MonoBehaviour
{
    public static BannerOverlay Instance { get; private set; }

    string currentMessage = "";
    float displayTimer;
    const float Duration = 4.5f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    public static void Show(string message)
    {
        if (Instance == null)
        {
            var go = new GameObject("BannerOverlay");
            Instance = go.AddComponent<BannerOverlay>();
            DontDestroyOnLoad(go);
        }
        Instance.currentMessage = message;
        Instance.displayTimer = Duration;
    }

    void Update()
    {
        if (displayTimer > 0f) displayTimer -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (displayTimer <= 0f || string.IsNullOrEmpty(currentMessage)) return;

        float alpha = Mathf.Clamp01(displayTimer > 1f ? 1f : displayTimer);
        float width = Mathf.Min(Screen.width * 0.85f, 620f);
        float height = 48f;
        float x = (Screen.width - width) * 0.5f;
        float y = 55f;

        GUI.color = new Color(0f, 0f, 0f, alpha * 0.75f);
        GUI.DrawTexture(new Rect(x - 6, y - 4, width + 12, height + 8), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 0.88f, 0.2f, alpha);
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 15,
            wordWrap = true
        };
        GUI.Label(new Rect(x, y, width, height), currentMessage, style);
        GUI.color = Color.white;
    }
}
