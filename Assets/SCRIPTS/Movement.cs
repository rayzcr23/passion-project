using UnityEngine;

public class Movement : MonoBehaviour
{
    public float speed;
    public Rigidbody2D rb;
    public Animator anim;
    public SpriteRenderer body;
    public AimInput aim;                    // optional: face where you shoot, not where you walk
    public bool faceAimAlways = true;       // false: only turn to the aim while actually firing
    public int facingDirection = 1;

    void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (aim == null) aim = GetComponent<AimInput>();
    }

    void FixedUpdate()
    {
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        // Diagonals used to come out ~41% faster than cardinals.
        if (input.sqrMagnitude > 1f) input.Normalize();

        anim.SetFloat("Horizontal", Mathf.Abs(input.x));
        anim.SetFloat("Vertical", Mathf.Abs(input.y));

        rb.velocity = input * speed;

        // Aim wins over movement: a wizard backing away left while shooting right faces right.
        // On desktop the cursor always has a position, so faceAimAlways decides whether the
        // body tracks it constantly or only mid-shot.
        bool useAim = aim != null && aim.HasAim && (faceAimAlways || aim.FireHeld || aim.FirePressed);
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
