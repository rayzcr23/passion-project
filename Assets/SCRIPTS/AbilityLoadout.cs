using Unity.Netcode;
using UnityEngine;

/// Binds the three ability slots to input. Each class is just a prefab with
/// different Ability components dropped into these three fields.
public class AbilityLoadout : NetworkBehaviour
{
    public AimInput aim;
    public PlayerDeath death;
    public Movement movement;
    public GuardAbility guard;

    [Header("Slots")]
    public Ability m1;
    public Ability special;
    public Ability super;

    [Header("Keys (desktop)")]
    public enum AimMode
    {
        Facing,     // the way the sprite faces: left or right only
        Movement,   // the last direction you walked, full 2D
        Pointer     // at the cursor / touch point
    }

    [Tooltip("Facing: the hitbox sits in front of the sprite, left or right. " +
         "Movement: swings the way you last walked, including up and down. " +
         "Pointer: aims at the cursor.")]
    public AimMode aimMode = AimMode.Facing;

    /// Exposed for the diagnostics overlay.
    public Vector2 LastAttackDirection { get; private set; } = Vector2.right;

    public KeyCode specialKey = KeyCode.Q;
    public KeyCode superKey = KeyCode.E;

    void Awake()
    {
        if (aim == null) aim = GetComponent<AimInput>();
        if (death == null) death = GetComponent<PlayerDeath>();
        if (movement == null) movement = GetComponent<Movement>();
        if (guard == null) guard = GetComponent<GuardAbility>();
    }

    void Update()
    {
        if (!IsOwner || aim == null) return;
        if (death != null && death.IsDead) return;
        if (guard != null && guard.IsBlocking) return;   // shield up, no attacking

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

        if (m1 != null && (aim.FireHeld || aim.FirePressed)) m1.TryActivate(dir);
        if (special != null && Input.GetKeyDown(specialKey)) special.TryActivate(dir);
        if (super != null && Input.GetKeyDown(superKey)) super.TryActivate(dir);
    }
}
