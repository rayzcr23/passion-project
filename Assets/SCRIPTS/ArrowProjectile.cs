using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Arrow shot by the player using the Bow.
/// High-speed straight-line projectile with client-side visual prediction,
/// continuous sweep collision to avoid tunneling, and server-authoritative damage.
/// Flies OVER water and terrain without getting blocked by water colliders!
[RequireComponent(typeof(Rigidbody2D), typeof(NetworkObject))]
public class ArrowProjectile : NetworkBehaviour
{
    public float speed = 22f;
    public float lifetime = 2.5f;
    public int damage = 25;
    public float knockback = 4.5f;
    public LayerMask hitLayers = ~0;
    public bool friendlyFire = false;

    readonly NetworkVariable<Vector2> heading = new NetworkVariable<Vector2>(Vector2.right);
    bool IsFrozenShot => TimeStopWorld.IsStopped;

    ulong shooterClientId;
    ulong shooterNetworkObjectId;
    Rigidbody2D rb;
    SpriteRenderer sprite;
    Collider2D col;
    float age;
    bool hasHit;

    static System.Collections.Generic.List<Collider2D> cachedWaterColliders;
    static float lastWaterCacheTime = -100f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (col != null)
        {
            col.isTrigger = true;
            IgnoreWaterColliders();
        }

        if (sprite != null)
        {
            sprite.sprite = AuraVFXLibrary.GetEnchantedArrow();
            sprite.sortingOrder = 32;
        }
    }

    GameObject trailObj;

    void Start()
    {
        // Attached luminous comet energy trail behind the arrow
        trailObj = new GameObject("ArrowEnergyTail");
        trailObj.transform.SetParent(transform, false);
        // Arrowhead is at x=+0.5, fletching is at x=-0.5, so trail extends backwards from x=-0.4
        trailObj.transform.localPosition = new Vector3(-0.45f, 0f, 0f);
        trailObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        trailObj.transform.localScale = new Vector3(0.85f, 2.2f, 1f);

        var trailSr = trailObj.AddComponent<SpriteRenderer>();
        trailSr.sprite = AuraVFXLibrary.GetCometStreak();
        trailSr.color = new Color(0.15f, 0.95f, 0.75f, 0.85f);
        trailSr.sortingOrder = (sprite != null ? sprite.sortingOrder : 32) - 1;

        StartCoroutine(FlightMotesRoutine());
    }

    System.Collections.IEnumerator FlightMotesRoutine()
    {
        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();
        Sprite ringSprite = AuraVFXLibrary.GetSonicBoomRing();
        float ringTimer = 0f;

        while (!hasHit)
        {
            yield return new WaitForSeconds(0.025f);
            if (hasHit) yield break;
            if (IsFrozenShot) continue;

            // Spawn drifting mana motes
            GameObject mote = new GameObject("ArrowMote");
            mote.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.12f);
            var mSr = mote.AddComponent<SpriteRenderer>();
            mSr.sprite = sparkSprite;
            mSr.color = Color.Lerp(Color.white, new Color(0.2f, 1f, 0.8f), Random.value);
            mSr.sortingOrder = 31;
            mote.AddComponent<ArrowMoteFader>().Init(0.22f, 0.35f);

            // Trailing sonic expansion ring
            ringTimer += 0.025f;
            if (ringTimer >= 0.12f)
            {
                ringTimer = 0f;
                GameObject r = new GameObject("ArrowFlightRing");
                r.transform.position = transform.position;
                r.transform.rotation = transform.rotation;
                var rSr = r.AddComponent<SpriteRenderer>();
                rSr.sprite = ringSprite;
                rSr.color = new Color(0.3f, 1f, 0.85f, 0.7f);
                rSr.sortingOrder = 30;
                r.AddComponent<ArrowRingFader>().Init(0.18f, 0.3f, 1.2f);
            }
        }
    }

    /// Ignores any water tilemaps or water colliders in the scene so the arrow flies freely over water
    void IgnoreWaterColliders()
    {
        if (col == null) return;

        if (cachedWaterColliders == null || Time.time - lastWaterCacheTime > 15f)
        {
            cachedWaterColliders = new System.Collections.Generic.List<Collider2D>();
            var allCols = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            foreach (var c in allCols)
            {
                if (c != null && c.gameObject.name.ToLower().Contains("water"))
                {
                    cachedWaterColliders.Add(c);
                }
            }
            lastWaterCacheTime = Time.time;
        }

        foreach (var c in cachedWaterColliders)
        {
            if (c != null)
            {
                Physics2D.IgnoreCollision(col, c, true);
            }
        }
    }

    /// Server only: called right after NetworkObject.Spawn()
    public void ServerLaunch(Vector2 direction, ulong shooter, int arrowDamage = 25, NetworkObject shooterObj = null)
    {
        shooterClientId = shooter;
        damage = arrowDamage;

        if (shooterObj != null)
        {
            shooterNetworkObjectId = shooterObj.NetworkObjectId;
            if (col != null)
            {
                var shooterCols = shooterObj.GetComponentsInChildren<Collider2D>();
                foreach (var sc in shooterCols)
                {
                    if (sc != null) Physics2D.IgnoreCollision(col, sc, true);
                }
            }
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out var client) && client.PlayerObject != null)
        {
            shooterNetworkObjectId = client.PlayerObject.NetworkObjectId;
            if (col != null)
            {
                var shooterCols = client.PlayerObject.GetComponentsInChildren<Collider2D>();
                foreach (var sc in shooterCols)
                {
                    if (sc != null) Physics2D.IgnoreCollision(col, sc, true);
                }
            }
        }

        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        heading.Value = dir;
        ApplyHeading(dir);
    }

    public override void OnNetworkSpawn()
    {
        heading.OnValueChanged += HandleHeadingChanged;
        ApplyHeading(heading.Value);
    }

    public override void OnNetworkDespawn()
    {
        heading.OnValueChanged -= HandleHeadingChanged;
    }

    void HandleHeadingChanged(Vector2 oldVal, Vector2 newVal)
    {
        ApplyHeading(newVal);
    }

    public void ApplyHeading(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        if (rb != null)
        {
            rb.linearVelocity = IsFrozenShot ? Vector2.zero : dir * speed;
        }
    }

    void Update()
    {
        if (!IsServer) return;
        if (IsFrozenShot) return;

        age += Time.deltaTime;
        if (age >= lifetime && !hasHit)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
        }
    }

    void FixedUpdate()
    {
        if (IsFrozenShot)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 dir = heading.Value;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

        if (rb != null)
        {
            rb.linearVelocity = dir * speed;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // Continuous sweep on server to avoid any tunneling through high-speed targets
        if (IsServer && !hasHit)
        {
            float stepDistance = speed * Time.fixedDeltaTime;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, 0.4f, dir, stepDistance, hitLayers);
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider != col && !IsWaterOrEnvironment(h.collider))
                {
                    CheckHit(h.collider);
                    if (hasHit) break;
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CheckHit(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckHit(collision.collider);
    }

    static bool IsWaterOrEnvironment(Collider2D c)
    {
        if (c == null) return true;
        // Entities with Health are NEVER water or environment!
        if (c.GetComponentInParent<Health>() != null) return false;

        string n = c.gameObject.name.ToLower();
        if (n.Contains("water") || n.Contains("pond") || n.Contains("river") || n.Contains("ocean")) return true;
        if (n.Contains("ground") || n.Contains("tilemap") || n.Contains("elevation") || n.Contains("bridge") || n.Contains("floor")) return true;
        if (c.GetComponentInParent<RideableMount>() != null) return true;
        return false;
    }

    static bool IsSolidWall(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        if (IsWaterOrEnvironment(c)) return false;

        string n = c.gameObject.name.ToLower();
        return n.Contains("wall") || n.Contains("cliff") || n.Contains("building") || n.Contains("house");
    }

    void CheckHit(Collider2D other)
    {
        if (!IsServer || hasHit || other == null || IsFrozenShot) return;

        // Skip ignored layers
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // Ignore shooter's own body
        NetworkObject hitNetObj = other.GetComponentInParent<NetworkObject>();
        if (hitNetObj != null)
        {
            if (shooterNetworkObjectId != 0 && hitNetObj.NetworkObjectId == shooterNetworkObjectId)
                return;

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out var client) &&
                client.PlayerObject != null && hitNetObj == client.PlayerObject)
            {
                return;
            }
        }

        // Check for damageable target with Health (Goblins, TNT Goblins, Giant Fish Boss, etc.)
        Health target = other.GetComponentInParent<Health>();
        if (target != null && !target.IsDead)
        {
            // Check if player themselves or teammates
            var loadout = other.GetComponentInParent<AbilityLoadout>();
            if (loadout != null && !friendlyFire) return;

            hasHit = true;
            target.ServerTakeDamage(damage, shooterClientId, heading.Value);

            // Apply knockback if applicable
            var receiver = target.GetComponent<KnockbackReceiver>();
            if (receiver != null && heading.Value.sqrMagnitude > 0.001f)
            {
                receiver.ServerApply(heading.Value, knockback);
            }

            PlayHitEffectRpc(transform.position, heading.Value);

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
            return;
        }

        // Water and terrain must NEVER block arrows
        if (IsWaterOrEnvironment(other)) return;

        if (IsSolidWall(other))
        {
            // Only despawn on true solid walls / buildings, NOT on water
            hasHit = true;
            PlayHitEffectRpc(transform.position, heading.Value);
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayHitEffectRpc(Vector3 pos, Vector2 dir)
    {
        CameraFollow.Shake(0.16f);

        // 1. Blinding core impact flash
        GameObject flash = new GameObject("ArrowHitFlash");
        flash.transform.position = pos;
        var fSr = flash.AddComponent<SpriteRenderer>();
        fSr.sprite = AuraVFXLibrary.GetGlowDisc();
        fSr.color = Color.white;
        fSr.sortingOrder = 36;
        flash.AddComponent<ArrowRingFader>().Init(0.14f, 0.4f, 1.8f);

        // 2. Sonic shockwave ring
        GameObject boom = new GameObject("ArrowHitBoom");
        boom.transform.position = pos;
        var bSr = boom.AddComponent<SpriteRenderer>();
        bSr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        bSr.color = new Color(0.2f, 1f, 0.8f, 0.9f);
        bSr.sortingOrder = 35;
        boom.AddComponent<ArrowRingFader>().Init(0.20f, 0.2f, 2.2f);

        // 3. Piercing spark stars erupting along the hit direction
        Sprite sparkSprite = AuraVFXLibrary.GetSparkStar();
        int count = 9;
        Vector2 normDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;

        for (int i = 0; i < count; i++)
        {
            GameObject sp = new GameObject("ArrowHitSpark");
            sp.transform.position = pos;

            var spSr = sp.AddComponent<SpriteRenderer>();
            spSr.sprite = sparkSprite;
            spSr.color = Color.Lerp(Color.white, new Color(0.15f, 1f, 0.8f), Random.value);
            spSr.sortingOrder = 37;

            Vector2 sparkDir = Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f)) * normDir;
            float speed = Random.Range(5f, 12f);
            sp.AddComponent<ArrowSparkMote>().Init(sparkDir.normalized * speed, Random.Range(0.18f, 0.28f));
        }
    }
}

public class ArrowMoteFader : MonoBehaviour
{
    float life;
    float maxScale;
    float elapsed;
    SpriteRenderer sr;

    public void Init(float lifetime, float scale)
    {
        life = lifetime;
        maxScale = scale;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (TimeStopWorld.IsStopped) return;
        elapsed += Time.deltaTime;
        float t = elapsed / life;
        transform.localScale = Vector3.one * (maxScale * (1f - t));
        if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
        if (elapsed >= life) Destroy(gameObject);
    }
}

public class ArrowRingFader : MonoBehaviour
{
    float life;
    float startScale;
    float endScale;
    float elapsed;
    SpriteRenderer sr;

    public void Init(float lifetime, float sStart, float sEnd)
    {
        life = lifetime;
        startScale = sStart;
        endScale = sEnd;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (TimeStopWorld.IsStopped) return;
        elapsed += Time.deltaTime;
        float t = elapsed / life;
        float s = Mathf.Lerp(startScale, endScale, Mathf.Pow(t, 0.55f));
        transform.localScale = new Vector3(s, s * 0.7f, 1f);
        if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.9f);
        if (elapsed >= life) Destroy(gameObject);
    }
}

public class ArrowSparkMote : MonoBehaviour
{
    Vector2 vel;
    float life;
    float elapsed;
    SpriteRenderer sr;

    public void Init(Vector2 velocity, float lifetime)
    {
        vel = velocity;
        life = lifetime;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (TimeStopWorld.IsStopped) return;
        elapsed += Time.deltaTime;
        transform.position += (Vector3)(vel * Time.deltaTime);
        float t = elapsed / life;
        transform.localScale = Vector3.one * (0.35f * (1f - t));
        if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
        if (elapsed >= life) Destroy(gameObject);
    }
}

