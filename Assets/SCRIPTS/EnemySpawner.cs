using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Drop one of these anywhere on the map and it keeps a pocket of enemies alive
/// around itself. Server only - clients receive the spawns through NGO.
public class EnemySpawner : NetworkBehaviour
{
    [Header("What")]
    public GameObject enemyPrefab;

    [Header("How many")]
    public int maxAlive = 4;
    public float spawnInterval = 4f;
    public float initialDelay = 1f;

    [Header("Where")]
    public float spawnRadius = 4f;

    [Tooltip("Stay dormant until a player is this close, so distant camps do not " +
             "fill the server with enemies nobody can see.")]
    public float activationRange = 18f;

    readonly List<NetworkObject> alive = new List<NetworkObject>();
    float nextSpawnTime;

    public override void OnNetworkSpawn()
    {
        if (IsServer) nextSpawnTime = Time.time + initialDelay;
    }

    void Update()
    {
        if (!IsServer || enemyPrefab == null) return;

        alive.RemoveAll(o => o == null || !o.IsSpawned);

        if (alive.Count >= maxAlive) return;
        if (Time.time < nextSpawnTime) return;
        if (!AnyPlayerNear()) return;

        nextSpawnTime = Time.time + spawnInterval;
        Spawn();
    }

    void Spawn()
    {
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 position = transform.position + new Vector3(offset.x, offset.y, 0f);

        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);

        NetworkObject netObject = enemy.GetComponent<NetworkObject>();
        if (netObject == null)
        {
            Debug.LogError("[Wanderers] Enemy prefab has no NetworkObject: " + enemyPrefab.name);
            Destroy(enemy);
            return;
        }

        netObject.Spawn(true);
        alive.Add(netObject);
    }

    bool AnyPlayerNear()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return false;

        float rangeSqr = activationRange * activationRange;
        Vector2 here = transform.position;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            NetworkObject player = client.PlayerObject;
            if (player == null) continue;

            Health hp = player.GetComponent<Health>();
            if (hp != null && hp.IsDead) continue;

            if (((Vector2)player.transform.position - here).sqrMagnitude <= rangeSqr) return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, activationRange);
    }
}
