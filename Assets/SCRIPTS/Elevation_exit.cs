using Unity.Netcode;
using UnityEngine;

/// Walking back down to the low ground. Mirror of Elevation_entre - see that
/// file for why sorting applies to everyone but colliders only to the local player.
public class Elevation_exit : MonoBehaviour
{
    public Collider2D[] mountaincolliders;
    public Collider2D[] boundarycolliders;
    public int sortingOrder = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Movement player = collision.GetComponentInParent<Movement>();
        if (player == null) return;

        SpriteRenderer sprite = player.body != null
            ? player.body
            : player.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.sortingOrder = sortingOrder;

        NetworkObject netObject = player.GetComponent<NetworkObject>();
        if (netObject != null && netObject.IsSpawned && !netObject.IsOwner) return;

        foreach (Collider2D mountain in mountaincolliders)
            if (mountain != null) mountain.enabled = true;

        foreach (Collider2D boundary in boundarycolliders)
            if (boundary != null) boundary.enabled = false;
    }
}
