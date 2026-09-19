using Unity.Netcode;
using UnityEngine;

/// Hold to raise the shield. Blocking is a replicated state rather than a local
/// flag, because damage is applied on the server - if only your own client knew
/// you were guarding, the server would happily kill you through it.
///
/// The owner drives the animator directly so the shield comes up with no
/// round-trip; NetworkAnimator (owner authority on the player) replicates that
/// to everyone else.
public class GuardAbility : NetworkBehaviour
{
    [Header("Input")]
    public bool useRightMouse = true;
    public KeyCode key = KeyCode.LeftShift;

    [Header("Rules")]
    [Tooltip("Off: only damage arriving within frontArcDegrees of your facing is " +
         "blocked, so you can be flanked.")]
    public bool blockAllDirections = true;
    public float frontArcDegrees = 180f;

    [Tooltip("How much of your speed you keep while guarding. 0 roots you.")]
    [Range(0f, 1f)]
    public float moveSpeedMultiplier = 0.35f;

    [Header("Feedback")]
    public float blockShake = 0.12f;

    readonly NetworkVariable<bool> blocking = new NetworkVariable<bool>();

    public bool IsBlocking => blocking.Value;

    Animator anim;
    PlayerDeath death;
    Movement movement;
    bool lastSent;

    void Awake()
    {
        anim = GetComponent<Animator>();
        death = GetComponent<PlayerDeath>();
        movement = GetComponent<Movement>();
    }

    void Update()
    {
        if (!IsOwner) return;

        bool wants = !(death != null && death.IsDead)
                     && ((useRightMouse && Input.GetMouseButton(1)) || Input.GetKey(key));

        // Local animation is immediate; the server copy follows.
        if (anim != null) anim.SetBool("Guard", wants);

        if (wants != lastSent)
        {
            lastSent = wants;
            SetBlockingRpc(wants);
        }
    }

    [Rpc(SendTo.Server)]
    void SetBlockingRpc(bool value)
    {
        blocking.Value = value;
    }

    /// Server-side. Called by Health before applying damage.
    public bool ServerBlocks(Vector2 fromDirection)
    {
        if (!blocking.Value) return false;
        if (blockAllDirections) return true;

        // fromDirection points from the attacker toward us, so the shield faces
        // the opposite way.
        Vector2 facing = new Vector2(movement != null ? movement.facingDirection : 1, 0f);
        return Vector2.Angle(facing, -fromDirection) <= frontArcDegrees * 0.5f;
    }

    /// Server-side. Lets the blocker feel the impact they just absorbed.
    public void ServerNotifyBlocked()
    {
        if (blockShake > 0f) BlockedRpc();
    }

    [Rpc(SendTo.Owner)]
    void BlockedRpc()
    {
        CameraFollow.Shake(blockShake);
    }
}
