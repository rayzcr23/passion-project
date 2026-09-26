using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Pannable view of the branching skill tree. Press K.
///
/// Only the unlocked nodes and the frontier touching them are drawn - the tree
/// has no edge, so it is rendered as a window onto a function rather than a
/// laid-out list of objects.
public class SkillTreeUI : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.K;
    public float nodeRadius = 7f;
    public float frontierRadius = 5f;

    bool open;
    Vector2 pan;
    Vector2 dragStart;
    bool dragging;

    readonly List<long> visible = new List<long>();

    static Texture2D pixel;
    static GUIStyle panel, title, small;

    SkillTree Tree
    {
        get
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.SpawnManager == null) return null;

            NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
            return player != null ? player.GetComponent<SkillTree>() : null;
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;

        open = !open;
        if (open) pan = Vector2.zero;
    }

    void OnGUI()
    {
        if (!open) return;

        SkillTree tree = Tree;
        if (tree == null) return;

        EnsureStyles();

        var area = new Rect(50f, 50f, Screen.width - 100f, Screen.height - 100f);
        GUI.Box(area, GUIContent.none, panel);

        GUI.Label(new Rect(area.x + 16f, area.y + 10f, 400f, 24f),
                  "Skill Tree   [" + toggleKey + "]", title);
        GUI.Label(new Rect(area.xMax - 200f, area.y + 12f, 180f, 22f),
                  "Points: " + tree.Points, title);

        HandleDrag(area);

        tree.CollectVisible(visible);
        DrawTree(tree, area);
        DrawLegend(tree, area);
    }

    void HandleDrag(Rect area)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
        {
            dragStart = e.mousePosition - pan;
            dragging = true;
        }
        else if (e.type == EventType.MouseDrag && dragging) pan = e.mousePosition - dragStart;
        else if (e.type == EventType.MouseUp) dragging = false;
    }

    void DrawTree(SkillTree tree, Rect area)
    {
        GUI.BeginClip(area);

        Vector2 origin = new Vector2(area.width * 0.5f, area.height * 0.5f) + pan;
        long clicked = -1;

        // Branches first, so the nodes sit on top of them.
        foreach (long id in visible)
        {
            long parent = SkillTree.ParentOf(id);
            if (parent < 0) continue;

            Vector2 a = origin + Flip(tree.PositionOf(parent));
            Vector2 b = origin + Flip(tree.PositionOf(id));

            bool solid = tree.IsUnlocked(id);
            DrawLine(a, b, solid ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.16f),
                     solid ? 2.5f : 1.5f);
        }

        foreach (long id in visible)
        {
            Vector2 p = origin + Flip(tree.PositionOf(id));

            bool unlocked = tree.IsUnlocked(id);
            float radius = unlocked ? nodeRadius : frontierRadius;
            var rect = new Rect(p.x - radius, p.y - radius, radius * 2f, radius * 2f);

            Color colour = SkillTree.ColourOf(tree.StatOf(id));
            bool affordable = tree.CanAfford(id);

            if (id == 0) GUI.color = Color.white;
            else if (unlocked) GUI.color = colour;
            else if (affordable) GUI.color = new Color(colour.r, colour.g, colour.b, 0.6f);
            else GUI.color = new Color(0.4f, 0.4f, 0.46f, 0.45f);

            GUI.DrawTexture(rect, Pixel());
            GUI.color = Color.white;

            if (!rect.Contains(Event.current.mousePosition)) continue;

            DrawTooltip(tree, p, id, unlocked);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                tree.IsReachable(id) && affordable)
                clicked = id;
        }

        GUI.EndClip();

        // Outside the clip, so the RPC is not fired mid-draw.
        if (clicked >= 0)
        {
            tree.RequestUnlockRpc(clicked);
            Event.current.Use();
        }
    }

    void DrawTooltip(SkillTree tree, Vector2 p, long id, bool unlocked)
    {
        string text = id == 0
            ? "The root"
            : tree.DescribeNode(id) + (unlocked ? "" : "    cost " + tree.CostOf(id));

        var tip = new Rect(p.x + 12f, p.y - 12f, 240f, 24f);

        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(tip, Pixel());
        GUI.color = Color.white;

        GUI.Label(new Rect(tip.x + 8f, tip.y + 2f, tip.width - 16f, 20f), text, small);
    }

    void DrawLegend(SkillTree tree, Rect area)
    {
        string[] lines =
        {
            "Damage x" + tree.DamageMultiplier.ToString("F2"),
            "Max Health +" + Mathf.RoundToInt(tree.HealthBonus),
            "Move Speed x" + tree.SpeedMultiplier.ToString("F2"),
            "POW Gain x" + tree.PowerMultiplier.ToString("F2"),
            "Cooldown x" + tree.CooldownMultiplier.ToString("F2")
        };

        var stats = (SkillTree.Stat[])System.Enum.GetValues(typeof(SkillTree.Stat));

        for (int i = 0; i < lines.Length; i++)
        {
            float y = area.yMax - 28f - (lines.Length - 1 - i) * 19f;

            GUI.color = SkillTree.ColourOf(stats[i]);
            GUI.DrawTexture(new Rect(area.x + 16f, y + 5f, 9f, 9f), Pixel());
            GUI.color = Color.white;

            GUI.Label(new Rect(area.x + 30f, y, 320f, 19f), lines[i], small);
        }

        GUI.Label(new Rect(area.xMax - 360f, area.yMax - 28f, 340f, 19f),
                  "drag to pan   -   click a lit node to unlock", small);
    }

    /// GUI space runs downward; the branch maths runs upward.
    static Vector2 Flip(Vector2 v) => new Vector2(v.x, -v.y);

    static void DrawLine(Vector2 a, Vector2 b, Color colour, float width)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        Matrix4x4 saved = GUI.matrix;

        GUI.color = colour;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), Pixel());

        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    static Texture2D Pixel()
    {
        if (pixel == null)
        {
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
        return pixel;
    }

    static void EnsureStyles()
    {
        if (panel != null) return;

        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.06f, 0.04f, 0.14f, 0.96f));
        bg.Apply();

        panel = new GUIStyle(GUI.skin.box);
        panel.normal.background = bg;

        title = new GUIStyle(GUI.skin.label);
        title.fontSize = 15;
        title.fontStyle = FontStyle.Bold;
        title.normal.textColor = new Color(1f, 0.85f, 0.4f);

        small = new GUIStyle(GUI.skin.label);
        small.fontSize = 11;
        small.normal.textColor = Color.white;
    }
}
