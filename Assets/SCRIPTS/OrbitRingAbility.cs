using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// The Swordsman's Special: a ring of small swords orbiting him, damaging what
/// it touches for a few seconds.
///
/// Only the expiry time is replicated - one double. The blades themselves are
/// local objects every client builds for itself, so a spinning ring of six
/// swords costs the same bandwidth as a single number.
public class OrbitRingAbility : Ability
{
    [Header("Ring")]
    public Sprite bladeSprite;
    public int bladeCount = 6;
    public float radius = 1.4f;
    public float rotationSpeed = 220f;      // degrees per second
    public float duration = 6f;
    public float bladeScale = 0.6f;

    [Header("Damage")]
    public int damage = 5;
    public float bladeRadius = 0.35f;
    public LayerMask hitLayers = ~0;

    [Tooltip("Per target, so standing in the ring is a steady bleed rather than " +
         "being deleted on the first physics step.")]
    public float hitInterval = 0.6f;

    public bool friendlyFire = false;

    readonly NetworkVariable<double> activeUntil = new NetworkVariable<double>();
    readonly Dictionary<Health, double> nextHitTime = new Dictionary<Health, double>();
    readonly List<Transform> blades = new List<Transform>();

    SpriteRenderer body;
    float angle;

    public bool IsActive =>
        NetworkManager != null && NetworkManager.ServerTime.Time < activeUntil.Value;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<SpriteRenderer>();
    }

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        activeUntil.Value = NetworkManager.ServerTime.Time + duration;
        nextHitTime.Clear();
    }

    void FixedUpdate()
    {
        if (!IsServer || !IsActive) return;

        double now = NetworkManager.ServerTime.Time;
        Collider2D[] found = Physics2D.OverlapCircleAll(
            transform.position, radius + bladeRadius, hitLayers);

        foreach (Collider2D col in found)
        {
            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.gameObject == gameObject) continue;

            if (!friendlyFire && target.GetComponent<AbilityLoadout>() != null) continue;

            PlayerDeath downed = target.GetComponent<PlayerDeath>();
            if (downed != null && downed.IsDead) continue;

            double next;
            if (nextHitTime.TryGetValue(target, out next) && now < next) continue;

            nextHitTime[target] = now + hitInterval;

            Vector2 away = (Vector2)target.transform.position - (Vector2)transform.position;
            target.ServerTakeDamage(ScaleDamage(damage), OwnerClientId,
                away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right);
        }
    }

    void Update()
    {
        bool active = IsActive;

        if (active && blades.Count == 0) BuildBlades();
        else if (!active && blades.Count > 0) ClearBlades();

        if (!active) return;

        angle += rotationSpeed * Time.deltaTime;

        for (int i = 0; i < blades.Count; i++)
        {
            float a = (angle + i * 360f / blades.Count) * Mathf.Deg2Rad;
            blades[i].localPosition = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);

            // Point outward, so the blades read as flying rather than sliding.
            blades[i].localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
        }
    }

    void BuildBlades()
    {
        for (int i = 0; i < bladeCount; i++)
        {
            var go = new GameObject("Blade");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * bladeScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bladeSprite;
            if (body != null) sr.sortingOrder = body.sortingOrder + 2;

            blades.Add(go.transform);
        }
    }

    void ClearBlades()
    {
        foreach (Transform t in blades)
            if (t != null) Destroy(t.gameObject);

        blades.Clear();
    }

    public override void OnNetworkDespawn()
    {
        ClearBlades();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius + bladeRadius);
    }
}
