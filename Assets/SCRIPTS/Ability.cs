using Unity.Netcode;
using UnityEngine;

/// Base for every M1, Special and Super.
///
/// The flow is always the same: the owning client decides it wants to act, the
/// SERVER performs the act, and everyone sees the result. Damage and spawning
/// never happen on a client, or players could simply cheat or desync.
public abstract class Ability : NetworkBehaviour
{
    public string displayName = "Ability";
    public float cooldown = 1f;

    [Header("Animation (optional)")]
    public string animatorTrigger = "";

    float nextReadyTime;
    protected Animator anim;

    public bool IsReady => Time.time >= nextReadyTime;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);

    protected virtual void Awake()
    {
        anim = GetComponentInParent<Animator>();
    }

    /// Called by AbilityLoadout on the owning client only.
    public void TryActivate(Vector2 aimDirection)
    {
        if (!IsOwner || !IsReady) return;

        // Predicted locally so the cooldown UI reacts instantly; the server
        // re-checks nothing here because a stale cooldown is harmless in co-op PvE.
        nextReadyTime = Time.time + cooldown;

        ActivateServerRpc(aimDirection);
    }

    [Rpc(SendTo.Server)]
    void ActivateServerRpc(Vector2 aimDirection)
    {
        OnServerActivate(aimDirection);
        PlayEffectsRpc(aimDirection);
    }

    /// Runs on the server only. Spawn projectiles, apply damage, start buffs here.
    protected abstract void OnServerActivate(Vector2 aimDirection);

    /// Runs on every client. Cosmetic only - animation, sound, particles.
    [Rpc(SendTo.ClientsAndHost)]
    void PlayEffectsRpc(Vector2 aimDirection)
    {
        if (anim != null && !string.IsNullOrEmpty(animatorTrigger) && HasParameter(animatorTrigger))
            anim.SetTrigger(animatorTrigger);

        OnClientEffects(aimDirection);
    }

    protected virtual void OnClientEffects(Vector2 aimDirection) { }

    bool HasParameter(string parameterName)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == parameterName) return true;
        return false;
    }
}
