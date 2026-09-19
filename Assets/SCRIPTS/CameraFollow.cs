using Unity.Netcode;
using UnityEngine;

/// Put this on the Main Camera. It finds whichever player belongs to THIS client
/// and follows them, so each Multiplayer Play Mode window shows its own character.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 0f, -10f);   // -10 keeps the 2D camera in front
    public float smoothTime = 0.15f;

    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null)
        {
            TryFindLocalPlayer();
            if (target == null) return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position, target.position + offset, ref velocity, smoothTime);
    }

    void TryFindLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || nm.SpawnManager == null) return;

        NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
        if (player != null) target = player.transform;
    }
}
