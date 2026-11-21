using Unity.Netcode;
using UnityEngine;

/// Attach to any enemy. On death, has a configurable chance to
/// report an iron_ore "kill" to QuestManager, counting as a drop.
/// Works server-side only, like all quest credit.
[RequireComponent(typeof(Health))]
public class IronDrop : MonoBehaviour
{
    [Range(0f, 1f)]
    public float dropChance = 0.20f;   // 20%

    public string dropId = "iron_ore";

    void Start()
    {
        var hp = GetComponent<Health>();
        if (hp != null) hp.OnDied += OnDied;
    }

    void OnDestroy()
    {
        var hp = GetComponent<Health>();
        if (hp != null) hp.OnDied -= OnDied;
    }

    void OnDied()
    {
        // Only the server awards quest credit to avoid duplicate counting.
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (Random.value <= dropChance && QuestManager.Instance != null)
        {
            QuestManager.Instance.ServerReportKill(dropId);
            Debug.Log($"[Wanderers] Iron dropped from {gameObject.name}!");
        }
    }
}
