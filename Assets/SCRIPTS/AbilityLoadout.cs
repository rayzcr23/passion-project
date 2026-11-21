using Unity.Netcode;
using UnityEngine;

/// Binds ability slots to input:
/// [M1] / Click: Primary Weapon (Sword / Bow)
/// [Q]: Executioner's Leap (35 POW)
/// [F]: Glacial Burst (35 POW)
/// [E]: Blade Rain / Super (100 POW)
/// [Space] / Right Click: Guard
public class AbilityLoadout : NetworkBehaviour
{
    public AimInput aim;
    public PlayerDeath death;
    public Movement movement;
    public GuardAbility guard;

    [Header("Slots")]
    public Ability m1;
    public Ability special; // Executioner's Leap on [Q]
    public Ability super;   // Blade Rain on [E]

    [Header("Ranged Weapon")]
    public BowAbility bow;
    public bool hasBow;
    public bool isBowEquipped;
    public KeyCode weaponSwapKey = KeyCode.Tab;

    [Header("Ice Ability (Glacial Burst)")]
    public GlacialBurstAbility iceAbility;
    public bool hasIceAbility = true;
    public KeyCode iceAbilityKey = KeyCode.G;

    public enum AimMode
    {
        Facing,     // the way the sprite faces: left or right only
        Movement,   // the last direction you walked, full 2D
        Pointer     // at the cursor / touch point
    }

    [Header("Aim")]
    [Tooltip("Facing: the hitbox sits in front of the sprite, left or right. " +
         "Movement: swings the way you last walked, including up and down. " +
         "Pointer: aims at the cursor.")]
    public AimMode aimMode = AimMode.Facing;

    /// Exposed for the diagnostics overlay.
    public Vector2 LastAttackDirection { get; private set; } = Vector2.right;

    [Header("Keys (desktop)")]
    public KeyCode specialKey = KeyCode.Q;
    public KeyCode superKey = KeyCode.E;

    void Awake()
    {
        if (aim == null) aim = GetComponent<AimInput>();
        if (death == null) death = GetComponent<PlayerDeath>();
        if (movement == null) movement = GetComponent<Movement>();
        if (guard == null) guard = GetComponent<GuardAbility>();
        if (bow == null) bow = GetComponent<BowAbility>();
        if (iceAbility == null) iceAbility = GetComponent<GlacialBurstAbility>();
        if (iceAbility == null) iceAbility = gameObject.AddComponent<GlacialBurstAbility>();
        iceAbility.powerCost = 35f;
        iceAbility.displayName = "Glacial Burst";
        iceAbility.cooldown = 1.4f;
    }

    bool isSpecialCharging;
    public bool IsSpecialCharging => isSpecialCharging;

    void Update()
    {
        if (!IsOwner || aim == null) return;
        if (death != null && death.IsDead)
        {
            CancelCharge();
            return;
        }
        if (guard != null && guard.IsBlocking)
        {
            CancelCharge();
            return;   // shield up, no attacking
        }

        // Weapon swapping
        if (hasBow && (Input.GetKeyDown(weaponSwapKey) || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.R)))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) isBowEquipped = false;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) isBowEquipped = true;
            else isBowEquipped = !isBowEquipped;
        }

        Vector2 dir;
        switch (aimMode)
        {
            case AimMode.Pointer:
                dir = aim.HasAim ? aim.AimDirection : Vector2.right;
                break;

            case AimMode.Movement:
                dir = movement != null ? movement.LastMoveDirection : Vector2.right;
                break;

            default:    // Facing
                dir = new Vector2(movement != null ? movement.facingDirection : 1, 0f);
                break;
        }
        LastAttackDirection = dir;

        // When using the bow, aim directly at the mouse pointer!
        Ability activePrimary = (isBowEquipped && bow != null) ? (Ability)bow : m1;
        Vector2 attackDir = dir;
        if (isBowEquipped)
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                float zDepth = Mathf.Abs(cam.transform.position.z - transform.position.z);
                if (zDepth < 0.1f) zDepth = 10f;
                Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDepth));
                Vector2 diff = (Vector2)mouseWorld - (Vector2)transform.position;
                if (diff.sqrMagnitude > 0.001f)
                {
                    attackDir = diff.normalized;
                }
            }
            else if (aim != null && aim.HasAim)
            {
                attackDir = aim.AimDirection;
            }
        }

        bool mouseFiring = !TouchControls.TouchModeActive && Input.touchCount == 0 &&
                           (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0));
        bool isFiring = (aim != null && (aim.FireHeld || aim.FirePressed)) ||
                        mouseFiring ||
                        TouchControls.AttackHeld;

        if (activePrimary != null && isFiring)
            activePrimary.TryActivate(attackDir);

        // [Q] Executioner's Leap (35 POW)
        HandleSpecial(dir);

        // [F] Glacial Burst (35 POW)
        bool icePressed = Input.GetKeyDown(iceAbilityKey) || Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) || TouchControls.IcePressed;
        if (hasIceAbility && iceAbility != null && icePressed)
        {
            if (DialogueUI.Instance == null || !DialogueUI.Instance.IsOpen)
            {
                iceAbility.TryActivate(dir);
            }
        }

        // [E] Super (100 POW)
        if (super != null && (Input.GetKeyDown(superKey) || TouchControls.SuperPressed))
            super.TryActivate(dir);
    }

    public void UnlockBow()
    {
        if (bow == null)
        {
            bow = GetComponent<BowAbility>();
            if (bow == null) bow = gameObject.AddComponent<BowAbility>();
        }
        hasBow = true;
        isBowEquipped = true;
    }

    public void UnlockGlacialBurst()
    {
        if (iceAbility == null)
        {
            iceAbility = GetComponent<GlacialBurstAbility>();
            if (iceAbility == null) iceAbility = gameObject.AddComponent<GlacialBurstAbility>();
        }
        iceAbility.powerCost = 35f;
        hasIceAbility = true;
    }

    void HandleSpecial(Vector2 fallbackDir)
    {
        if (special == null) return;

        if (special.SupportsCharging)
        {
            Vector2 mouseWorld = aim != null ? aim.PointerWorldPosition : (Vector2)transform.position + fallbackDir * 5f;
            bool held = Input.GetKey(specialKey) || TouchControls.SpecialHeld;
            bool down = Input.GetKeyDown(specialKey) || TouchControls.SpecialPressed;

            if (!isSpecialCharging)
            {
                if (down && special.IsReady && special.CanAfford)
                {
                    isSpecialCharging = true;
                    special.OnChargeStart(mouseWorld);
                }
            }
            else
            {
                if (held)
                {
                    special.OnChargeUpdate(mouseWorld);
                }
                else
                {
                    isSpecialCharging = false;
                    special.OnChargeRelease(mouseWorld);
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(specialKey) || TouchControls.SpecialPressed)
                special.TryActivate(fallbackDir);
        }
    }

    void CancelCharge()
    {
        if (isSpecialCharging)
        {
            isSpecialCharging = false;
            if (special != null) special.OnChargeCancel();
        }
    }

    void OnDisable()
    {
        CancelCharge();
    }

    void OnGUI()
    {
        if (!IsOwner || (!hasBow && !hasIceAbility)) return;

        float width = (hasBow && hasIceAbility) ? 295f : 205f;
        GUILayout.BeginArea(new Rect(Screen.width - width - 10, Screen.height - 75, width, 65));
        GUILayout.BeginHorizontal("box");
        GUI.color = !isBowEquipped ? Color.cyan : Color.gray;
        if (GUILayout.Button("[1] Sword")) isBowEquipped = false;
        if (hasBow)
        {
            GUI.color = isBowEquipped ? Color.yellow : Color.gray;
            if (GUILayout.Button("[2] Bow 🏹")) isBowEquipped = true;
        }
        if (hasIceAbility && iceAbility != null)
        {
            bool ready = iceAbility.IsReady && iceAbility.CanAfford;
            GUI.color = ready ? new Color(0.35f, 0.9f, 1f) : Color.gray;
            string label;
            if (!iceAbility.CanAfford)
                label = "[G] Frost (35P)";
            else if (!iceAbility.IsReady)
                label = string.Format("[G] {0:F1}s", iceAbility.CooldownRemaining);
            else
                label = "[G] Frost ❄️";

            if (GUILayout.Button(label) && ready)
            {
                iceAbility.TryActivate(LastAttackDirection);
            }
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        string hint = hasIceAbility ? "<size=11><b>[Q]</b> Leap (35P)  <b>[G]</b> Glacial (35P)  <b>[E]</b> Super</size>" : "<size=11><b>[Tab]</b> or <b>[1]/[2]</b> to Swap Weapon</size>";
        GUILayout.Label(hint);
        GUILayout.EndArea();
    }
}
