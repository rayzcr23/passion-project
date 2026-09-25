using Unity.Netcode;
using UnityEngine;

/// A sheep that stands in the world until someone rides it, and stands wherever
/// they left it when they get off.
///
/// It is a place in the world, not an item you own: exactly one rider at a time,
/// and dismounting parks it where you stopped. Position lives in a
/// NetworkVariable rather than a NetworkTransform - it only moves on dismount,
/// so paying per-tick sync for something that stands still is waste.
public class RideableMount : NetworkBehaviour
{
    public const ulong NoRider = ulong.MaxValue;

    public float claimRange = 1.9f;
    public Vector3 promptOffset = new Vector3(0f, 0.9f, 0f);

    [Tooltip("Stays hidden until this quest is complete. Blank means available " +
         "immediately.")]
    public string requiresQuestId = "";

    readonly NetworkVariable<ulong> rider = new NetworkVariable<ulong>(NoRider);
    readonly NetworkVariable<Vector3> parkedAt = new NetworkVariable<Vector3>();

    public bool HasRider => rider.Value != NoRider;
    public ulong RiderClientId => rider.Value;

    SpriteRenderer sprite;
    Collider2D[] colliders;

    bool Unlocked
    {
        get
        {
            if (string.IsNullOrEmpty(requiresQuestId)) return true;
            QuestManager qm = QuestManager.Instance;
            return qm != null && qm.IsComplete(requiresQuestId);
        }
    }

    /// Free, unlocked, and standing here to be climbed on.
    public bool IsAvailable => Unlocked && !HasRider;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        colliders = GetComponents<Collider2D>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && parkedAt.Value == Vector3.zero)
            parkedAt.Value = transform.position;

        parkedAt.OnValueChanged += (_, now) => transform.position = now;
        if (parkedAt.Value != Vector3.zero) transform.position = parkedAt.Value;
    }

    void Update()
    {
        bool visible = Unlocked && !HasRider;

        if (sprite != null) sprite.enabled = visible;
        foreach (Collider2D c in colliders)
            if (c != null) c.enabled = visible;
    }

    // ------------------------------------------------------------ server side

    public bool ServerTryMount(ulong clientId)
    {
        if (!IsServer || HasRider || !Unlocked) return false;

        rider.Value = clientId;
        return true;
    }

    /// Parks the sheep where the rider stopped.
    public void ServerDismount(Vector3 position)
    {
        if (!IsServer) return;

        rider.Value = NoRider;
        parkedAt.Value = position;
        transform.position = position;
    }

    // ---------------------------------------------------------------- display

    void OnGUI()
    {
        if (!IsAvailable || Camera.main == null) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;

        MountState local = MountState.Local;
        if (local == null || local.IsRiding) return;

        float sqr = (local.transform.position - transform.position).sqrMagnitude;
        if (sqr > claimRange * claimRange) return;

        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + promptOffset);
        if (screen.z < 0f) return;

        var rect = new Rect(screen.x - 80f, Screen.height - screen.y - 14f, 160f, 26f);
        GUI.Label(rect, "[" + local.toggleKey + "] Ride", DialogueUI.CentredLabel);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0.5f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, claimRange);
    }
}
