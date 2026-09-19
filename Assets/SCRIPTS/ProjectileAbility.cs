using Unity.Netcode;
using UnityEngine;

/// Covers the Wizard's fireballs and the Assassin's daggers - same component,
/// different prefab and numbers. Add it again with another prefab for a new one.
public class ProjectileAbility : Ability
{
    public Projectile projectilePrefab;
    public Transform muzzle;
    public int projectileCount = 1;          // >1 gives a spread, for free
    public float spreadDegrees = 0f;

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        if (projectilePrefab == null) return;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;

        for (int i = 0; i < projectileCount; i++)
        {
            Vector2 dir = aimDirection;

            if (projectileCount > 1 && spreadDegrees > 0f)
            {
                float t = projectileCount == 1 ? 0.5f : i / (float)(projectileCount - 1);
                float angle = Mathf.Lerp(-spreadDegrees * 0.5f, spreadDegrees * 0.5f, t);
                dir = Quaternion.Euler(0f, 0f, angle) * dir;
            }

            Projectile shot = Instantiate(projectilePrefab, origin, Quaternion.identity);

            // Spawn first so the NetworkObject exists, then hand it its heading -
            // clients read that in OnNetworkSpawn and simulate the flight themselves.
            shot.NetworkObject.Spawn(true);
            shot.ServerLaunch(dir, OwnerClientId);
        }
    }
}
