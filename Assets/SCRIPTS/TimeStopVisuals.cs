using UnityEngine;

/// Local screen treatment for the shared Time Stop state. It never changes
/// Time.timeScale: the world freeze is controlled by the networked ability.
public sealed class TimeStopVisuals : MonoBehaviour
{
    static TimeStopVisuals instance;

    static readonly Color Cyan = new Color(0.48f, 0.94f, 1f);

    Texture2D clockRing;
    SpriteRenderer outerRune;
    SpriteRenderer innerRune;
    GUIStyle titleStyle;
    GUIStyle hintStyle;
    GUIStyle introStyle;
    bool active;
    float fade;
    float introTime;

    /// Called when the replicated Time Stop state changes on this client.
    public static void OnStateChanged(bool stopped)
    {
        if (instance == null)
        {
            if (!stopped) return;
            var go = new GameObject("[Time Stop Visuals]");
            instance = go.AddComponent<TimeStopVisuals>();
            DontDestroyOnLoad(go);
        }

        if (instance.active == stopped) return;
        instance.active = stopped;
        instance.introTime = stopped ? 1.7f : 0f;

        // The flash is a visual beat only. The brief hitstop used by other
        // abilities would fight this ability's unlimited-duration toggle.
        ImpactFrameEffect.TriggerCustom(
            stopped ? new Color(0.3f, 0.82f, 1f) : new Color(0.52f, 0.58f, 1f),
            stopped ? 0.27f : 0.17f,
            stopped ? 0.4f : 0.14f,
            false);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        clockRing = MakeClockRing();
        BuildCasterRunes();
    }

    void Update()
    {
        TimeStopWorld.RefreshAnimationFreeze();
        fade = Mathf.MoveTowards(fade, active ? 1f : 0f, Time.unscaledDeltaTime * 3.5f);
        if (introTime > 0f) introTime = Mathf.Max(0f, introTime - Time.unscaledDeltaTime);
        UpdateCasterRunes();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (clockRing != null) Destroy(clockRing);
    }

    void OnGUI()
    {
        if (fade <= 0.001f || Event.current.type != EventType.Repaint) return;

        // Draw below dialogue, touch controls and the multiplayer launcher.
        GUI.depth = 900;
        EnsureStyles();

        float width = Screen.width;
        float height = Screen.height;
        float t = Time.unscaledTime;
        Rect screen = new Rect(0f, 0f, width, height);

        bool hostileStop = HSirkBoss.IsTimeStopped;
        Color edgeColor = hostileStop ? new Color(1f, 0.37f, 0.13f) : new Color(0.27f, 0.88f, 1f);
        Tint(screen, hostileStop
            ? new Color(0.24f, 0.025f, 0.015f, 0.39f * fade)
            : new Color(0.025f, 0.065f, 0.19f, 0.31f * fade));

        // A cold rim, a few restrained scan lines, and drifting still-time motes.
        float rim = Mathf.Clamp(height * 0.007f, 3f, 7f);
        Tint(new Rect(0f, 0f, width, rim), new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.57f * fade));
        Tint(new Rect(0f, height - rim, width, rim), new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.57f * fade));
        Tint(new Rect(0f, 0f, rim, height), new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.38f * fade));
        Tint(new Rect(width - rim, 0f, rim, height), new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.38f * fade));

        for (int i = 0; i < 12; i++)
        {
            float y = Mathf.Repeat(i * 103.7f + t * 8f, height);
            Tint(new Rect(0f, y, width, 1f), new Color(0.3f, 0.8f, 1f, 0.018f * fade));
        }

        for (int i = 0; i < 24; i++)
        {
            float x = Hash(i * 2 + 1) * width;
            float y = Mathf.Repeat(Hash(i * 2 + 2) * height - t * (5f + i % 5), height);
            float size = 2f + i % 3;
            Tint(new Rect(x, y, size, size), new Color(0.53f, 0.94f, 1f, (0.32f + 0.2f * Mathf.Sin(t * 2f + i)) * fade));
        }

        // The two counter-rotating dials make the frozen scene feel charged
        // while remaining translucent enough to see hazards and teammates.
        float dialSize = Mathf.Min(Mathf.Min(width * 0.48f, height * 0.74f), 520f);
        Rect outer = new Rect((width - dialSize) * 0.5f, (height - dialSize) * 0.5f, dialSize, dialSize);
        Rect inner = new Rect(outer.x + dialSize * 0.16f, outer.y + dialSize * 0.16f,
                              dialSize * 0.68f, dialSize * 0.68f);
        RotatedTexture(outer, clockRing, t * 7f, new Color(0.42f, 0.87f, 1f, 0.20f * fade));
        RotatedTexture(inner, clockRing, -t * 13f, new Color(0.65f, 0.75f, 1f, 0.13f * fade));

        Vector2 centre = outer.center;
        float handAngle = t * 34f;
        RotatedBar(new Rect(centre.x - 2f, centre.y - dialSize * 0.31f, 4f, dialSize * 0.31f),
                   centre, handAngle, new Color(0.6f, 0.94f, 1f, 0.34f * fade));
        Tint(new Rect(centre.x - 5f, centre.y - 5f, 10f, 10f),
             new Color(0.79f, 0.98f, 1f, 0.50f * fade));

        // A stable status badge stays visible for an unlimited duration.
        float badgeWidth = Mathf.Min(width - 24f, 390f);
        Rect badge = new Rect((width - badgeWidth) * 0.5f, 3f, badgeWidth, 50f);
        Tint(badge, new Color(0.015f, 0.045f, 0.12f, 0.83f * fade));
        Tint(new Rect(badge.x, badge.y, badge.width, 2f), new Color(0.43f, 0.9f, 1f, 0.9f * fade));
        GUI.color = hostileStop ? new Color(1f, 0.67f, 0.35f, fade) : new Color(Cyan.r, Cyan.g, Cyan.b, fade);
        GUI.Label(new Rect(badge.x, badge.y + 3f, badge.width, 25f),
                  hostileStop ? "H-SIRK STOPPED TIME" : "TIME STOP ACTIVE", titleStyle);
        GUI.color = new Color(0.77f, 0.88f, 1f, fade);
        AbilityLoadout caster = TimeStopWorld.ActiveCaster;
        GUI.Label(new Rect(badge.x, badge.y + 27f, badge.width, 20f),
                  hostileStop ? "BRACE FOR THE FIREBALLS" :
                  caster != null && caster.IsOwner
                      ? string.Format("{0:F0}s LEFT  |  {1}", caster.TimeStopSecondsRemaining,
                          TouchControls.TouchModeActive ? "TAP TIME TO RESUME" : "PRESS T TO RESUME")
                      : "WAIT FOR TIME TO RESUME", hintStyle);

        if (introTime > 0f)
        {
            float introAlpha = Mathf.Min(1f, introTime * 1.3f) * fade;
            GUI.color = new Color(0.84f, 0.97f, 1f, introAlpha);
            float y = height * 0.63f;
            GUI.Label(new Rect(0f, y, width, 62f), "TIME HAS STOPPED", introStyle);
        }

        GUI.color = Color.white;
    }

    static void Tint(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    static void RotatedTexture(Rect rect, Texture2D texture, float angle, Color color)
    {
        if (texture == null) return;
        Matrix4x4 previous = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, rect.center);
        GUI.color = color;
        GUI.DrawTexture(rect, texture);
        GUI.matrix = previous;
    }

    static void RotatedBar(Rect rect, Vector2 pivot, float angle, Color color)
    {
        Matrix4x4 previous = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, pivot);
        Tint(rect, color);
        GUI.matrix = previous;
    }

    static float Hash(int seed)
    {
        float value = Mathf.Sin(seed * 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    void BuildCasterRunes()
    {
        Sprite rune = AuraVFXLibrary.GetRunicCircle();
        var outer = new GameObject("TimeStopOuterRune");
        outer.transform.SetParent(transform, false);
        outerRune = outer.AddComponent<SpriteRenderer>();
        outerRune.sprite = rune;
        outerRune.enabled = false;
        outer.transform.localScale = new Vector3(2.1f, 1.15f, 1f);

        var inner = new GameObject("TimeStopInnerRune");
        inner.transform.SetParent(transform, false);
        innerRune = inner.AddComponent<SpriteRenderer>();
        innerRune.sprite = rune;
        innerRune.enabled = false;
        inner.transform.localScale = new Vector3(1.25f, 0.7f, 1f);
    }

    void UpdateCasterRunes()
    {
        AbilityLoadout caster = TimeStopWorld.ActiveCaster;
        bool visible = caster != null && fade > 0.001f;
        outerRune.enabled = visible;
        innerRune.enabled = visible;
        if (!visible) return;

        Vector3 position = caster.transform.position;
        transform.position = new Vector3(position.x, position.y - 0.27f, position.z);
        SpriteRenderer body = caster.GetComponent<SpriteRenderer>();
        int order = body != null ? body.sortingOrder : 10;
        if (body != null)
        {
            outerRune.sortingLayerID = body.sortingLayerID;
            innerRune.sortingLayerID = body.sortingLayerID;
        }
        outerRune.sortingOrder = order - 2;
        innerRune.sortingOrder = order - 1;

        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3.5f);
        outerRune.color = new Color(0.34f, 0.85f, 1f, 0.65f * pulse * fade);
        innerRune.color = new Color(0.76f, 0.48f, 1f, 0.48f * pulse * fade);
        outerRune.transform.Rotate(0f, 0f, 18f * Time.unscaledDeltaTime);
        innerRune.transform.Rotate(0f, 0f, -29f * Time.unscaledDeltaTime);
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };
        titleStyle.normal.textColor = Color.white;
        hintStyle = new GUIStyle(titleStyle) { fontSize = 12 };
        introStyle = new GUIStyle(titleStyle) { fontSize = Mathf.Clamp(Screen.height / 22, 22, 40) };
    }

    static Texture2D MakeClockRing()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "TimeStopClockRing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        float middle = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - middle) / middle;
            float dy = (y - middle) / middle;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx);
            float outer = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.89f) * 190f);
            float inner = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.61f) * 110f) * 0.55f;
            float tick = Mathf.Abs(Mathf.Sin(angle * 12f)) > 0.975f && radius > 0.77f && radius < 0.96f
                ? 0.8f : 0f;
            float alpha = Mathf.Clamp01(Mathf.Max(outer, Mathf.Max(inner, tick)));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
