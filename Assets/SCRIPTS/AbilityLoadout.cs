using Unity.Netcode;
using UnityEngine;

/// Binds the three ability slots to input. Each class is just a prefab with
/// different Ability components dropped into these three fields.
public class AbilityLoadout : NetworkBehaviour
{
    public AimInput aim;

    [Header("Slots")]
    public Ability m1;
    public Ability special;
    public Ability super;

    [Header("Keys (desktop)")]
    public KeyCode specialKey = KeyCode.Q;
    public KeyCode superKey = KeyCode.E;

    void Awake()
    {
        if (aim == null) aim = GetComponent<AimInput>();
    }

    void Update()
    {
        if (!IsOwner || aim == null) return;

        Vector2 dir = aim.AimDirection;

        if (m1 != null && (aim.FireHeld || aim.FirePressed)) m1.TryActivate(dir);
        if (special != null && Input.GetKeyDown(specialKey)) special.TryActivate(dir);
        if (super != null && Input.GetKeyDown(superKey)) super.TryActivate(dir);
    }
}
