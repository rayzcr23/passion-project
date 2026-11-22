using Unity.Netcode;
using UnityEngine;

/// One registered network projectile supports H-sirk's homing volleys and
/// falling meteors. Only the server moves it and decides damage.
[RequireComponent(typeof(NetworkObject), typeof(SpriteRenderer))]
public sealed class HSirkFireball : NetworkBehaviour
{
    public float homingSpeed = 8.5f;
    public float turnRate = 260f;
    public float meteorSpeed = 17f;
    public int homingDamage = 38;
    public int meteorDamage = 55;
    public float impactRadius = 1.45f;

    readonly NetworkVariable<bool> meteor = new NetworkVariable<bool>();
    readonly NetworkVariable<Vector2> landing = new NetworkVariable<Vector2>();
    NetworkObject target;
    SpriteRenderer sprite;
    Sprite[] regularFrames;
    Sprite[] superFrames;
    Vector2 heading = Vector2.right;
    float age;
    float visualClock;
    bool resolved;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        regularFrames = Resources.LoadAll<Sprite>("HSirk/hsirk fireball");
        superFrames = Resources.LoadAll<Sprite>("HSirk/hsirk fireball super");
        System.Array.Sort(regularFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        System.Array.Sort(superFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        sprite.sortingOrder = 36;
        if (regularFrames.Length > 0) sprite.sprite = regularFrames[0];
        sprite.color = new Color(1f, 0.64f, 0.34f);
    }

    public void ServerLaunchHoming(NetworkObject player, Vector2 direction)
    {
        if (!IsServer) return;
        target = player;
        meteor.Value = false;
        heading = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
    }

    public void ServerLaunchMeteor(Vector2 point)
    {
        if (!IsServer) return;
        meteor.Value = true;
        landing.Value = point;
        heading = Vector2.down;
    }

    void Update()
    {
        if (TimeStopWorld.IsStopped) return;
        visualClock += Time.deltaTime;
        Sprite[] frames = meteor.Value ? superFrames : regularFrames;
        if (frames.Length > 0)
            sprite.sprite = frames[Mathf.FloorToInt(visualClock * (meteor.Value ? 13f : 10f)) % frames.Length];
        else if (sprite.sprite == null) sprite.sprite = AuraVFXLibrary.GetGlowDisc();
        sprite.color = meteor.Value ? new Color(1f, 0.46f, 0.16f) : new Color(1f, 0.64f, 0.34f);
        float pulse = 0.92f + Mathf.Sin(visualClock * 16f) * 0.08f;
        transform.localScale = Vector3.one * pulse * (meteor.Value ? 1.35f : 0.85f);
    }

    void FixedUpdate()
    {
        if (!IsServer || resolved || TimeStopWorld.IsStopped) return;
        age += Time.fixedDeltaTime;
        if (age > (meteor.Value ? 2f : 7f)) { NetworkObject.Despawn(); return; }

        if (meteor.Value)
        {
            Vector2 from = transform.position;
            Vector2 to = landing.Value;
            transform.position = Vector2.MoveTowards(from, to, meteorSpeed * Time.fixedDeltaTime);
            if (((Vector2)transform.position - to).sqrMagnitude < 0.015f) MeteorImpact(to);
            return;
        }

        if (target != null)
        {
            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && !targetHealth.IsDead)
            {
                Vector2 desired = (Vector2)target.transform.position - (Vector2)transform.position;
                if (desired.sqrMagnitude > 0.001f)
                {
                    heading = (Vector2)Vector3.RotateTowards(heading, desired.normalized,
                        turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                    heading.Normalize();
                }
            }
        }

        Vector2 start = transform.position;
        Vector2 step = heading * (homingSpeed * Time.fixedDeltaTime);
        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(start, 0.4f, heading, step.magnitude))
        {
            Health victim = hit.collider != null ? hit.collider.GetComponentInParent<Health>() : null;
            if (victim == null || victim.IsDead || victim.GetComponent<AbilityLoadout>() == null) continue;
            victim.ServerTakeDamage(homingDamage, PowerMeter.NoAttacker, heading);
            KnockbackReceiver knockback = victim.GetComponent<KnockbackReceiver>();
            if (knockback != null) knockback.ServerApply(heading, 4.5f);
            Resolve(hit.point);
            return;
        }
        transform.position = start + step;
    }

    void MeteorImpact(Vector2 point)
    {
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Health victim = client.PlayerObject.GetComponent<Health>();
            if (victim == null || victim.IsDead) continue;
            Vector2 away = (Vector2)victim.transform.position - point;
            if (away.sqrMagnitude > impactRadius * impactRadius) continue;
            victim.ServerTakeDamage(meteorDamage, PowerMeter.NoAttacker, away.normalized);
            KnockbackReceiver knockback = victim.GetComponent<KnockbackReceiver>();
            if (knockback != null) knockback.ServerApply(away.normalized, 5f);
        }
        Resolve(point);
    }

    void Resolve(Vector2 point)
    {
        if (resolved) return;
        resolved = true;
        ImpactRpc(point, meteor.Value);
        NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ImpactRpc(Vector2 point, bool wasMeteor)
    {
        float radius = wasMeteor ? 1.8f : 0.85f;
        SlamImpactEffect.Spawn(point, radius, 40,
            new Color(1f, 0.17f, 0.04f), new Color(1f, 0.83f, 0.18f));
        CameraFollow.Shake(wasMeteor ? 0.16f : 0.05f);
    }
}
