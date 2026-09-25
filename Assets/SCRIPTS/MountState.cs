using Unity.Netcode;
using UnityEngine;

/// Riding a sheep.
///
/// The sheep is a thing in the world, not a possession: you climb onto a
/// specific one, and getting off leaves it standing where you stopped. This
/// component only tracks whether THIS player is currently on one, and which.
///
/// While ridden the sheep is drawn as a child of the rider rather than moved as
/// its own networked object - the rider already replicates a position, so
/// syncing a second transform alongside it would be paying twice for one thing.
public class MountState : NetworkBehaviour
{
    /// The local player's mount state, for world objects that need to ask.
    public static MountState Local { get; private set; }

    [Header("Ride")]
    public KeyCode toggleKey = KeyCode.R;
    public float speedMultiplier = 2.2f;

    [Header("Visual")]
    public Sprite[] mountFrames;
    public float framesPerSecond = 10f;
    public Vector3 mountOffset = new Vector3(0f, -0.5f, 0f);
    public float mountScale = 1.8f;

    readonly NetworkVariable<bool> riding = new NetworkVariable<bool>();
    readonly NetworkVariable<ulong> mountObjectId = new NetworkVariable<ulong>();

    public bool IsRiding => riding.Value;
    public float SpeedScale => riding.Value ? speedMultiplier : 1f;

    GameObject visual;
    SpriteRenderer mountSprite;
    SpriteRenderer body;
    PlayerDeath death;

    void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        death = GetComponent<PlayerDeath>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;

        riding.OnValueChanged += (_, now) => ApplyVisual(now);
        ApplyVisual(riding.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this) Local = null;
        ClearVisual();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Dying throws you off, and leaves the sheep where you fell.
        if (death != null && death.IsDead)
        {
            if (riding.Value) DismountRpc(transform.position);
            return;
        }

        if (!Input.GetKeyDown(toggleKey)) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;

        if (riding.Value) DismountRpc(transform.position);
        else TryMountNearest();
    }

    void LateUpdate()
    {
        // The mount is its own renderer, so the body's flip never reaches it.
        if (mountSprite != null && body != null) mountSprite.flipX = body.flipX;
    }

    void TryMountNearest()
    {
        RideableMount best = null;
        float bestSqr = float.MaxValue;

        foreach (RideableMount m in FindObjectsByType<RideableMount>(FindObjectsSortMode.None))
        {
            if (!m.IsAvailable) continue;

            float sqr = (m.transform.position - transform.position).sqrMagnitude;
            if (sqr > m.claimRange * m.claimRange) continue;

            if (sqr < bestSqr) { bestSqr = sqr; best = m; }
        }

        if (best != null && best.NetworkObject != null)
            MountRpc(best.NetworkObject.NetworkObjectId);
    }

    // ------------------------------------------------------------ server side

    [Rpc(SendTo.Server)]
    void MountRpc(ulong targetId)
    {
        RideableMount mount = FindMount(targetId);
        if (mount == null || !mount.ServerTryMount(OwnerClientId)) return;

        mountObjectId.Value = targetId;
        riding.Value = true;
    }

    [Rpc(SendTo.Server)]
    void DismountRpc(Vector3 position)
    {
        if (!riding.Value) return;

        RideableMount mount = FindMount(mountObjectId.Value);
        if (mount != null) mount.ServerDismount(position);

        riding.Value = false;
    }

    static RideableMount FindMount(ulong objectId)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || nm.SpawnManager == null) return null;

        NetworkObject found;
        if (!nm.SpawnManager.SpawnedObjects.TryGetValue(objectId, out found)) return null;

        return found != null ? found.GetComponent<RideableMount>() : null;
    }

    // ---------------------------------------------------------------- visuals

    void ApplyVisual(bool isRiding)
    {
        if (isRiding) BuildVisual();
        else ClearVisual();
    }

    void BuildVisual()
    {
        if (visual != null) return;

        visual = new GameObject("Mount");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = mountOffset;
        visual.transform.localScale = Vector3.one * mountScale;

        mountSprite = visual.AddComponent<SpriteRenderer>();

        // Behind the rider on purpose - you sit on the sheep, not inside it.
        if (body != null) mountSprite.sortingOrder = body.sortingOrder - 1;

        if (mountFrames != null && mountFrames.Length > 0)
        {
            mountSprite.sprite = mountFrames[0];

            var book = visual.AddComponent<SpriteFlipbook>();
            book.frames = mountFrames;
            book.framesPerSecond = framesPerSecond;
            book.loop = true;
            book.destroyWhenDone = false;
            book.fadeOutFraction = 0f;
        }
    }

    void ClearVisual()
    {
        if (visual == null) return;

        Destroy(visual);
        visual = null;
        mountSprite = null;
    }
}
