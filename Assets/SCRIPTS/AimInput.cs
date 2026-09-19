using Unity.Netcode;
using UnityEngine;

/// Single source of aim + fire input. Gameplay scripts read AimDirection/FireHeld
/// from here and never touch Input.mousePosition or Input.touches themselves, so
/// desktop and iPad share one code path.
public class AimInput : NetworkBehaviour
{
    [Header("Origin")]
    public Transform aimOrigin;             // aim is measured from here; defaults to this object

    [Header("Touch (iPad)")]
    [Range(0f, 1f)]
    public float aimZoneLeftEdge = 0f;      // fingers left of this screen fraction are ignored.
                                            // 0 = tap anywhere to aim, right for a hardware
                                            // keyboard. Raise to ~0.35 if you ever add an
                                            // on-screen movement stick on the left.
    public bool touchHoldToFire = true;     // keep firing while the finger is down

    [Header("Desktop")]
    public bool mouseHoldToFire = true;

    [Tooltip("World-space distance the pointer must be from the character before it " +
         "steers aim. Stops the direction thrashing when the cursor sits on you.")]
    public float aimDeadzone = 0.35f;

    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public bool FireHeld { get; private set; }
    public bool FirePressed { get; private set; }
    public bool HasAim { get; private set; }

    Camera cam;
    int activeTouchId = -1;
    bool warnedNoCamera;

    void Awake()
    {
        if (aimOrigin == null) aimOrigin = transform;
        cam = ResolveCamera();

        // On iOS every touch ALSO arrives as a fake mouse click, which would fire
        // each shot twice. Turn that off so the two paths stay separate.
        Input.simulateMouseWithTouches = false;
    }

    void Update()
    {
        // Only the player who owns this character reads their own device.
        // Without this, one person's keyboard would drive everybody's character.
        if (!IsOwner) return;

        if (cam == null) cam = ResolveCamera();
        FirePressed = false;

        if (Input.touchCount > 0) ReadTouch();
        else ReadMouse();
    }

    void ReadTouch()
    {
        // Claim a finger that began inside the aim zone.
        if (activeTouchId < 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                if (t.position.x / Screen.width < aimZoneLeftEdge) continue;

                activeTouchId = t.fingerId;
                PointAt(t.position);
                FirePressed = true;
                break;
            }
        }

        if (activeTouchId < 0)
        {
            FireHeld = false;
            return;
        }

        // Follow the claimed finger until it lifts, so drag re-aims.
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId != activeTouchId) continue;

            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) break;

            PointAt(t.position);
            FireHeld = touchHoldToFire;
            return;
        }

        activeTouchId = -1;
        FireHeld = false;
    }

    void ReadMouse()
    {
        activeTouchId = -1;
        PointAt(Input.mousePosition);                            // hover aims, no click needed
        if (Input.GetMouseButtonDown(0)) FirePressed = true;
        FireHeld = mouseHoldToFire && Input.GetMouseButton(0);
    }

    /// Camera.main matches on the MainCamera TAG, not the name. An untagged camera
    /// leaves aiming stuck at Vector2.right, which is silent and baffling - so fall
    /// back to any camera and warn once.
    Camera ResolveCamera()
    {
        Camera found = Camera.main;
        if (found != null) return found;

        found = FindFirstObjectByType<Camera>();

        if (found != null && !warnedNoCamera)
        {
            warnedNoCamera = true;
            Debug.LogWarning("[Wanderers] No camera tagged 'MainCamera'. Falling back to '" +
                             found.name + "'. Tag it MainCamera to silence this.", found);
        }
        return found;
    }

    void PointAt(Vector2 screenPos)
    {
        if (cam == null) return;

        float zDepth = Mathf.Abs(cam.transform.position.z - aimOrigin.position.z);
        if (zDepth < 0.1f) zDepth = 10f;
        Vector3 screenPos3 = new Vector3(screenPos.x, screenPos.y, zDepth);
        Vector2 world = cam.ScreenToWorldPoint(screenPos3);
        Vector2 delta = world - (Vector2)aimOrigin.position;

        // Pointer sitting on the character: keep the last good aim rather than
        // thrashing between directions from sub-pixel deltas.
        if (delta.sqrMagnitude < aimDeadzone * aimDeadzone) return;

        AimDirection = delta.normalized;
        HasAim = true;
    }
}
