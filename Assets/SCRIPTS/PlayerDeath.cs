using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Handles a player hitting zero health: locks out control, fades them out, and
/// respawns them after a delay. Players are never despawned, so they keep their
/// NetworkObject and ownership across a death.
[RequireComponent(typeof(Health))]
public class PlayerDeath : NetworkBehaviour
{
    public float respawnSeconds = 5f;
    public bool respawnAtStart = true;      // otherwise respawn where you fell

    readonly NetworkVariable<bool> dead = new NetworkVariable<bool>();

    public bool IsDead => dead.Value;

    Health health;
    SpriteRenderer sprite;
    Rigidbody2D rb;
    Vector3 startPosition;

    void Awake()
    {
        health = GetComponent<Health>();
        sprite = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        startPosition = transform.position;

        if (IsServer) health.OnDied += ServerHandleDeath;
        dead.OnValueChanged += HandleDeadChanged;

        ApplyVisual(dead.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && health != null) health.OnDied -= ServerHandleDeath;
        dead.OnValueChanged -= HandleDeadChanged;
    }

    void ServerHandleDeath()
    {
        if (!IsServer || dead.Value) return;

        dead.Value = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnSeconds);

        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) yield break;

        if (respawnAtStart) TeleportRpc(startPosition);

        health.ServerRevive();
        dead.Value = false;
    }

    /// Owner moves its own transform, so the server has to ask rather than set it.
    [Rpc(SendTo.Owner)]
    void TeleportRpc(Vector3 position)
    {
        transform.position = position;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void HandleDeadChanged(bool previous, bool now)
    {
        ApplyVisual(now);
    }

    void ApplyVisual(bool isDead)
    {
        if (sprite == null) return;

        Color c = sprite.color;
        c.a = isDead ? 0.35f : 1f;
        sprite.color = c;
    }
}
