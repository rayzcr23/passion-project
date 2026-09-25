using Unity.Netcode;
using UnityEngine;

/// Put this on the Main Camera. It finds whichever player belongs to THIS client
/// and follows them, so each Multiplayer Play Mode window shows its own character.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 0f, -10f);   // -10 keeps the 2D camera in front
    public float smoothTime = 0.15f;

    [Tooltip("The camera may never fall further than this behind the player. " +
         "SmoothDamp alone cannot keep up with a sheep at 2.4x speed, and the " +
         "player drifts toward the edge of the screen.")]
    public float maxLag = 1.6f;

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

        Vector3 wanted = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position, wanted, ref velocity, smoothTime);

        // Hard leash, so speed can never outrun the smoothing.
        Vector3 behind = transform.position - wanted;
        behind.z = 0f;

        if (behind.magnitude > maxLag)
            transform.position = wanted + behind.normalized * maxLag;

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
