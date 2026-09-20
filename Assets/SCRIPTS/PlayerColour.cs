using Unity.Netcode;
using UnityEngine;

/// Gives each brother his own colour so three identical warriors are tellable
/// apart in a fight.
///
/// The server picks the lowest colour nobody is using - not the client id, which
/// keeps climbing as people rejoin and would hand the fourth ever connection a
/// colour that does not exist. The choice is replicated, so everyone sees the
/// same brother in the same colour.
public class PlayerColour : NetworkBehaviour
{
    [Tooltip("Override controllers built by Build Character Animations, in order.")]
    public RuntimeAnimatorController[] variants;

    readonly NetworkVariable<int> colourIndex = new NetworkVariable<int>(-1);

    public int Index => colourIndex.Value;

    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) colourIndex.Value = PickFreeColour();

        colourIndex.OnValueChanged += HandleChanged;
        Apply(colourIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        colourIndex.OnValueChanged -= HandleChanged;
    }

    void HandleChanged(int previous, int now)
    {
        Apply(now);
    }

    int PickFreeColour()
    {
        if (variants == null || variants.Length == 0) return 0;

        var taken = new bool[variants.Length];

        foreach (PlayerColour other in FindObjectsByType<PlayerColour>(FindObjectsSortMode.None))
        {
            if (other == this) continue;

            int i = other.colourIndex.Value;
            if (i >= 0 && i < taken.Length) taken[i] = true;
        }

        for (int i = 0; i < taken.Length; i++)
            if (!taken[i]) return i;

        return 0;      // more players than colours: share rather than fail
    }

    void Apply(int index)
    {
        if (anim == null || variants == null || variants.Length == 0) return;
        if (index < 0 || index >= variants.Length) return;
        if (variants[index] == null) return;

        // Swapping the controller resets state, so keep the pose we were in.
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        int hash = state.fullPathHash;
        float time = state.normalizedTime;

        anim.runtimeAnimatorController = variants[index];

        if (anim.isActiveAndEnabled && anim.gameObject.activeInHierarchy)
            anim.Play(hash, 0, time);
    }
}
