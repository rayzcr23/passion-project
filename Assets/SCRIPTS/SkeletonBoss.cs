using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// The Witchcraft Village boss. The server chooses targets, moves the boss,
/// deals stomp damage, and launches its orb; clients play the same telegraphs.
[RequireComponent(typeof(NetworkObject), typeof(Health), typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SkeletonBoss : NetworkBehaviour
{
    enum State { Idle, Walking, Stomping, Defeated }

    [Header("Animation frames, in sheet order")]
    public Sprite[] idleFrames;
    public Sprite[] walkFrames;
    public Sprite[] stompFrames;

    [Header("Encounter")]
    public string bossName = "SKELETON, THE WITCHCRAFT GUARDIAN";
    public float bossScale = 4.2f;
    public float aggroRange = 20f;
    public float moveSpeed = 2.8f;
    public float attackRange = 3.2f;
    public float attackCooldown = 1.35f;

    [Header("Triple stomp")]
    public float stompRadius = 3.4f;
    public int stompDamage = 42;
    public float stompWindup = 0.75f;
    public float timeBetweenStomps = 0.28f;
    public float stompKnockback = 6f;

    [Header("Homing orb")]
    public SkeletonHomingOrb orbPrefab;
    public float orbAttackRange = 12f;

    readonly NetworkVariable<int> state = new NetworkVariable<int>((int)State.Idle);
    readonly NetworkVariable<bool> faceLeft = new NetworkVariable<bool>();

    Health health;
    Rigidbody2D body;
    SpriteRenderer sprite;
    DamageFlash flash;
    bool attackRunning;
    bool orbAttackNext;
    float nextAttackTime;
    float animationTime;
    float stompAnimationTime;

    void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        flash = GetComponent<DamageFlash>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.linearDamping = 8f;

        if (sprite != null)
        {
            sprite.sortingOrder = Mathf.Max(sprite.sortingOrder, 28);
            if (idleFrames != null && idleFrames.Length > 0) sprite.sprite = idleFrames[0];
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) body.bodyType = RigidbodyType2D.Kinematic;

        health.creatureId = "skeleton_boss";
        health.isBoss = true;
        if (IsServer) health.OnDied += ServerOnDied;

        transform.localScale = Vector3.one * bossScale;
        if (flash != null) flash.SetBaseScale(Vector3.one * bossScale);
        state.OnValueChanged += OnStateChanged;
        animationTime = 0f;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && health != null) health.OnDied -= ServerOnDied;
        state.OnValueChanged -= OnStateChanged;
    }

    void OnStateChanged(int oldState, int newState)
    {
        animationTime = 0f;
    }

    void Update()
    {
        if (sprite == null) return;

        sprite.flipX = faceLeft.Value;
        State current = (State)state.Value;
        Sprite[] frames = current == State.Stomping ? stompFrames :
                          current == State.Walking ? walkFrames : idleFrames;
        if (frames == null || frames.Length == 0) return;

        float fps = current == State.Stomping
            ? frames.Length / Mathf.Max(0.1f, stompWindup * 1.3f)
            : current == State.Walking ? 7f : 3f;

        if (current == State.Stomping)
        {
            stompAnimationTime += Time.deltaTime;
            int frame = Mathf.Min(Mathf.FloorToInt(stompAnimationTime * fps), frames.Length - 1);
            sprite.sprite = frames[frame];
        }
        else
        {
            animationTime += Time.deltaTime;
            sprite.sprite = frames[Mathf.FloorToInt(animationTime * fps) % frames.Length];
        }
    }

    void FixedUpdate()
    {
        if (!IsServer || health == null || health.IsDead) return;
        if (attackRunning)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Health target = FindClosestPlayer();
        if (target == null)
        {
            body.linearVelocity = Vector2.zero;
            state.Value = (int)State.Idle;
            return;
        }

        Vector2 toward = (Vector2)target.transform.position - (Vector2)transform.position;
        float distance = toward.magnitude;
        if (toward.sqrMagnitude > 0.001f) faceLeft.Value = toward.x < 0f;

        bool canStomp = distance <= attackRange;
        bool canFireOrb = orbAttackNext && orbPrefab != null && distance <= orbAttackRange;
        if (Time.time >= nextAttackTime && (canStomp || canFireOrb))
        {
            body.linearVelocity = Vector2.zero;
            if (canFireOrb) StartCoroutine(ServerOrbAttack(target));
            else StartCoroutine(ServerTripleStomp());
            return;
        }

        if (distance > attackRange * 0.8f)
        {
            body.linearVelocity = toward.normalized * moveSpeed;
            state.Value = (int)State.Walking;
        }
        else
        {
            body.linearVelocity = Vector2.zero;
            state.Value = (int)State.Idle;
        }
    }

    IEnumerator ServerTripleStomp()
    {
        attackRunning = true;
        body.linearVelocity = Vector2.zero;
        state.Value = (int)State.Stomping;

        for (int i = 0; i < 3; i++)
        {
            if (health.IsDead) yield break;
            Vector2 center = transform.position;
            StompWarningRpc(center, stompRadius, stompWindup);
            yield return new WaitForSeconds(stompWindup);
            if (health.IsDead) yield break;

            ServerDamageStomp(center);
            StompImpactRpc(center, stompRadius);
            if (i < 2) yield return new WaitForSeconds(timeBetweenStomps);
        }

        orbAttackNext = true;
        FinishAttack();
    }

    IEnumerator ServerOrbAttack(Health target)
    {
        attackRunning = true;
        body.linearVelocity = Vector2.zero;
        state.Value = (int)State.Stomping;

        Vector2 center = transform.position;
        StompWarningRpc(center, stompRadius, stompWindup);
        yield return new WaitForSeconds(stompWindup);
        if (health.IsDead) yield break;

        ServerDamageStomp(center);
        StompImpactRpc(center, stompRadius);
        yield return new WaitForSeconds(0.18f);
        if (health.IsDead) yield break;

        ServerLaunchOrb(target);
        orbAttackNext = false;
        FinishAttack();
    }

    void FinishAttack()
    {
        attackRunning = false;
        nextAttackTime = Time.time + attackCooldown;
        if (!health.IsDead) state.Value = (int)State.Idle;
    }

    void ServerDamageStomp(Vector2 center)
    {
        var damaged = new HashSet<Health>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, stompRadius))
        {
            Health victim = hit.GetComponentInParent<Health>();
            if (victim == null || victim.IsDead || !damaged.Add(victim)) continue;
            if (victim.GetComponent<AbilityLoadout>() == null) continue;

            Vector2 away = (Vector2)victim.transform.position - center;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
            victim.ServerTakeDamage(stompDamage, PowerMeter.NoAttacker, away.normalized);

            KnockbackReceiver knockback = victim.GetComponent<KnockbackReceiver>();
            if (knockback != null) knockback.ServerApply(away.normalized, stompKnockback);
        }
    }

    void ServerLaunchOrb(Health target)
    {
        if (orbPrefab == null) return;

        NetworkObject targetObject = target != null ? target.GetComponent<NetworkObject>() : null;
        Vector2 toward = targetObject != null
            ? (Vector2)(targetObject.transform.position - transform.position)
            : (faceLeft.Value ? Vector2.left : Vector2.right);
        if (toward.sqrMagnitude < 0.0001f) toward = Vector2.right;

        Vector3 spawnPosition = transform.position + (Vector3)(toward.normalized * 1.25f);
        SkeletonHomingOrb orb = Instantiate(orbPrefab, spawnPosition, Quaternion.identity);
        NetworkObject orbNetworkObject = orb.GetComponent<NetworkObject>();
        if (orbNetworkObject == null)
        {
            Debug.LogError("[SkeletonBoss] Orb prefab needs a NetworkObject.");
            Destroy(orb.gameObject);
            return;
        }

        orbNetworkObject.Spawn(true);
        orb.ServerLaunch(targetObject, toward.normalized);
    }

    Health FindClosestPlayer()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null) return null;

        Health closest = null;
        float bestDistance = aggroRange * aggroRange;
        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Health candidate = client.PlayerObject.GetComponent<Health>();
            if (candidate == null || candidate.IsDead) continue;

            float distance = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            closest = candidate;
        }
        return closest;
    }

    void ServerOnDied()
    {
        StopAllCoroutines();
        attackRunning = false;
        body.linearVelocity = Vector2.zero;
        state.Value = (int)State.Defeated;
        DefeatRpc(transform.position);
    }

    /// Called by the monk's encounter after the network object has spawned.
    public void PlayEmergence()
    {
        if (IsServer && IsSpawned) EmergenceRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void EmergenceRpc(Vector3 position)
    {
        var circle = SummoningCircleEffect.Spawn(position, 3.2f, 1.5f, 26);
        circle.primaryColor = new Color(0.7f, 0.35f, 1f);
        circle.secondaryColor = new Color(0.3f, 0.85f, 1f);
        CameraFollow.Shake(0.4f);
        BannerOverlay.Show("The Skeleton Guardian rises!");
    }

    [Rpc(SendTo.ClientsAndHost)]
    void StompWarningRpc(Vector2 center, float radius, float windup)
    {
        stompAnimationTime = 0f;
        var circle = SummoningCircleEffect.Spawn(center, radius, windup, 27);
        circle.primaryColor = new Color(0.7f, 0.25f, 1f);
        circle.secondaryColor = new Color(1f, 0.45f, 0.18f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void StompImpactRpc(Vector2 center, float radius)
    {
        ShockwaveEffect.Spawn(center, radius, 12, 38);
        SlamImpactEffect.Spawn(center, radius, 39,
            new Color(0.65f, 0.22f, 1f), new Color(1f, 0.75f, 0.3f));
        CameraFollow.Shake(0.35f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void DefeatRpc(Vector3 position)
    {
        SlamImpactEffect.Spawn(position, 3.5f, 40,
            new Color(0.55f, 0.28f, 0.9f), Color.white);
        CameraFollow.Shake(0.55f);
        BannerOverlay.Show("The Skeleton Guardian is defeated!");
    }
}
