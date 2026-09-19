using UnityEngine;

/// Rotates one unanimated weapon sprite through a full 360 degrees, so the
/// character needs no directional attack animations at all.
/// Put this on an empty child of the player and parent the staff/wand/gun under it.
public class AimPivot : MonoBehaviour
{
    public AimInput aim;
    public SpriteRenderer weapon;           // the single staff/wand sprite, no clips needed
    public SpriteRenderer body;             // player's body sprite, read for sorting order
    public Transform muzzle;                // where projectiles are born; a child of the weapon
    public int weaponSortingOffset = 1;     // drawn just in front of the body

    void Reset()
    {
        aim = GetComponentInParent<AimInput>();
        body = GetComponentInParent<SpriteRenderer>();
    }

    void Awake()
    {
        if (aim == null) aim = GetComponentInParent<AimInput>();
        if (body == null) body = GetComponentInParent<SpriteRenderer>();
    }

    // LateUpdate so this runs after Movement has settled the body for the frame.
    void LateUpdate()
    {
        if (aim == null) return;

        Vector2 dir = aim.AimDirection;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        if (weapon == null) return;

        // Rotating past vertical would leave the sprite upside down. Mirror it instead.
        weapon.flipY = dir.x < 0f;

        // The elevation triggers swap the body between sorting orders 1 and 15, so
        // track it live or the weapon disappears under the cliff on the high ground.
        if (body != null) weapon.sortingOrder = body.sortingOrder + weaponSortingOffset;
    }
}
