using UnityEngine;

/// Overhead visual indicator and beacon for the Blacksmith NPC at the eastern village.
/// Ensures the player can easily spot him in front of the houses from afar.
public class BlacksmithNPCMarker : MonoBehaviour
{
    public float maxVisibleDistance = 30f;
    public Vector3 overheadOffset = new Vector3(0f, 1.4f, 0f);
    public string title = "🔨 BLACKSMITH";
    public string subtitle = "[F] Iron Quest";

    static GUIStyle badgeStyle;
    static GUIStyle subStyle;
    static Texture2D darkBg;

    void OnGUI()
    {
        if (Camera.main == null) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;

        Vector3 worldPos = transform.position + overheadOffset + new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.1f, 0f);
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // Check if behind camera or too far
        if (screenPos.z < 0f || screenPos.z > maxVisibleDistance) return;

        EnsureStyles();

        float width = 170f;
        float height = 44f;
        float x = screenPos.x - width * 0.5f;
        float y = Screen.height - screenPos.y - height * 0.5f;

        Rect boxRect = new Rect(x, y, width, height);
        GUI.DrawTexture(boxRect, darkBg);

        // Gold border
        GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        GUI.Box(boxRect, GUIContent.none);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y + 3f, width, 20f), title, badgeStyle);
        GUI.Label(new Rect(x, y + 22f, width, 18f), subtitle, subStyle);
    }

    static void EnsureStyles()
    {
        if (darkBg == null)
        {
            darkBg = new Texture2D(1, 1);
            darkBg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.12f, 0.88f));
            darkBg.Apply();
        }

        if (badgeStyle == null)
        {
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            badgeStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
        }

        if (subStyle == null)
        {
            subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Normal
            };
            subStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
