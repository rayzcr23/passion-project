using Unity.Netcode;
using UnityEngine;

/// Put this on the Main Camera. It finds whichever player belongs to THIS client
/// and follows them, so each Multiplayer Play Mode window shows its own character.
/// Supports smooth zooming in and out via mouse scroll wheel, pinch gestures, and +/- keys.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 0f, -10f);   // -10 keeps the 2D camera in front
    public float smoothTime = 0.15f;

    [Tooltip("The camera may never fall further than this behind the player. " +
         "SmoothDamp alone cannot keep up with a sheep at 2.4x speed, and the " +
         "player drifts toward the edge of the screen.")]
    public float maxLag = 1.6f;

    [Header("Shake")]
    public float maxShakeDistance = 0.35f;
    public float traumaDecay = 1.8f;

    [Header("Zoom Controls")]
    [Tooltip("Master toggle for camera zooming.")]
    public bool allowZoom = true;

    [Tooltip("Allows mouse scroll wheel to zoom.")]
    public bool allowMouseScrollZoom = true;

    [Tooltip("If true, mouse scroll zoom requires holding Ctrl, Alt, or Cmd so normal mouse clicks and wheel touches during combat never trigger accidental zooming.")]
    public bool requireModifierForScroll = true;

    [Tooltip("Allows pinch-to-zoom gestures on touchscreens. Disabled by default during combat to prevent virtual joystick/buttons from triggering zooms.")]
    public bool allowTouchPinchZoom = false;

    [Tooltip("Allows +/- keys and NumPad +/- to zoom, and [0] or Middle Mouse Click to reset.")]
    public bool allowKeyboardZoom = true;

    [Tooltip("Closest zoom-in orthographic size.")]
    public float minSize = 2.8f;

    [Tooltip("Farthest zoom-out orthographic size.")]
    public float maxSize = 12.0f;

    [Tooltip("Default orthographic size.")]
    public float defaultSize = 5.0f;

    [Tooltip("Zoom sensitivity multiplier.")]
    public float zoomSpeed = 3.0f;

    [Tooltip("Smooth transition time for zoom changes.")]
    public float zoomSmoothTime = 0.08f;

    /// The camera that is following THIS client's player, for shake calls.
    public static CameraFollow Active { get; private set; }

    Vector3 velocity;
    float trauma;
    Camera cam;
    float targetSize;
    float zoomVelocity;

    void Awake()
    {
        Active = this;
        cam = GetComponent<Camera>();
        if (cam != null && cam.orthographic)
        {
            if (cam.orthographicSize > 0.1f)
                defaultSize = cam.orthographicSize;
            targetSize = defaultSize;
        }
        else
        {
            targetSize = defaultSize;
        }
    }

    /// Resets the camera zoom back to its default size.
    public void ResetZoom()
    {
        targetSize = defaultSize;
    }

    /// Static convenience method to reset the active camera zoom.
    public static void ResetCameraZoom()
    {
        if (Active != null) Active.ResetZoom();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    /// 0-1. Squared on use, so small hits barely register and big ones land hard.
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    public static void Shake(float amount)
    {
        if (Active != null) Active.AddTrauma(amount);
    }

    void Update()
    {
        HandleZoomInput();
    }

    void LateUpdate()
    {
        ApplyZoom();

        if (target == null)
        {
            TryFindLocalPlayer();
            if (target == null) return;
        }

        Vector3 wanted = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position, wanted, ref velocity, smoothTime);

        // Hard leash, so speed can never outrun the smoothing.
        Vector3 behind = transform.position - wanted;
        behind.z = 0f;

        if (behind.magnitude > maxLag)
            transform.position = wanted + behind.normalized * maxLag;

        if (trauma <= 0f) return;

        // Squared falloff reads better than linear - the shake dies off sharply
        // instead of lingering as a wobble.
        float shake = trauma * trauma * maxShakeDistance;
        transform.position += new Vector3(
            Random.Range(-1f, 1f) * shake,
            Random.Range(-1f, 1f) * shake,
            0f);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
    }

    void HandleZoomInput()
    {
        if (!allowZoom) return;

        // 1. Mouse Scroll Wheel
        if (allowMouseScrollZoom)
        {
            // If requireModifierForScroll is enabled, the player must hold Ctrl, Alt, or Command to zoom with the mouse wheel.
            // This prevents accidental wheel bumps, loose scrollwheels, or trackpad gestures from triggering zooms while fighting.
            bool modifierHeld = !requireModifierForScroll ||
                                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                                Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) ||
                                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            // Also ignore scroll wheel if player is actively clicking/attacking or using touch controls
            bool inCombatAction = Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                                 (TouchControls.Instance != null && TouchControls.IsAnyControlActive);

            if (modifierHeld && !inCombatAction)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    // Scrolling up (positive) -> zoom IN (smaller size)
                    // Scrolling down (negative) -> zoom OUT (larger size)
                    targetSize -= scroll * zoomSpeed * 1.5f;
                }
            }
        }

        // 2. Keyboard shortcuts (+ / - / NumPad / 0 to reset)
        if (allowKeyboardZoom)
        {
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
                targetSize -= zoomSpeed * Time.deltaTime * 3f;

            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                targetSize += zoomSpeed * Time.deltaTime * 3f;

            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0) || Input.GetMouseButtonDown(2))
                targetSize = defaultSize;
        }

        // 3. Touch Pinch-to-zoom (Mobile / iPad / Touchscreens)
        // Disabled by default (allowTouchPinchZoom = false) so holding the joystick and tapping attack
        // never triggers pinch zoom during gameplay.
        if (allowTouchPinchZoom && Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            // Never zoom if either touch is operating virtual controls (joystick or buttons)
            bool touchOverControls = TouchControls.IsAnyControlActive ||
                                     TouchControls.IsTouchOverControls(t0.position) ||
                                     TouchControls.IsTouchOverControls(t1.position);

            if (!touchOverControls)
            {
                // Only process when touches are actively moving, not during tap/release frames
                if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
                {
                    Vector2 prevPos0 = t0.position - t0.deltaPosition;
                    Vector2 prevPos1 = t1.position - t1.deltaPosition;

                    float prevDist = (prevPos0 - prevPos1).magnitude;
                    float curDist = (t0.position - t1.position).magnitude;

                    float delta = curDist - prevDist;
                    // Require a deadzone threshold to ignore accidental jitter
                    if (Mathf.Abs(delta) > 2.5f)
                    {
                        targetSize -= delta * 0.01f * zoomSpeed;
                    }
                }
            }
        }

        targetSize = Mathf.Clamp(targetSize, minSize, maxSize);
    }

    void ApplyZoom()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref zoomVelocity, zoomSmoothTime);
        }
        else
        {
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetSize * 12f, ref zoomVelocity, zoomSmoothTime);
        }
    }

    void TryFindLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || nm.SpawnManager == null) return;

        NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
        if (player != null) target = player.transform;
    }
}
