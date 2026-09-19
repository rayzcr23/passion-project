using Unity.Netcode;
using UnityEngine;

/// Throwaway Host/Client buttons for the netcode spike. Uses OnGUI so it needs no
/// Canvas. Replace with a real lobby screen once connection is proven.
public class NetworkHUD : MonoBehaviour
{
    void OnGUI()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        GUILayout.BeginArea(new Rect(12f, 12f, 180f, 160f));

        if (!nm.IsClient && !nm.IsServer)
        {
            if (GUILayout.Button("Host")) nm.StartHost();
            if (GUILayout.Button("Client")) nm.StartClient();
        }
        else
        {
            GUILayout.Label(nm.IsHost ? "HOST" : (nm.IsServer ? "SERVER" : "CLIENT"));

            // ConnectedClientsIds is only populated on the server side.
            if (nm.IsServer) GUILayout.Label("Players: " + nm.ConnectedClientsIds.Count);

            if (GUILayout.Button("Disconnect")) nm.Shutdown();
        }

        GUILayout.EndArea();
    }
}
