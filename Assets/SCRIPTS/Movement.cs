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
    [Tooltip("On: body tracks the cursor constantly (twin-stick feel). Off: body " +
         "follows movement and only turns to the aim while attacking. Off is right " +
         "for keyboard movement - the camera lags behind you as you walk, so a " +
         "stationary cursor sits behind you and you would walk backwards.")]
    public bool faceAimAlways = false;
    public int facingDirection = 1;

    void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (aim == null) aim = GetComponent<AimInput>();
        if (death == null) death = GetComponent<PlayerDeath>();
        if (knockback == null) knockback = GetComponent<KnockbackReceiver>();
    }

    public override void OnNetworkSpawn()
    {
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

        anim.SetFloat("Horizontal", Mathf.Abs(input.x));
        anim.SetFloat("Vertical", Mathf.Abs(input.y));

        rb.linearVelocity = input * speed;

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

        // Only the body sprite flips. Negating the root scale, as this used to do, would
        // also mirror the AimPivot child and invert every aim angle.
        if (body != null) body.flipX = direction < 0;
    }

    public void Flip()
    {
        SetFacing(-facingDirection);
    }
}
