using Unity.Netcode;
using UnityEngine;

/// Walking onto the high ground.
///
/// Two rules keep this working in multiplayer:
///   - Sorting order applies to EVERY player, so all clients draw each other at
///     the right depth.
///   - Collider toggling applies ONLY to the local player. These tilemap colliders
///     are local scene objects and physics is owner-authoritative, so each client
///     only needs its own player's collisions correct. Toggling them for remote
///     players would drop your walls because someone else climbed.
public class Elevation_entre : MonoBehaviour
{
    public Collider2D[] mountaincolliders;
    public Collider2D[] boundarycolliders;
    public int sortingOrder = 15;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Enemies and projectiles have rigidbodies too, and used to fire this.
        Movement player = collision.GetComponentInParent<Movement>();
        if (player == null) return;

        SpriteRenderer sprite = player.body != null
            ? player.body
            : player.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.sortingOrder = sortingOrder;

        NetworkObject netObject = player.GetComponent<NetworkObject>();
        if (netObject != null && netObject.IsSpawned && !netObject.IsOwner) return;

        foreach (Collider2D mountain in mountaincolliders)
            if (mountain != null) mountain.enabled = false;

        foreach (Collider2D boundary in boundarycolliders)
            if (boundary != null) boundary.enabled = true;
    }
}
