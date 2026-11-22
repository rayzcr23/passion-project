using System;
using UnityEngine;

/// Dialogue box and quest log. OnGUI rather than a Canvas, to match the rest of
/// the prototype's HUD - swap it for a real Canvas when the game is being
/// polished rather than built.
///
/// Purely local: what is on this player's screen is nobody else's business.
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    public KeyCode advanceKey = KeyCode.Space;
    public KeyCode questLogKey = KeyCode.J;

    public bool IsOpen { get; private set; }

    string speaker;
    string[] lines;
    int index;
    int openedFrame;
    Action onFinished;
    bool showQuestLog = true;

    static GUIStyle box, name_, body, centred;

    public static GUIStyle CentredLabel
    {
        get
        {
            if (centred == null)
            {
                centred = new GUIStyle(GUI.skin.label);
                centred.alignment = TextAnchor.MiddleCenter;
                centred.fontSize = Scaled(12);
                centred.normal.textColor = Color.white;
            }
            return centred;
        }
    }

    /// Scale factor relative to a 96-dpi baseline.
    /// iPads report ~264 dpi, so this returns ~2.75 there.
    /// Clamped to [1, 3] so nothing goes crazy on very unusual screens.
    static float UIScale =>
        Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 96f : 1f, 1f, 3f);

    static int Scaled(int px) => Mathf.RoundToInt(px * UIScale);
    static float Scaled(float px) => px * UIScale;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open(string speakerName, string[] content, Action finished)
    {
        if (content == null || content.Length == 0) return;

        speaker = speakerName;
        lines = content;
        index = 0;
        openedFrame = Time.frameCount;
        onFinished = finished;
        IsOpen = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(questLogKey)) showQuestLog = !showQuestLog;

        if (!IsOpen) return;

        // The same touch that opens a conversation must not skip its first line.
        if (Time.frameCount == openedFrame) return;

        bool tappedDialogue = false;
        Rect dialogueRect = DialogueRect();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began) continue;
            Vector2 guiPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);
            if (dialogueRect.Contains(guiPosition)) { tappedDialogue = true; break; }
        }

        if (Input.GetKeyDown(advanceKey) ||
            (!TouchControls.TouchModeActive && Input.GetMouseButtonDown(0)) ||
            tappedDialogue || TouchControls.InteractTouchedThisFrame || TouchControls.InteractPressed)
        {
            index++;
            if (index >= lines.Length) Close();
        }
    }

    void Close()
    {
        IsOpen = false;

        // Fire after closing, so a quest starting cannot reopen this box.
        Action finished = onFinished;
        onFinished = null;
        if (finished != null) finished();
    }

    void OnGUI()
    {
        EnsureStyles();

        if (showQuestLog) DrawQuestLog();
        if (IsOpen) DrawDialogue();
    }

    void DrawDialogue()
    {
        Rect rect = DialogueRect();
        float width = rect.width;
        float height = rect.height;

        GUI.Box(rect, GUIContent.none, box);

        GUI.Label(new Rect(rect.x + Scaled(18f), rect.y + Scaled(12f), width - Scaled(36f), Scaled(24f)), speaker, name_);
        GUI.Label(new Rect(rect.x + Scaled(18f), rect.y + Scaled(42f), width - Scaled(36f), Scaled(70f)), lines[index], body);

        string hint = TouchControls.TouchModeActive
            ? (index < lines.Length - 1 ? "Tap to continue" : "Tap to finish")
            : (index < lines.Length - 1 ? "[Space] continue" : "[Space] end");
        GUI.Label(new Rect(rect.x + width - Scaled(150f), rect.y + height - Scaled(26f), Scaled(140f), Scaled(20f)), hint, body);
    }

    static Rect DialogueRect()
    {
        float width = Mathf.Min(Scaled(720f), Screen.width - Scaled(60f));
        float height = Scaled(130f);
        return new Rect((Screen.width - width) * 0.5f,
                        Screen.height - height - Scaled(30f), width, height);
    }

    void DrawQuestLog()
    {
        QuestManager qm = QuestManager.Instance;
        if (qm == null || qm.Entries == null) return;

        var active = qm.ActiveQuests();
        if (active.Count == 0) return;

        float rowPlain   = Scaled(22f);
        float rowCounted = Scaled(40f);

        float width  = Scaled(300f);
        float height = Scaled(30f);
        foreach (var q in active) height += q.counted ? rowCounted : rowPlain;

        var rect = new Rect(Screen.width - width - Scaled(14f), Scaled(120f), width, height);

        GUI.Box(rect, GUIContent.none, box);
        GUI.Label(new Rect(rect.x + Scaled(12f), rect.y + Scaled(6f), width - Scaled(24f), Scaled(20f)), "Quests  [J]", name_);

        float y = rect.y + Scaled(28f);

        foreach (var q in active)
        {
            GUI.Label(new Rect(rect.x + Scaled(12f), y, width - Scaled(24f), Scaled(20f)), q.title, body);

            if (!q.counted) { y += rowPlain; continue; }

            var bar = new Rect(rect.x + Scaled(12f), y + Scaled(20f), width - Scaled(70f), Scaled(9f));
            DrawBar(bar, q.Fraction);

            GUI.Label(new Rect(bar.xMax + Scaled(6f), y + Scaled(15f), Scaled(50f), Scaled(20f)),
                      q.counter + "/" + q.required, body);

            y += rowCounted;
        }
    }

    /// Tints the built-in white texture rather than allocating bar textures.
    static void DrawBar(Rect rect, float fraction)
    {
        Color previous = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        if (fraction > 0f)
        {
            // Green as it fills, so a nearly-done quest reads at a glance.
            GUI.color = Color.Lerp(new Color(0.85f, 0.55f, 0.2f),
                                   new Color(0.35f, 0.85f, 0.4f), fraction);

            var fill = new Rect(rect.x, rect.y, rect.width * fraction, rect.height);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
        }

        GUI.color = previous;
    }

    static void EnsureStyles()
    {
        if (box != null) return;

        var background = new Texture2D(1, 1);
        background.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.09f, 0.92f));
        background.Apply();

        box = new GUIStyle(GUI.skin.box);
        box.normal.background = background;
        box.border = new RectOffset(2, 2, 2, 2);

        name_ = new GUIStyle(GUI.skin.label);
        name_.fontSize = Scaled(15);
        name_.fontStyle = FontStyle.Bold;
        name_.normal.textColor = new Color(1f, 0.85f, 0.4f);

        body = new GUIStyle(GUI.skin.label);
        body.fontSize = Scaled(13);
        body.wordWrap = true;
        body.normal.textColor = Color.white;
    }
}
