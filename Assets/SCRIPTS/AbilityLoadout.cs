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
    [Tooltip("Off: abilities fire along the way you are facing and the mouse only " +
         "triggers them. On: abilities aim at the pointer.")]
    public bool usePointerAim = true;

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

        Vector2 facing = new Vector2(movement != null ? movement.facingDirection : 1, 0f);

        // Before the pointer has ever been read, aim is still the default right,
        // so swinging where you face beats swinging east forever.
        Vector2 dir = (usePointerAim && aim.HasAim) ? aim.AimDirection : facing;

        if (m1 != null && (aim.FireHeld || aim.FirePressed)) m1.TryActivate(dir);
        if (special != null && Input.GetKeyDown(specialKey)) special.TryActivate(dir);
        if (super != null && Input.GetKeyDown(superKey)) super.TryActivate(dir);
    }
}
