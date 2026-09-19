using UnityEngine;

/// Fires the projectile along AimInput's direction on a cooldown.
/// The cast animation is optional and guarded, so this works before you have drawn one.
public class RangedAttack : MonoBehaviour
{
    public AimInput aim;
    public AimPivot pivot;
    public Projectile projectilePrefab;
    public SpriteRenderer body;

    public float fireRate = 3f;             // shots per second
    public bool automatic = true;           // hold to keep firing

    [Header("Animation (optional)")]
    public Animator anim;
    public string castTrigger = "Cast";     // leave blank until a cast state exists

    float nextShotTime;

    void Reset()
    {
        aim = GetComponent<AimInput>();
        anim = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        pivot = GetComponentInChildren<AimPivot>();
    }

    void Awake()
    {
        if (aim == null) aim = GetComponent<AimInput>();
        if (anim == null) anim = GetComponent<Animator>();
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (pivot == null) pivot = GetComponentInChildren<AimPivot>();
    }

    void Update()
    {
        if (aim == null || projectilePrefab == null) return;

        bool wants = automatic ? (aim.FireHeld || aim.FirePressed) : aim.FirePressed;
        if (!wants || Time.time < nextShotTime) return;

        Fire();
    }

    void Fire()
    {
        nextShotTime = Time.time + 1f / Mathf.Max(fireRate, 0.01f);

        Transform spawn = (pivot != null && pivot.muzzle != null) ? pivot.muzzle : transform;
        Projectile shot = Instantiate(projectilePrefab, spawn.position, Quaternion.identity);
        shot.Launch(aim.AimDirection, body != null ? body.sortingOrder : 0);

        if (anim != null && !string.IsNullOrEmpty(castTrigger) && HasParameter(anim, castTrigger))
            anim.SetTrigger(castTrigger);
    }

    // The shared Warrior controller only declares Horizontal and Vertical. Checking first
    // keeps SetTrigger from spamming errors on characters with no cast state.
    static bool HasParameter(Animator animator, string parameterName)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == parameterName) return true;
        return false;
    }
}
