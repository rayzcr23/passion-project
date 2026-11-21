using UnityEngine;

/// On-screen stick and ability buttons, for playing without a keyboard.
///
/// Every control the game reads goes through here as well as through the
/// keyboard, so gameplay scripts never branch on platform - they ask for a
/// movement vector and a set of button states, and this decides where those
/// came from.
///
/// Shown automatically when the device reports touch support, so the same build
/// serves a desktop and an iPad.
public class TouchControls : MonoBehaviour
{
    public static TouchControls Instance { get; private set; }
    static bool hasReceivedTouch;

    [Tooltip("Force the controls on in the editor to lay them out without a device.")]
    public bool alwaysShow = false;

    [Header("Layout")]
    public float stickRadius = 110f;
    public float stickMargin = 145f;
    public float buttonSize = 104f;
    public float buttonGap = 16f;

    Vector2 stickCentre;
    Vector2 stickKnob;
    int stickFinger = -1;
    bool stickActive;

    // Button states for this frame.
    bool attack, special, super, guard, ride, ice, interact;
    bool specialConsumed, superConsumed, rideConsumed, iceConsumed, interactConsumed;

    public static bool Visible =>
        Instance != null && (Instance.alwaysShow || Input.touchSupported);

    /// Once a real finger has touched the game, never trust WebGL's synthetic
    /// mouse button again during this play session. Some mobile browsers keep
    /// that button held specifically while no fingers are on the screen.
    public static bool TouchModeActive =>
        Input.touchCount > 0 || hasReceivedTouch;

    float LayoutScale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 1f, 1.35f);
    float EffectiveStickRadius => Mathf.Max(stickRadius, 110f) * LayoutScale;
    float EffectiveStickMargin => Mathf.Max(stickMargin, 145f) * LayoutScale;

    /// Movement from the stick, or Vector2.zero when it is not being held.
    public static Vector2 Move
    {
        get
        {
            if (!Visible || Instance == null || !Instance.stickActive) return Vector2.zero;

            Vector2 delta = Instance.stickKnob - Instance.stickCentre;
            float length = delta.magnitude;
            if (length < 8f) return Vector2.zero;

            float radius = Instance.EffectiveStickRadius;
            return delta / radius * Mathf.Min(1f, length / radius);
        }
    }

    public static bool AttackHeld => Visible && Instance != null && Instance.attack;
    public static bool GuardHeld => Visible && Instance != null && Instance.guard;

    /// One-shot reads, so a held finger fires an ability once rather than every frame.
    public static bool SpecialPressed => Visible && Instance != null && Instance.TakeSpecial();
    public static bool SpecialHeld => Visible && Instance != null && Instance.special;
    public static bool SuperPressed => Visible && Instance != null && Instance.TakeSuper();
    public static bool RidePressed => Visible && Instance != null && Instance.TakeRide();
    public static bool IcePressed => Visible && Instance != null && Instance.TakeIce();
    public static bool InteractPressed => Visible && Instance != null && Instance.TakeInteract();

    /// True if player is currently pressing or holding any virtual control (joystick or action buttons).
    public static bool IsAnyControlActive =>
        Visible && Instance != null && (Instance.stickActive || Instance.attack || Instance.special || Instance.super || Instance.guard || Instance.ride || Instance.ice || Instance.interact);

    /// Checks if a touch screen coordinate is over or near any virtual touch control.
    public static bool IsTouchOverControls(Vector2 touchPos)
    {
        if (!Visible || Instance == null) return false;

        // 1. Left joystick zone (left 48% of screen)
        if (touchPos.x <= Screen.width * 0.48f) return true;

        // 2. Action buttons on right side
        Vector2 guiPoint = new Vector2(touchPos.x, Screen.height - touchPos.y);
        Rect[] rects = Instance.ButtonRects();
        float padding = 25f;
        for (int i = 0; i < rects.Length; i++)
        {
            Rect padded = new Rect(
                rects[i].x - padding,
                rects[i].y - padding,
                rects[i].width + padding * 2f,
                rects[i].height + padding * 2f
            );
            if (padded.Contains(guiPoint)) return true;
        }

        return false;
    }

    bool TakeSpecial() { if (!special || specialConsumed) return false; specialConsumed = true; return true; }
    bool TakeSuper() { if (!super || superConsumed) return false; superConsumed = true; return true; }
    bool TakeRide() { if (!ride || rideConsumed) return false; rideConsumed = true; return true; }
    bool TakeIce() { if (!ice || iceConsumed) return false; iceConsumed = true; return true; }
    bool TakeInteract() { if (!interact || interactConsumed) return false; interactConsumed = true; return true; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (Input.touchCount > 0) hasReceivedTouch = true;
        if (!Visible) return;

        ReadStick();
        ReadButtons();
    }

    void ReadStick()
    {
        // The stick owns the left half of the screen, so a thumb can land anywhere
        // there rather than hunting for a fixed circle.
        if (stickFinger >= 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId != stickFinger) continue;

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) break;

                stickKnob = t.position;
                return;
            }

            stickFinger = -1;
            stickActive = false;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase != TouchPhase.Began) continue;
            if (t.position.x > Screen.width * 0.45f) continue;

            stickFinger = t.fingerId;
            stickCentre = t.position;
            stickKnob = t.position;
            stickActive = true;
            return;
        }
    }

    void ReadButtons()
    {
        bool wasSpecial = special, wasSuper = super, wasRide = ride, wasIce = ice, wasInteract = interact;

        attack = special = super = guard = ride = ice = interact = false;

        Rect[] rects = ButtonRects();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId == stickFinger) continue;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;

            // GUI space is flipped relative to touch space.
            var point = new Vector2(t.position.x, Screen.height - t.position.y);

            if (rects[0].Contains(point)) attack = true;
            if (rects[1].Contains(point)) special = true;
            if (rects[2].Contains(point)) super = true;
            if (rects[3].Contains(point)) guard = true;
            if (rects[4].Contains(point)) ride = true;
            if (rects.Length > 5 && rects[5].Contains(point)) ice = true;
            if (rects.Length > 6 && rects[6].Contains(point)) interact = true;
        }

        // Reset the one-shot latches when the finger lifts.
        if (!special && wasSpecial) specialConsumed = false;
        if (!super && wasSuper) superConsumed = false;
        if (!ride && wasRide) rideConsumed = false;
        if (!ice && wasIce) iceConsumed = false;
        if (!interact && wasInteract) interactConsumed = false;

        if (!special) specialConsumed = false;
        if (!super) superConsumed = false;
        if (!ride) rideConsumed = false;
        if (!ice) iceConsumed = false;
        if (!interact) interactConsumed = false;
    }

    Rect[] ButtonRects()
    {
        float scale = LayoutScale;
        float s = Mathf.Max(buttonSize, 104f) * scale;
        float g = Mathf.Max(buttonGap, 16f) * scale;
        float edge = 24f * scale;
        float right = Screen.width - s - edge;
        float bottom = Screen.height - s - edge;

        return new[]
        {
            new Rect(right, bottom - s - g, s, s),                  // attack
            new Rect(right - s - g, bottom - s * 0.4f, s, s),       // special (Q)
            new Rect(right - (s + g) * 2f, bottom - s * 0.4f, s, s),// super (E)
            new Rect(right, bottom, s, s),                          // guard
            new Rect(right - s - g, bottom - (s + g) * 2f, s, s),   // ride
            new Rect(right - (s + g) * 2f, bottom - (s + g) * 2f, s, s),// ice (G)
            new Rect(right - (s + g) * 3f, bottom - (s + g) * 1.2f, s * 1.15f, s) // talk
        };
    }

    static Texture2D pixel;
    static GUIStyle label;

    void OnGUI()
    {
        if (!Visible) return;

        EnsureStyle();

        if (stickActive)
        {
            float radius = EffectiveStickRadius;
            Vector2 c = new Vector2(stickCentre.x, Screen.height - stickCentre.y);
            Vector2 k = new Vector2(stickKnob.x, Screen.height - stickKnob.y);

            Vector2 delta = k - c;
            if (delta.magnitude > radius) k = c + delta.normalized * radius;

            Disc(c, radius, new Color(1f, 1f, 1f, 0.10f));
            Disc(k, radius * 0.38f, new Color(1f, 1f, 1f, 0.32f));
        }
        else
        {
            float margin = EffectiveStickMargin;
            Disc(new Vector2(margin, Screen.height - margin),
                 EffectiveStickRadius, new Color(1f, 1f, 1f, 0.07f));
        }

        Rect[] rects = ButtonRects();
        string[] names = { "ATK", "Q", "E", "DEF", "RIDE", "G", "TALK" };
        bool[] held = { attack, special, super, guard, ride, ice, interact };

        for (int i = 0; i < rects.Length; i++)
        {
            Disc(rects[i].center, rects[i].width * 0.5f,
                 held[i] ? new Color(1f, 0.85f, 0.4f, 0.45f) : new Color(1f, 1f, 1f, 0.16f));

            GUI.Label(rects[i], names[i], label);
        }
    }

    static void Disc(Vector2 centre, float radius, Color colour)
    {
        Color previous = GUI.color;
        GUI.color = colour;

        GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f),
                        Pixel());

        GUI.color = previous;
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

    static void EnsureStyle()
    {
        if (label != null) return;

        label = new GUIStyle(GUI.skin.label);
        label.alignment = TextAnchor.MiddleCenter;
        label.fontStyle = FontStyle.Bold;
        label.fontSize = 20;
        label.normal.textColor = Color.white;
    }
}
