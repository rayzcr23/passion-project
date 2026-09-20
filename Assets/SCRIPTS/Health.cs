using System;
using Unity.Netcode;
using UnityEngine;

/// Server-authoritative health. Only the server may change it; clients just watch
/// the NetworkVariable and react, which is what keeps everyone agreeing on who
/// is alive and stops damage from being faked client-side.
public class Health : NetworkBehaviour
{
    public int maxHealth = 100;

    [Tooltip("Bosses take the Super's boss percentage instead of the normal one.")]
    public bool isBoss = false;

    [Tooltip("What this creature counts as for Kill quest objectives. Blank means " +
         "it is not quest-relevant - players, for instance.")]
    public string creatureId = "";
    public bool despawnOnDeath = true;      // true for enemies, false for players

    [Tooltip("Server waits this long before removing the body, so DeathEffect has " +
         "time to play. 0 despawns immediately.")]
    public float despawnDelay = 0.6f;

    readonly NetworkVariable<int> current = new NetworkVariable<int>();

    GuardAbility guard;

    void Awake()
    {
        guard = GetComponent<GuardAbility>();
    }

    public int Current => current.Value;
    public int Max => maxHealth;
    public float Fraction => maxHealth <= 0 ? 0f : current.Value / (float)maxHealth;
    public bool IsDead => current.Value <= 0;

    /// (current, max) - for health bars.
    public event Action<int, int> OnChanged;
    public event Action OnDied;

    public override void OnNetworkSpawn()
    {
        if (IsServer) current.Value = maxHealth;

        current.OnValueChanged += HandleChanged;
        OnChanged?.Invoke(current.Value, maxHealth);
    }

    public override void OnNetworkDespawn()
    {
        current.OnValueChanged -= HandleChanged;
    }

    void HandleChanged(int previous, int now)
    {
        OnChanged?.Invoke(now, maxHealth);
        if (previous > 0 && now <= 0) OnDied?.Invoke();
    }

    /// Server only. Ignored elsewhere so a stray client call cannot deal damage.
    public void ServerTakeDamage(int amount, ulong attackerClientId)
    {
        ServerTakeDamage(amount, attackerClientId, Vector2.zero);
    }

    /// fromDirection points from the attacker toward this target, for directional
    /// blocking. Vector2.zero means "unknown", which an omnidirectional guard
    /// still stops.
    public void ServerTakeDamage(int amount, ulong attackerClientId, Vector2 fromDirection)
    {
        if (!IsServer || IsDead || amount <= 0) return;

        if (guard != null && guard.ServerBlocks(fromDirection))
        {
            guard.ServerNotifyBlocked();
            return;                          // fully absorbed
        }

        current.Value = Mathf.Max(0, current.Value - amount);

        PowerMeter.ServerAwardForDamage(attackerClientId, this, amount);

        if (current.Value == 0) ServerDie();
    }

    /// For the Swordsman's Super, which the PRD specifies as a percentage rather
    /// than a flat number.
    public void ServerTakePercentDamage(float percent, ulong attackerClientId)
    {
        if (!IsServer || IsDead || percent <= 0f) return;

        int amount = Mathf.Max(1, Mathf.RoundToInt(maxHealth * Mathf.Clamp01(percent)));
        ServerTakeDamage(amount, attackerClientId);
    }

    public void ServerHeal(int amount)
    {
        if (!IsServer || IsDead || amount <= 0) return;
        current.Value = Mathf.Min(maxHealth, current.Value + amount);
    }

    public void ServerRevive()
    {
        if (!IsServer) return;
        current.Value = maxHealth;
    }

    void ServerDie()
    {
        // Quest credit is party-wide, so it is reported once on the server rather
        // than by whoever landed the blow.
        if (!string.IsNullOrEmpty(creatureId) && QuestManager.Instance != null)
            QuestManager.Instance.ServerReportKill(creatureId);

        // Players stay in the world so they can be revived; enemies leave.
        if (!despawnOnDeath) return;

        if (despawnDelay > 0f) StartCoroutine(DespawnRoutine());
        else DespawnNow();
    }

    System.Collections.IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(despawnDelay);
        DespawnNow();
    }

    void DespawnNow()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}
