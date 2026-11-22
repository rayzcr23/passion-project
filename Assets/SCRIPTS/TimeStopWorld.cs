using System.Collections.Generic;
using UnityEngine;

/// Shared, replicated view of the Time Stop state. Each player loadout owns a
/// NetworkVariable; this registry reads those variables without changing Unity's
/// global time scale, so the caster, UI and Netcode continue to run.
public static class TimeStopWorld
{
    static readonly HashSet<AbilityLoadout> loadouts = new HashSet<AbilityLoadout>();
    static readonly Dictionary<Animator, float> frozenAnimators = new Dictionary<Animator, float>();
    static float nextAnimatorScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Reset()
    {
        loadouts.Clear();
        frozenAnimators.Clear();
        nextAnimatorScan = 0f;
    }

    public static void Register(AbilityLoadout loadout)
    {
        if (loadout != null) loadouts.Add(loadout);
    }

    public static void Unregister(AbilityLoadout loadout)
    {
        if (loadout != null) loadouts.Remove(loadout);
    }

    public static AbilityLoadout ActiveCaster
    {
        get
        {
            foreach (AbilityLoadout loadout in loadouts)
                if (loadout != null && loadout.IsSpawned && loadout.IsTimeStopActive)
                    return loadout;
            return null;
        }
    }

    public static bool IsStopped => ActiveCaster != null || HSirkBoss.IsTimeStopped;

    public static bool LocalUnlocked
    {
        get
        {
            foreach (AbilityLoadout loadout in loadouts)
                if (loadout != null && loadout.IsSpawned && loadout.IsOwner && loadout.TimeStopUnlocked)
                    return true;
            return false;
        }
    }

    public static bool IsFrozenPlayer(AbilityLoadout loadout)
    {
        AbilityLoadout caster = ActiveCaster;
        return loadout != null && (HSirkBoss.IsTimeStopped || (caster != null && loadout != caster));
    }

    /// Keep world characters in their exact pose while the clock is stopped.
    /// Player animators are managed by Movement so the caster stays animated.
    public static void RefreshAnimationFreeze()
    {
        if (!IsStopped)
        {
            foreach (var pair in frozenAnimators)
                if (pair.Key != null) pair.Key.speed = pair.Value;
            frozenAnimators.Clear();
            nextAnimatorScan = 0f;
            return;
        }

        if (Time.unscaledTime < nextAnimatorScan) return;
        nextAnimatorScan = Time.unscaledTime + 0.25f;

        AbilityLoadout caster = ActiveCaster;
        foreach (Animator animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (animator == null) continue;
            AbilityLoadout player = animator.GetComponentInParent<AbilityLoadout>();
            if (player == caster) continue;
            if (player != null)
            {
                Movement movement = player.GetComponent<Movement>();
                if (movement != null && movement.anim == animator) continue;
            }
            if (!frozenAnimators.ContainsKey(animator))
                frozenAnimators.Add(animator, animator.speed);
            animator.speed = 0f;
        }
    }
}
