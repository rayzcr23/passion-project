using Unity.Netcode;
using UnityEngine;

/// Caps the session at 3 players. Requires "Connection Approval" to be ticked
/// on the NetworkManager, otherwise the callback is never invoked.
public class MaxPlayers : MonoBehaviour
{
    public int maxPlayers = 3;

    void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.ConnectionApprovalCallback += Approve;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.ConnectionApprovalCallback -= Approve;
    }

    void Approve(NetworkManager.ConnectionApprovalRequest request,
                 NetworkManager.ConnectionApprovalResponse response)
    {
        // The host occupies one slot, so this counts everyone already connected.
        response.Approved = NetworkManager.Singleton.ConnectedClientsIds.Count < maxPlayers;
        response.CreatePlayerObject = true;

        if (!response.Approved) response.Reason = "Game is full (" + maxPlayers + " players).";
    }
}
