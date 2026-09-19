using Unity.Netcode;
using UnityEngine;

/// Put this on the Main Camera. It finds whichever player belongs to THIS client
/// and follows them, so each Multiplayer Play Mode window shows its own character.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 0f, -10f);   // -10 keeps the 2D camera in front
    public float smoothTime = 0.15f;

    [Header("Shake")]
    public float maxShakeDistance = 0.35f;
    public float traumaDecay = 1.8f;

    /// The camera that is following THIS client's player, for shake calls.
    public static CameraFollow Active { get; private set; }

    Vector3 velocity;
    float trauma;

    void Awake() { Active = this; }
    void OnDestroy() { if (Active == this) Active = null; }

    /// 0-1. Squared on use, so small hits barely register and big ones land hard.
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    public static void Shake(float amount)
    {
        if (Active != null) Active.AddTrauma(amount);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            TryFindLocalPlayer();
            if (target == null) return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position, target.position + offset, ref velocity, smoothTime);

        if (trauma <= 0f) return;

        // Squared falloff reads better than linear - the shake dies off sharply
        // instead of lingering as a wobble.
        float shake = trauma * trauma * maxShakeDistance;
        transform.position += new Vector3(
            Random.Range(-1f, 1f) * shake,
            Random.Range(-1f, 1f) * shake,
            0f);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
    }

    void TryFindLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || nm.SpawnManager == null) return;

        NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
        if (player != null) target = player.transform;
    }
}
