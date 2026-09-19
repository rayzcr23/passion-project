using UnityEngine;

/// Travels in a straight line and rotates to match its own heading, so a single
/// sprite covers every firing angle.
/// Prefab needs: Rigidbody2D (gravityScale 0) and a Collider2D with Is Trigger on.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    public float speed = 12f;
    public float lifetime = 3f;
    public int damage = 1;
    public LayerMask hitLayers = ~0;
    public GameObject impactEffect;

    Rigidbody2D rb;
    SpriteRenderer sprite;
    float age;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
    }

    /// sortingOrder comes from the shooter so shots respect the elevation layers.
    public void Launch(Vector2 direction, int sortingOrder)
    {
        direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;

        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        rb.linearVelocity = direction * speed;

        if (sprite != null) sprite.sortingOrder = sortingOrder;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // TODO: hand `damage` to a Health component once enemies exist.
        if (impactEffect != null) Instantiate(impactEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
