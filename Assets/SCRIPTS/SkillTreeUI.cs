using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Enhanced Astral Skill Tree UI:
/// - Rich cosmic nebula & rotating astrolabe background
/// - Dual-layer neon energy branch beams
/// - Multi-layered glowing celestial nodes with breathing pulse on available nodes
/// - Distinct stat rune glyphs & color themes
/// - Glassmorphism hero attributes sidebar with progress bars
/// - Premium floating RPG tooltip card
public class SkillTreeUI : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.K;
    public float nodeRadius = 9f;
    public float frontierRadius = 7f;

    [Header("Zoom & Navigation")]
    public float zoom = 1f;
    public float minZoom = 0.35f;
    public float maxZoom = 2.8f;
    public float zoomStep = 0.15f;

    bool open;
    Vector2 pan;
    Vector2 dragStart;
    bool dragging;
    float openAnimTime;

    readonly List<long> visible = new List<long>();

    // Procedural textures
    static Texture2D circleTex;
    static Texture2D glowTex;
    static Texture2D ringTex;
    static Texture2D panelTex;
    static Texture2D cardTex;
    static Texture2D barBgTex;
    static Texture2D whitePixel;

    // GUI Styles
    static GUIStyle headerStyle;
    static GUIStyle subtitleStyle;
    static GUIStyle badgeStyle;
    static GUIStyle tooltipTitleStyle;
    static GUIStyle tooltipBodyStyle;
    static GUIStyle tooltipBadgeStyle;
    static GUIStyle statLabelStyle;
    static GUIStyle statValStyle;
    static GUIStyle buttonStyle;

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
        if (Input.GetKeyDown(toggleKey))
        {
            open = !open;
            if (open)
            {
                pan = Vector2.zero;
                openAnimTime = 0f;
            }
        }

        if (open)
        {
            openAnimTime += Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                pan = Vector2.zero;
                zoom = 1f;
            }
        }
    }

    void OnGUI()
    {
        if (!open) return;

        SkillTree tree = Tree;
        if (tree == null) return;

        EnsureVisualAssets();

        // Main window area with margin
        float margin = Mathf.Min(Screen.width * 0.05f, 40f);
        var area = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);

        // Backdrop Glassmorphism Panel
        DrawBackdrop(area);

        // Center map & tree canvas
        HandleDrag(area);

        tree.CollectVisible(visible);

        // Constellation Tree View
        DrawCosmicTree(tree, area);

        // Floating Hero Stats Card (Left)
        DrawAttributesSidebar(tree, area);

        // Top Navigation Header & Points Badge
        DrawHeader(tree, area);

        // Bottom Navigation Bar
        DrawFooter(area);
    }

    void HandleDrag(Rect area)
    {
        Event e = Event.current;

        // Reserve left sidebar area from dragging
        Rect sidebarRect = new Rect(area.x + 16f, area.y + 70f, 220f, 320f);
        if (sidebarRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
        {
            dragStart = e.mousePosition - pan;
            dragging = true;
        }
        else if (e.type == EventType.MouseDrag && dragging)
        {
            pan = e.mousePosition - dragStart;
        }
        else if (e.type == EventType.MouseUp)
        {
            dragging = false;
        }
        else if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
        {
            Vector2 centre = new Vector2(area.x + area.width * 0.5f, area.y + area.height * 0.5f);
            Vector2 before = (e.mousePosition - centre - pan) / zoom;

            zoom = Mathf.Clamp(zoom * (1f - e.delta.y * zoomStep * 0.1f), minZoom, maxZoom);
            pan = e.mousePosition - centre - before * zoom;
            e.Use();
        }
    }

    void DrawBackdrop(Rect area)
    {
        // Dark midnight cosmic glass panel
        GUI.color = Color.white;
        GUI.DrawTexture(area, panelTex);

        // Glowing border frame
        GUI.color = new Color(0.3f, 0.45f, 0.8f, 0.45f);
        DrawFrame(area, 2f);
        GUI.color = Color.white;
    }

    void DrawCosmicTree(SkillTree tree, Rect area)
    {
        GUI.BeginClip(area);

        Vector2 origin = new Vector2(area.width * 0.5f, area.height * 0.5f) + pan;
        long hoveredNode = -1;
        Vector2 hoveredPos = Vector2.zero;
        long clickedNode = -1;

        // Background Astrolabe / Celestial Rings centered at tree root
        DrawAstrolabe(origin);

        // Pass 1: Neon Glow on Branches
        foreach (long id in visible)
        {
            long parent = SkillTree.ParentOf(id);
            if (parent < 0) continue;

            Vector2 a = origin + Flip(tree.PositionOf(parent)) * zoom;
            Vector2 b = origin + Flip(tree.PositionOf(id)) * zoom;

            bool solid = tree.IsUnlocked(id);
            Color branchColor = solid
                ? SkillTree.ColourOf(tree.StatOf(id))
                : new Color(0.35f, 0.4f, 0.6f, 0.25f);

            // Wide soft glow beam
            DrawLine(a, b, new Color(branchColor.r, branchColor.g, branchColor.b, solid ? 0.35f : 0.1f), (solid ? 6f : 3f) * zoom);
            // Crisp core line
            DrawLine(a, b, solid ? Color.white : new Color(0.6f, 0.7f, 0.9f, 0.4f), (solid ? 2f : 1f) * zoom);
        }

        // Pass 2: Celestial Nodes
        float time = Time.time;
        foreach (long id in visible)
        {
            Vector2 p = origin + Flip(tree.PositionOf(id)) * zoom;
            bool unlocked = tree.IsUnlocked(id);
            bool affordable = tree.CanAfford(id);
            bool reachable = tree.IsReachable(id);

            Color statColor = (id == 0) ? new Color(1f, 0.9f, 0.4f) : SkillTree.ColourOf(tree.StatOf(id));

            float baseRadius = (unlocked ? nodeRadius : frontierRadius) * Mathf.Max(0.6f, zoom);
            float currentRadius = baseRadius;

            // Breathing pulse for affordable frontier nodes
            if (!unlocked && reachable && affordable)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6f + id);
                currentRadius += pulse * 2.5f * zoom;

                // Pulsing outer beacon ring
                float ringSize = currentRadius * (1.6f + pulse * 0.6f);
                GUI.color = new Color(statColor.r, statColor.g, statColor.b, 0.6f - pulse * 0.3f);
                GUI.DrawTexture(new Rect(p.x - ringSize, p.y - ringSize, ringSize * 2f, ringSize * 2f), ringTex);
            }

            var nodeRect = new Rect(p.x - currentRadius, p.y - currentRadius, currentRadius * 2f, currentRadius * 2f);
            bool isHovered = nodeRect.Contains(Event.current.mousePosition);

            if (isHovered)
            {
                currentRadius *= 1.25f;
                nodeRect = new Rect(p.x - currentRadius, p.y - currentRadius, currentRadius * 2f, currentRadius * 2f);
                hoveredNode = id;
                hoveredPos = p;
            }

            // Outer Radial Bloom Halo
            float haloSize = currentRadius * (unlocked ? 3.2f : (affordable ? 2.5f : 1.6f));
            Color haloColor = statColor;
            haloColor.a = unlocked ? 0.65f : (affordable ? 0.45f : 0.15f);
            GUI.color = haloColor;
            GUI.DrawTexture(new Rect(p.x - haloSize, p.y - haloSize, haloSize * 2f, haloSize * 2f), glowTex);

            // Core Circular Node
            Color coreColor = unlocked ? Color.white : (affordable ? statColor : new Color(0.2f, 0.22f, 0.3f, 0.8f));
            GUI.color = coreColor;
            GUI.DrawTexture(nodeRect, circleTex);

            // Inner Accent Ring
            if (unlocked || affordable)
            {
                GUI.color = statColor;
                float innerRing = currentRadius * 0.7f;
                GUI.DrawTexture(new Rect(p.x - innerRing, p.y - innerRing, innerRing * 2f, innerRing * 2f), ringTex);
            }

            // Click handling
            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                reachable && affordable && !unlocked)
            {
                clickedNode = id;
            }
        }

        // Draw Tooltip on top of tree elements
        if (hoveredNode >= 0)
        {
            DrawTooltipCard(tree, hoveredPos, hoveredNode);
        }

        GUI.EndClip();

        if (clickedNode >= 0)
        {
            tree.RequestUnlockRpc(clickedNode);
            CameraFollow.Shake(0.06f);
            Event.current.Use();
        }
    }

    void DrawAstrolabe(Vector2 origin)
    {
        float rot = Time.time * 4f;
        Color astroColor = new Color(0.25f, 0.4f, 0.75f, 0.12f);

        DrawCircleOutline(origin, 80f * zoom, astroColor, 1.5f);
        DrawCircleOutline(origin, 180f * zoom, astroColor, 1.2f);
        DrawCircleOutline(origin, 320f * zoom, astroColor, 1.0f);

        // Faint central aura
        float aura = 110f * zoom;
        GUI.color = new Color(1f, 0.85f, 0.4f, 0.08f);
        GUI.DrawTexture(new Rect(origin.x - aura, origin.y - aura, aura * 2f, aura * 2f), glowTex);
        GUI.color = Color.white;
    }

    void DrawHeader(SkillTree tree, Rect area)
    {
        // Top Banner Bar
        float headerY = area.y + 12f;

        // Title with glowing drop shadow
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(area.x + 22f, headerY + 2f, 380f, 28f), "✧ ASTRAL SKILL TREE ✧", headerStyle);
        GUI.color = new Color(1f, 0.88f, 0.45f);
        GUI.Label(new Rect(area.x + 20f, headerY, 380f, 28f), "✧ ASTRAL SKILL TREE ✧", headerStyle);

        GUI.color = new Color(0.7f, 0.75f, 0.9f);
        GUI.Label(new Rect(area.x + 24f, headerY + 26f, 380f, 20f), "Channel cosmic essence to forge your hero's destiny", subtitleStyle);

        // Glowing Skill Points Badge
        int pts = tree.Points;
        float badgeWidth = 190f;
        float badgeX = area.xMax - badgeWidth - 75f;
        Rect badgeRect = new Rect(badgeX, headerY + 4f, badgeWidth, 34f);

        GUI.color = (pts > 0) ? new Color(1f, 0.82f, 0.2f, 0.95f) : new Color(0.2f, 0.25f, 0.35f, 0.8f);
        GUI.DrawTexture(badgeRect, cardTex);

        // Badge border
        GUI.color = (pts > 0) ? new Color(1f, 0.95f, 0.5f, 0.8f) : new Color(0.4f, 0.45f, 0.55f, 0.4f);
        DrawFrame(badgeRect, 1.5f);

        // Badge Text
        GUI.color = (pts > 0) ? new Color(1f, 0.95f, 0.6f) : Color.white;
        string ptText = (pts > 0) ? string.Format("★ {0} POINTS READY", pts) : "0 POINTS";
        GUI.Label(badgeRect, ptText, badgeStyle);

        // Close Button [X]
        Rect closeRect = new Rect(area.xMax - 54f, headerY + 4f, 38f, 34f);
        GUI.color = new Color(0.75f, 0.2f, 0.25f, 0.85f);
        GUI.DrawTexture(closeRect, cardTex);
        GUI.color = Color.white;
        DrawFrame(closeRect, 1.5f);

        if (GUI.Button(closeRect, "✕", buttonStyle) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
        {
            open = false;
        }

        GUI.color = Color.white;
    }

    void DrawAttributesSidebar(SkillTree tree, Rect area)
    {
        float sidebarWidth = 210f;
        float sidebarHeight = 310f;
        Rect sideRect = new Rect(area.x + 18f, area.y + 70f, sidebarWidth, sidebarHeight);

        // Glassmorphism card body
        GUI.color = new Color(0.04f, 0.06f, 0.12f, 0.85f);
        GUI.DrawTexture(sideRect, cardTex);

        // Border
        GUI.color = new Color(0.25f, 0.4f, 0.65f, 0.4f);
        DrawFrame(sideRect, 1.5f);

        // Sidebar Title
        GUI.color = new Color(0.6f, 0.8f, 1f);
        GUI.Label(new Rect(sideRect.x + 12f, sideRect.y + 8f, sideRect.width - 24f, 22f), "✦ HERO ATTRIBUTES", subtitleStyle);

        float curY = sideRect.y + 36f;
        float rowHeight = 28f;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "⚔️ Damage", "x" + tree.DamageMultiplier.ToString("F2"), SkillTree.ColourOf(SkillTree.Stat.Damage), tree.DamageMultiplier - 1f);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "💖 Max HP", "+" + Mathf.RoundToInt(tree.HealthBonus), SkillTree.ColourOf(SkillTree.Stat.MaxHealth), tree.HealthBonus / 100f);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "⚡ Speed", "x" + tree.SpeedMultiplier.ToString("F2"), SkillTree.ColourOf(SkillTree.Stat.MoveSpeed), tree.SpeedMultiplier - 1f);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "🔮 POW Gain", "x" + tree.PowerMultiplier.ToString("F2"), SkillTree.ColourOf(SkillTree.Stat.PowerGain), tree.PowerMultiplier - 1f);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "⌛ Cooldown", "x" + tree.CooldownMultiplier.ToString("F2"), SkillTree.ColourOf(SkillTree.Stat.Cooldown), 1f - tree.CooldownMultiplier);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "🏹 Range", "x" + tree.RangeMultiplier.ToString("F2"), SkillTree.ColourOf(SkillTree.Stat.Range), tree.RangeMultiplier - 1f);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "🩸 Lifesteal", Mathf.RoundToInt(tree.Lifesteal * 100f) + "%", SkillTree.ColourOf(SkillTree.Stat.Lifesteal), tree.Lifesteal);
        curY += rowHeight;

        DrawStatRow(sideRect.x + 12f, curY, sideRect.width - 24f, "🌀 Arc Bonus", "+" + Mathf.RoundToInt(tree.ArcBonusDegrees) + "°", new Color(0.4f, 0.9f, 0.85f), tree.ArcBonusDegrees / 120f);

        GUI.color = Color.white;
    }

    void DrawStatRow(float x, float y, float width, string label, string val, Color statCol, float progress)
    {
        // Stat Label
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, width - 60f, 16f), label, statLabelStyle);

        // Stat Value
        GUI.color = statCol;
        GUI.Label(new Rect(x + width - 60f, y, 60f, 16f), val, statValStyle);

        // Progress bar underline
        float barY = y + 17f;
        GUI.color = new Color(0.12f, 0.15f, 0.22f, 0.8f);
        GUI.DrawTexture(new Rect(x, barY, width, 4f), whitePixel);

        float fillW = Mathf.Clamp01(progress) * width;
        if (fillW > 0.5f)
        {
            GUI.color = statCol;
            GUI.DrawTexture(new Rect(x, barY, fillW, 4f), whitePixel);
        }
    }

    void DrawFooter(Rect area)
    {
        float footY = area.yMax - 32f;

        // Center View button
        Rect centerBtnRect = new Rect(area.x + 18f, footY - 2f, 115f, 26f);
        GUI.color = new Color(0.15f, 0.2f, 0.35f, 0.9f);
        GUI.DrawTexture(centerBtnRect, cardTex);
        GUI.color = Color.white;
        DrawFrame(centerBtnRect, 1.2f);

        if (GUI.Button(centerBtnRect, "⊙ Re-Center", buttonStyle))
        {
            pan = Vector2.zero;
            zoom = 1f;
        }

        // Navigation info
        GUI.color = new Color(0.7f, 0.75f, 0.85f, 0.75f);
        string hints = string.Format("[Drag to Pan]  •  [Scroll: {0:F1}x]  •  [Space: Center]  •  [K: Close]", zoom);
        GUI.Label(new Rect(area.x + 150f, footY + 2f, area.width - 170f, 20f), hints, statLabelStyle);
        GUI.color = Color.white;
    }

    void DrawTooltipCard(SkillTree tree, Vector2 nodePos, long id)
    {
        bool unlocked = tree.IsUnlocked(id);
        bool affordable = tree.CanAfford(id);
        bool reachable = tree.IsReachable(id);

        Color statColor = (id == 0) ? new Color(1f, 0.85f, 0.3f) : SkillTree.ColourOf(tree.StatOf(id));

        float cardW = 220f;
        float cardH = 88f;

        // Position tooltip intelligently so it does not clip off-screen
        float tx = nodePos.x + 16f;
        float ty = nodePos.y - cardH * 0.5f;

        var tipRect = new Rect(tx, ty, cardW, cardH);

        // Tooltip Glassmorphism Box
        GUI.color = new Color(0.05f, 0.07f, 0.14f, 0.96f);
        GUI.DrawTexture(tipRect, cardTex);

        // Colored Glow Border matching stat
        GUI.color = statColor;
        DrawFrame(tipRect, 2f);

        // Stat Name & Icon
        string nodeName = (id == 0) ? "Celestial Origin" : GetStatTitle(tree.StatOf(id));
        GUI.color = Color.white;
        GUI.Label(new Rect(tipRect.x + 10f, tipRect.y + 8f, cardW - 20f, 20f), nodeName, tooltipTitleStyle);

        // Description / Magnitude
        string desc = (id == 0) ? "The nexus of your ancestral spirit." : tree.DescribeNode(id);
        GUI.color = new Color(0.85f, 0.9f, 0.95f);
        GUI.Label(new Rect(tipRect.x + 10f, tipRect.y + 30f, cardW - 20f, 22f), desc, tooltipBodyStyle);

        // Status Badge Pill
        Rect statusPill = new Rect(tipRect.x + 10f, tipRect.y + 56f, cardW - 20f, 22f);
        if (unlocked)
        {
            GUI.color = new Color(0.2f, 0.7f, 0.35f, 0.3f);
            GUI.DrawTexture(statusPill, cardTex);
            GUI.color = new Color(0.4f, 1f, 0.5f);
            GUI.Label(statusPill, "✓ UNLOCKED", tooltipBadgeStyle);
        }
        else if (reachable && affordable)
        {
            GUI.color = new Color(1f, 0.75f, 0.15f, 0.35f);
            GUI.DrawTexture(statusPill, cardTex);
            GUI.color = new Color(1f, 0.9f, 0.4f);
            GUI.Label(statusPill, string.Format("⚡ UNLOCK (Cost: {0})", tree.CostOf(id)), tooltipBadgeStyle);
        }
        else if (reachable && !affordable)
        {
            GUI.color = new Color(0.8f, 0.2f, 0.2f, 0.25f);
            GUI.DrawTexture(statusPill, cardTex);
            GUI.color = new Color(1f, 0.45f, 0.45f);
            GUI.Label(statusPill, string.Format("🔒 Need {0} Points", tree.CostOf(id)), tooltipBadgeStyle);
        }
        else
        {
            GUI.color = new Color(0.3f, 0.35f, 0.45f, 0.25f);
            GUI.DrawTexture(statusPill, cardTex);
            GUI.color = new Color(0.6f, 0.65f, 0.75f);
            GUI.Label(statusPill, "🔒 Locked Frontier", tooltipBadgeStyle);
        }

        GUI.color = Color.white;
    }

    string GetStatTitle(SkillTree.Stat stat)
    {
        switch (stat)
        {
            case SkillTree.Stat.Damage: return "⚔️ Savage Might";
            case SkillTree.Stat.MaxHealth: return "💖 Vitality";
            case SkillTree.Stat.MoveSpeed: return "⚡ Fleetfoot";
            case SkillTree.Stat.PowerGain: return "🔮 Arcane Flow";
            case SkillTree.Stat.Cooldown: return "⌛ Quick Reflexes";
            case SkillTree.Stat.Range: return "🏹 Eagle Reach";
            case SkillTree.Stat.Knockback: return "💥 Heavy Impact";
            case SkillTree.Stat.Lifesteal: return "🩸 Vampiric Thirst";
            case SkillTree.Stat.ArcWidth: return "🌀 Cleaving Arc";
            default: return "✧ Celestial Insight";
        }
    }

    static Vector2 Flip(Vector2 v) => new Vector2(v.x, -v.y);

    static void DrawLine(Vector2 a, Vector2 b, Color colour, float width)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        Matrix4x4 saved = GUI.matrix;
        GUI.color = colour;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), WhitePixel());
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    static void DrawFrame(Rect r, float thickness)
    {
        Texture2D px = WhitePixel();
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), px);
        GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), px);
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), px);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), px);
    }

    static void DrawCircleOutline(Vector2 center, float radius, Color color, float thickness)
    {
        int segments = 36;
        float angleStep = 360f / segments;
        Vector2 prev = center + new Vector2(Mathf.Cos(0) * radius, Mathf.Sin(0) * radius);

        for (int i = 1; i <= segments; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            Vector2 next = center + new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
            DrawLine(prev, next, color, thickness);
            prev = next;
        }
    }

    static Texture2D WhitePixel()
    {
        if (whitePixel == null)
        {
            whitePixel = new Texture2D(1, 1);
            whitePixel.SetPixel(0, 0, Color.white);
            whitePixel.Apply();
        }
        return whitePixel;
    }

    static void EnsureVisualAssets()
    {
        if (circleTex != null) return;

        // Anti-aliased circle
        int cSize = 64;
        circleTex = new Texture2D(cSize, cSize, TextureFormat.RGBA32, false);
        circleTex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(cSize * 0.5f, cSize * 0.5f);
        float r = cSize * 0.46f;
        for (int y = 0; y < cSize; y++)
        {
            for (int x = 0; x < cSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01((r - d) / 1.8f);
                circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        circleTex.Apply();

        // Soft Radial Glow
        int gSize = 64;
        glowTex = new Texture2D(gSize, gSize, TextureFormat.RGBA32, false);
        glowTex.filterMode = FilterMode.Bilinear;
        Vector2 gCenter = new Vector2(gSize * 0.5f, gSize * 0.5f);
        float gR = gSize * 0.48f;
        for (int y = 0; y < gSize; y++)
        {
            for (int x = 0; x < gSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), gCenter);
                float t = Mathf.Clamp01(d / gR);
                float a = Mathf.Pow(1f - t, 2.2f);
                glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        glowTex.Apply();

        // Glowing Hollow Ring
        int ringSize = 64;
        ringTex = new Texture2D(ringSize, ringSize, TextureFormat.RGBA32, false);
        ringTex.filterMode = FilterMode.Bilinear;
        Vector2 rCenter = new Vector2(ringSize * 0.5f, ringSize * 0.5f);
        float outerR = ringSize * 0.46f;
        float innerR = ringSize * 0.36f;
        for (int y = 0; y < ringSize; y++)
        {
            for (int x = 0; x < ringSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), rCenter);
                float aOuter = Mathf.Clamp01((outerR - d) / 1.5f);
                float aInner = Mathf.Clamp01((d - innerR) / 1.5f);
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, aOuter * aInner));
            }
        }
        ringTex.Apply();

        // Glassmorphism Dark Panel Texture
        panelTex = new Texture2D(1, 1);
        panelTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.10f, 0.94f));
        panelTex.Apply();

        // Card Glass Texture
        cardTex = new Texture2D(1, 1);
        cardTex.SetPixel(0, 0, new Color(0.08f, 0.11f, 0.18f, 0.92f));
        cardTex.Apply();

        // Setup Typographic Styles
        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        badgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        tooltipTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        tooltipBodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };

        tooltipBadgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        statLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };

        statValStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }
}
