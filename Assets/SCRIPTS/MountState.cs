using Unity.Netcode;
using UnityEngine;

/// Riding the sheep the villagers gave you.
///
/// Two separate facts, both replicated: whether you OWN a mount, and whether you
/// are currently ON it. Owning it is a permanent reward; riding is a toggle, so
/// you can hop off to fight in a doorway without losing the sheep.
///
/// The mount is a visual child rather than a second networked object. The rider
/// already replicates a position, and a mount that never acts on its own does
/// not need one of its own.
public class MountState : NetworkBehaviour
{
    [Header("Ride")]
    public KeyCode toggleKey = KeyCode.R;
    public float speedMultiplier = 2.4f;

    [Header("Visual")]
    public Sprite[] mountFrames;
    public float framesPerSecond = 10f;
    public Vector3 mountOffset = new Vector3(0f, -0.45f, 0f);
    public float mountScale = 1.5f;

    readonly NetworkVariable<bool> owned = new NetworkVariable<bool>();
    readonly NetworkVariable<bool> riding = new NetworkVariable<bool>();

    public bool HasMount => owned.Value;
    public bool IsRiding => riding.Value;

    /// Movement multiplies by this, so there is one source of truth for speed.
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
        riding.OnValueChanged += (_, now) => ApplyVisual(now);
        ApplyVisual(riding.Value);
    }

    public override void OnNetworkDespawn()
    {
        ClearVisual();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Dying throws you off; no riding a sheep while face down.
        if (death != null && death.IsDead)
        {
            if (riding.Value) SetRidingRpc(false);
            return;
        }

        if (!owned.Value) return;

        if (Input.GetKeyDown(toggleKey)) SetRidingRpc(!riding.Value);
    }

    void LateUpdate()
    {
        // The mount is a separate renderer, so the body's flip does not reach it.
        // Runs on every client, since facing is replicated onto body.flipX.
        if (mountSprite != null && body != null) mountSprite.flipX = body.flipX;
    }

    // ------------------------------------------------------------ server side

    public void ServerGrant()
    {
        if (!IsServer) return;
        owned.Value = true;
    }

    [Rpc(SendTo.Server)]
    void SetRidingRpc(bool value)
    {
        if (!owned.Value) return;
        riding.Value = value;
    }

    [Rpc(SendTo.Server)]
    public void RequestGrantRpc()
    {
        ServerGrant();
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
        SpriteRenderer sr = mountSprite;
        if (body != null) sr.sortingOrder = body.sortingOrder - 1;   // under the rider

        if (mountFrames != null && mountFrames.Length > 0)
        {
            sr.sprite = mountFrames[0];

            var book = visual.AddComponent<SpriteFlipbook>();
            book.frames = mountFrames;
            book.framesPerSecond = framesPerSecond;
            book.loop = true;
            book.destroyWhenDone = false;
            book.fadeOutFraction = 0f;
        }

        // Deliberately does not move the rider. body is the SpriteRenderer on the
        // player ROOT, so nudging body.transform moves the player - and restoring
        // it on dismount teleported them back to wherever they climbed on. The
        // sheep sits lower via mountOffset instead.
    }

    void ClearVisual()
    {
        if (visual != null)
        {
            Destroy(visual);
            visual = null;
            mountSprite = null;
        }
    }
}
