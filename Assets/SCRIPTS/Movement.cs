using Unity.Netcode;
using UnityEngine;

public class Movement : NetworkBehaviour
{
    public float speed;
    public Rigidbody2D rb;
    public Animator anim;
    public SpriteRenderer body;
    public AimInput aim;                    // optional: face where you shoot, not where you walk
    public PlayerDeath death;               // optional: set automatically if present
    public KnockbackReceiver knockback;     // optional: set automatically if present
    public GuardAbility guard;              // optional: set automatically if present
    [Tooltip("On: body tracks the cursor constantly (twin-stick feel). Off: body " +
         "follows movement and only turns to the aim while attacking. Off is right " +
         "for keyboard movement - the camera lags behind you as you walk, so a " +
         "stationary cursor sits behind you and you would walk backwards.")]
    public bool faceAimAlways = false;
    public int facingDirection = 1;

    /// Last non-zero movement direction, normalised. This is what a keyboard player
    /// means by "in front of me" - facingDirection is only left/right and cannot
    /// express up or down.
    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;

    // flipX is not covered by NetworkTransform or NetworkAnimator, so without this
    // every remote player permanently faces right.
    readonly NetworkVariable<bool> facingLeft = new NetworkVariable<bool>();

    void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (aim == null) aim = GetComponent<AimInput>();
        if (death == null) death = GetComponent<PlayerDeath>();
        if (knockback == null) knockback = GetComponent<KnockbackReceiver>();
        if (guard == null) guard = GetComponent<GuardAbility>();
    }

    public override void OnNetworkSpawn()
    {
        facingLeft.OnValueChanged += (_, now) => ApplyFlip(now);
        ApplyFlip(facingLeft.Value);

        // Remote copies are positioned by NetworkTransform, so their physics must not
        // fight it. Only the owner runs real dynamics.
        if (!IsOwner && rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        if (death != null && death.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            anim.SetFloat("Horizontal", 0f);
            anim.SetFloat("Vertical", 0f);
            return;
        }

        // Being knocked back: let physics carry you instead of assigning velocity,
        // which would cancel the impulse on the very next step.
        if (knockback != null && knockback.IsRecovering)
        {
            anim.SetFloat("Horizontal", 0f);
            anim.SetFloat("Vertical", 0f);
            return;
        }

        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        // Diagonals used to come out ~41% faster than cardinals.
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Quantised on purpose. GetAxis is smoothed, so raw magnitudes change every
        // single tick, and NetworkAnimator then streams two float updates per tick
        // per player. The controller only tests against 0.01, so 0/1 is all it needs.
        anim.SetFloat("Horizontal", Mathf.Abs(input.x) > 0.1f ? 1f : 0f);
        anim.SetFloat("Vertical", Mathf.Abs(input.y) > 0.1f ? 1f : 0f);

        float speedScale = (guard != null && guard.IsBlocking) ? guard.moveSpeedMultiplier : 1f;
        if (input.sqrMagnitude > 0.0001f) LastMoveDirection = input.normalized;

        rb.linearVelocity = input * speed * speedScale;

        // Facing follows movement. The pointer is consulted ONLY when faceAimAlways
        // is deliberately switched on - attacking must never yank the body around,
        // which read as a twitch toward the cursor on every swing.
        bool useAim = faceAimAlways && aim != null && aim.HasAim;
        float face = useAim ? aim.AimDirection.x : input.x;
        if (face > 0.01f) SetFacing(1);
        else if (face < -0.01f) SetFacing(-1);
    }

    public void SetFacing(int direction)
    {
        facingDirection = direction;
        ApplyFlip(direction < 0);

        // Writing only on change keeps this off the wire while you walk in a line.
        if (IsOwner && IsSpawned && facingLeft.Value != (direction < 0))
            SetFacingRpc(direction < 0);
    }

    [Rpc(SendTo.Server)]
    void SetFacingRpc(bool left)
    {
        facingLeft.Value = left;
    }

    void ApplyFlip(bool left)
    {
        // Only the body sprite flips. Negating the root scale, as this used to do, would
        // also mirror the AimPivot child and invert every aim angle.
        if (body != null) body.flipX = left;
    }

    public void Flip()
    {
        SetFacing(-facingDirection);
    }
}
