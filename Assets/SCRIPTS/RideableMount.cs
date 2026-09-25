using Unity.Netcode;
using UnityEngine;

/// The sheep standing in the village waiting to be claimed.
///
/// Walking up and taking it grants the mount permanently, then this object
/// stands down - it is a gift, not a parking space. The claim is replicated so
/// the sheep disappears for all three brothers at once rather than lingering as
/// a ghost on the other screens.
public class RideableMount : NetworkBehaviour
{
    public KeyCode claimKey = KeyCode.R;
    public float range = 1.8f;
    public Vector3 promptOffset = new Vector3(0f, 0.8f, 0f);

    [Tooltip("Everyone gets a sheep, or only the first to reach it.")]
    public bool grantToEveryone = true;

    [Tooltip("Stays invisible and untouchable until this quest is complete. Left " +
         "blank it is available immediately.

It hides itself rather than being " +
         "SetActive(false) by a QuestTrigger, because NGO does not spawn in-scene " +
         "NetworkObjects that start inactive - the claim would never reach the server.")]
    public string requiresQuestId = "";

    readonly NetworkVariable<bool> claimed = new NetworkVariable<bool>();

    SpriteRenderer sprite;
    Collider2D[] colliders;
    bool playerInRange;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        colliders = GetComponents<Collider2D>();
    }

    public override void OnNetworkSpawn()
    {
        claimed.OnValueChanged += (_, now) => ApplyClaimed(now);
        ApplyClaimed(claimed.Value);
    }

    bool Available
    {
        get
        {
            if (claimed.Value) return false;
            if (string.IsNullOrEmpty(requiresQuestId)) return true;

            QuestManager qm = QuestManager.Instance;
            return qm != null && qm.IsComplete(requiresQuestId);
        }
    }

    void Update()
    {
        ApplyClaimed(claimed.Value);

        if (!Available) return;

        MountState mine = LocalMount();
        playerInRange = mine != null &&
            (mine.transform.position - transform.position).sqrMagnitude <= range * range;

        if (!playerInRange || mine == null) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;

        if (Input.GetKeyDown(claimKey)) ClaimRpc();
    }

    [Rpc(SendTo.Server)]
    void ClaimRpc()
    {
        if (claimed.Value) return;

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var mount = client.PlayerObject.GetComponent<MountState>();
            if (mount != null) mount.ServerGrant();

            if (!grantToEveryone) break;
        }

        claimed.Value = true;
    }

    void ApplyClaimed(bool isClaimed)
    {
        bool visible = !isClaimed && (string.IsNullOrEmpty(requiresQuestId) ||
            (QuestManager.Instance != null && QuestManager.Instance.IsComplete(requiresQuestId)));

        if (sprite != null) sprite.enabled = visible;

        foreach (Collider2D c in colliders)
            if (c != null) c.enabled = visible;
    }

    static MountState LocalMount()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || nm.SpawnManager == null) return null;

        NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
        return player != null ? player.GetComponent<MountState>() : null;
    }

    void OnGUI()
    {
        if (!Available || !playerInRange || Camera.main == null) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;

        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + promptOffset);
        if (screen.z < 0f) return;

        var rect = new Rect(screen.x - 80f, Screen.height - screen.y - 14f, 160f, 26f);
        GUI.Label(rect, "[" + claimKey + "] Take the sheep", DialogueUI.CentredLabel);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0.5f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
