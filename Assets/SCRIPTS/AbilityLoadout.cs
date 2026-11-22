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

    [Header("Time Stop")]
    public KeyCode timeStopKey = KeyCode.T;
    public const float TimeStopPowerCost = 100f;
    public const float TimeStopDuration = 10f;
    public const float TimeStopCooldown = 30f;
    readonly NetworkVariable<bool> hasTimeStop = new NetworkVariable<bool>();
    readonly NetworkVariable<bool> timeStopActive = new NetworkVariable<bool>();
    readonly NetworkVariable<double> timeStopEndsAt = new NetworkVariable<double>();
    readonly NetworkVariable<double> timeStopReadyAt = new NetworkVariable<double>();
    PowerMeter powerMeter;

    public bool TimeStopUnlocked => hasTimeStop.Value;
    public bool IsTimeStopActive => timeStopActive.Value;
    public float TimeStopSecondsRemaining => NetworkManager == null ? 0f :
        Mathf.Max(0f, (float)(timeStopEndsAt.Value - NetworkManager.ServerTime.Time));
    public float TimeStopCooldownRemaining => NetworkManager == null ? 0f :
        Mathf.Max(0f, (float)(timeStopReadyAt.Value - NetworkManager.ServerTime.Time));

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
        powerMeter = GetComponent<PowerMeter>();
        if (bow == null) bow = GetComponent<BowAbility>();
        if (iceAbility == null) iceAbility = GetComponent<GlacialBurstAbility>();
        if (iceAbility == null) iceAbility = gameObject.AddComponent<GlacialBurstAbility>();
        iceAbility.powerCost = 35f;
        iceAbility.displayName = "Glacial Burst";
        iceAbility.cooldown = 1.4f;
    }

    bool isSpecialCharging;
    public bool IsSpecialCharging => isSpecialCharging;

    public override void OnNetworkSpawn()
    {
        hasTimeStop.OnValueChanged += OnTimeStopUnlocked;
        timeStopActive.OnValueChanged += OnTimeStopActiveChanged;
        TimeStopWorld.Register(this);
        TimeStopVisuals.OnStateChanged(TimeStopWorld.IsStopped);
    }

    public override void OnNetworkDespawn()
    {
        hasTimeStop.OnValueChanged -= OnTimeStopUnlocked;
        timeStopActive.OnValueChanged -= OnTimeStopActiveChanged;
        TimeStopWorld.Unregister(this);
        TimeStopVisuals.OnStateChanged(TimeStopWorld.IsStopped);
    }

    void OnTimeStopActiveChanged(bool previous, bool current)
    {
        TimeStopVisuals.OnStateChanged(TimeStopWorld.IsStopped);
    }

    void OnTimeStopUnlocked(bool previous, bool current)
    {
        if (!IsOwner || !current || previous) return;
        BannerOverlay.Show("TIME STOP AWAKENED  |  100 POW  |  10 SECONDS");
        ImpactFrameEffect.TriggerCustom(new Color(0.43f, 0.83f, 1f), 0.3f, 0.45f, false);
    }

    void Update()
    {
        if (IsServer && timeStopActive.Value &&
            ((death != null && death.IsDead) || NetworkManager.ServerTime.Time >= timeStopEndsAt.Value))
            EndTimeStopServer();

        if (!IsOwner || aim == null) return;
        if (TimeStopWorld.IsFrozenPlayer(this))
        {
            CancelCharge();
            return;
        }

        // Handle Time Stop before guard input so the caster can release it.
        if (hasTimeStop.Value &&
            (Input.GetKeyDown(timeStopKey) || TouchControls.TimePressed) &&
            (DialogueUI.Instance == null || !DialogueUI.Instance.IsOpen))
            RequestTimeStopToggle();

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

    public void RequestUnlockTimeStop()
    {
        if (!IsOwner || !IsSpawned) return;
        RequestUnlockTimeStopRpc();
    }

    public void RequestHSirkAmbush()
    {
        if (!IsOwner || !IsSpawned) return;
        RequestHSirkAmbushRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestHSirkAmbushRpc()
    {
        HSirkEncounter hsirk = FindFirstObjectByType<HSirkEncounter>();
        if (hsirk == null || Vector2.Distance(transform.position, hsirk.transform.position) >
            hsirk.TalkRange + 1.5f) return;
        if (hsirk.ServerSummonGuards()) HSirkAmbushFxRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void HSirkAmbushFxRpc()
    {
        BannerOverlay.Show("H-SIRK'S GUARDS ATTACK!");
        CameraFollow.Shake(0.4f);
    }

    [Rpc(SendTo.Server)]
    void RequestUnlockTimeStopRpc()
    {
        QuestManager manager = QuestManager.Instance;
        WitchcraftMonkEncounter monk = FindFirstObjectByType<WitchcraftMonkEncounter>();
        if (manager == null || !manager.IsComplete("quest_skeleton_boss") || monk == null)
            return;
        if (Vector2.Distance(transform.position, monk.transform.position) > 4f) return;
        hasTimeStop.Value = true;
    }

    void RequestTimeStopToggle()
    {
        if (!IsOwner || !IsSpawned || !hasTimeStop.Value) return;
        RequestSetTimeStopRpc(!timeStopActive.Value);
    }

    [Rpc(SendTo.Server)]
    void RequestSetTimeStopRpc(bool active)
    {
        if (!hasTimeStop.Value) return;
        if (!active)
        {
            if (timeStopActive.Value) EndTimeStopServer();
            return;
        }

        if (timeStopActive.Value || (death != null && death.IsDead) ||
            NetworkManager.ServerTime.Time < timeStopReadyAt.Value || TimeStopWorld.IsStopped)
            return;
        if (powerMeter == null || !powerMeter.ServerTrySpend(TimeStopPowerCost)) return;

        timeStopEndsAt.Value = NetworkManager.ServerTime.Time + TimeStopDuration;
        timeStopActive.Value = true;
    }

    void EndTimeStopServer()
    {
        timeStopActive.Value = false;
        timeStopEndsAt.Value = 0d;
        timeStopReadyAt.Value = NetworkManager.ServerTime.Time + TimeStopCooldown;
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
        if (!IsOwner || TimeStopWorld.IsFrozenPlayer(this) ||
            (!hasBow && !hasIceAbility && !hasTimeStop.Value)) return;

        float width = hasTimeStop.Value ? 400f : (hasBow && hasIceAbility) ? 295f : 205f;
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
        if (hasTimeStop.Value)
        {
            bool canToggle = !TimeStopWorld.IsFrozenPlayer(this) &&
                (timeStopActive.Value || (TimeStopCooldownRemaining <= 0f &&
                 powerMeter != null && powerMeter.Has(TimeStopPowerCost)));
            GUI.color = canToggle ? new Color(0.4f, 0.95f, 1f) : Color.gray;
            GUI.enabled = canToggle;
            string timeLabel = timeStopActive.Value
                ? string.Format("[T] Resume {0:F0}s", TimeStopSecondsRemaining)
                : TimeStopCooldownRemaining > 0f
                    ? string.Format("[T] {0:F0}s", TimeStopCooldownRemaining)
                    : "[T] Time (100P)";
            if (GUILayout.Button(timeLabel))
                RequestTimeStopToggle();
            GUI.enabled = true;
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        string hint = hasIceAbility ? "<size=11><b>[Q]</b> Leap (35P)  <b>[G]</b> Glacial (35P)  <b>[E]</b> Super</size>" : "<size=11><b>[Tab]</b> or <b>[1]/[2]</b> to Swap Weapon</size>";
        GUILayout.Label(hint);
        GUILayout.EndArea();
    }
}
