using Unity.Netcode;
using UnityEngine;

/// Hunter's Bow ability: fires an arrow towards the mouse / aim direction.
/// Fast, responsive ranged attack given by the Blacksmith Villager.
public class BowAbility : Ability
{
    [Header("Bow Settings")]
    public ArrowProjectile arrowPrefab;
    public float forwardOffset = 0.6f;
    public int arrowDamage = 20;

    protected override void Awake()
    {
        base.Awake();
        displayName = "Hunter's Bow";
        cooldown = 0.32f;
        powerCost = 0f;
    }

    protected override void OnServerActivate(Vector2 aimDirection)
    {
        if (arrowPrefab == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
            {
                foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
                {
                    if (p.Prefab != null && p.Prefab.GetComponent<ArrowProjectile>() != null)
                    {
                        arrowPrefab = p.Prefab.GetComponent<ArrowProjectile>();
                        break;
                    }
                }
            }
        }
        if (arrowPrefab == null) return;

        Vector2 dir = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.right;
        Vector3 spawnPos = transform.position + (Vector3)(dir * forwardOffset);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        ArrowProjectile arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
        arrow.NetworkObject.Spawn(true);
        arrow.ServerLaunch(dir, OwnerClientId, ScaleDamage(arrowDamage), NetworkObject);
    }

    protected override void OnClientEffects(Vector2 aimDirection)
    {
        CameraFollow.Shake(0.09f);

        Vector2 dir = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.right;
        Vector3 muzzlePos = transform.position + (Vector3)(dir * forwardOffset);

        // 1. Wind / Mana Muzzle Shockwave Ring
        GameObject ring = new GameObject("BowMuzzleRing");
        ring.transform.position = muzzlePos;
        ring.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        var rSr = ring.AddComponent<SpriteRenderer>();
        rSr.sprite = AuraVFXLibrary.GetSonicBoomRing();
        rSr.color = Color.white;
        rSr.sortingOrder = 36;
        ring.AddComponent<ArrowRingFader>().Init(0.18f, 0.2f, 1.6f);

        // 2. Muzzle Flash Star
        GameObject flash = new GameObject("BowMuzzleFlash");
        flash.transform.position = muzzlePos;
        var fSr = flash.AddComponent<SpriteRenderer>();
        fSr.sprite = AuraVFXLibrary.GetSparkStar();
        fSr.color = new Color(0.2f, 1f, 0.8f, 1f);
        fSr.sortingOrder = 37;
        flash.AddComponent<ArrowMoteFader>().Init(0.14f, 0.7f);

        // 3. Muzzle speed sparks
        for (int i = 0; i < 4; i++)
        {
            GameObject sp = new GameObject("BowMuzzleSpark");
            sp.transform.position = muzzlePos;
            var spSr = sp.AddComponent<SpriteRenderer>();
            spSr.sprite = AuraVFXLibrary.GetSparkStar();
            spSr.color = Color.Lerp(Color.white, new Color(0.1f, 0.9f, 0.7f), Random.value);
            spSr.sortingOrder = 38;

            Vector2 spDir = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * dir;
            sp.AddComponent<ArrowSparkMote>().Init(spDir.normalized * Random.Range(4f, 8f), 0.16f);
        }
    }
}

