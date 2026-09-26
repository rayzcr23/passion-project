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

    [Tooltip("Force the controls on in the editor to lay them out without a device.")]
    public bool alwaysShow = false;

    [Header("Layout")]
    public float stickRadius = 90f;
    public float stickMargin = 130f;
    public float buttonSize = 74f;
    public float buttonGap = 12f;

    Vector2 stickCentre;
    Vector2 stickKnob;
    int stickFinger = -1;
    bool stickActive;

    // Button states for this frame.
    bool attack, special, super, guard, ride;
    bool specialConsumed, superConsumed, rideConsumed;

    public static bool Visible =>
        Instance != null && (Instance.alwaysShow || Input.touchSupported);

    /// Movement from the stick, or Vector2.zero when it is not being held.
    public static Vector2 Move
    {
        get
        {
            if (!Visible || Instance == null || !Instance.stickActive) return Vector2.zero;

            Vector2 delta = Instance.stickKnob - Instance.stickCentre;
            float length = delta.magnitude;
            if (length < 8f) return Vector2.zero;

            return delta / Instance.stickRadius * Mathf.Min(1f, length / Instance.stickRadius);
        }
    }

    public static bool AttackHeld => Visible && Instance != null && Instance.attack;
    public static bool GuardHeld => Visible && Instance != null && Instance.guard;

    /// One-shot reads, so a held finger fires an ability once rather than every frame.
    public static bool SpecialPressed => Visible && Instance != null && Instance.TakeSpecial();
    public static bool SuperPressed => Visible && Instance != null && Instance.TakeSuper();
    public static bool RidePressed => Visible && Instance != null && Instance.TakeRide();

    bool TakeSpecial() { if (!special || specialConsumed) return false; specialConsumed = true; return true; }
    bool TakeSuper() { if (!super || superConsumed) return false; superConsumed = true; return true; }
    bool TakeRide() { if (!ride || rideConsumed) return false; rideConsumed = true; return true; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
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
        bool wasSpecial = special, wasSuper = super, wasRide = ride;

        attack = special = super = guard = ride = false;

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
        }

        // Reset the one-shot latches when the finger lifts.
        if (!special && wasSpecial) specialConsumed = false;
        if (!super && wasSuper) superConsumed = false;
        if (!ride && wasRide) rideConsumed = false;

        if (!special) specialConsumed = false;
        if (!super) superConsumed = false;
        if (!ride) rideConsumed = false;
    }

    Rect[] ButtonRects()
    {
        float s = buttonSize;
        float g = buttonGap;
        float right = Screen.width - s - 30f;
        float bottom = Screen.height - s - 30f;

        return new[]
        {
            new Rect(right, bottom - s - g, s, s),                  // attack
            new Rect(right - s - g, bottom - s * 0.4f, s, s),       // special
            new Rect(right - (s + g) * 2f, bottom - s * 0.4f, s, s),// super
            new Rect(right, bottom, s, s),                          // guard
            new Rect(right - s - g, bottom - (s + g) * 2f, s, s)    // ride
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
            Vector2 c = new Vector2(stickCentre.x, Screen.height - stickCentre.y);
            Vector2 k = new Vector2(stickKnob.x, Screen.height - stickKnob.y);

            Vector2 delta = k - c;
            if (delta.magnitude > stickRadius) k = c + delta.normalized * stickRadius;

            Disc(c, stickRadius, new Color(1f, 1f, 1f, 0.10f));
            Disc(k, stickRadius * 0.38f, new Color(1f, 1f, 1f, 0.32f));
        }
        else
        {
            Disc(new Vector2(stickMargin, Screen.height - stickMargin),
                 stickRadius, new Color(1f, 1f, 1f, 0.07f));
        }

        Rect[] rects = ButtonRects();
        string[] names = { "ATK", "Q", "E", "DEF", "RIDE" };
        bool[] held = { attack, special, super, guard, ride };

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
        label.fontSize = 14;
        label.normal.textColor = Color.white;
    }
}
