using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

/// H-sirk's village challenge. The NPC is scenery; the player's networked
/// loadout sends the completed conversation to the server for the ambush.
public sealed class HSirkEncounter : MonoBehaviour
{
    static readonly Vector2 ScenePosition = new Vector2(35.6f, -26.1f);
    const int GoblinCount = 10;
    const int TntCount = 5;

    DialogueData challenge;
    SpriteRenderer fallbackRenderer;
    Sprite[] fallbackFrames;
    float frameTimer;
    int frameIndex;
    bool guardsSummoned;
    bool bossStarted;
    readonly List<Health> guards = new List<Health>();

    public float TalkRange => 2.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BindToVillage()
    {
        SpriteRenderer existing = null;
        float nearest = 16f;
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (renderer.gameObject.name != "H Sirk Idle_0") continue;
            float distance = ((Vector2)renderer.transform.position - ScenePosition).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            existing = renderer;
        }

        if (existing != null)
        {
            if (existing.GetComponent<HSirkEncounter>() == null)
                existing.gameObject.AddComponent<HSirkEncounter>();
            return;
        }

        // The editor placement may still be unsaved. Keep the encounter present
        // in builds by making the same character at the Inspector position.
        Sprite[] frames = Resources.LoadAll<Sprite>("HSirk/H Sirk Idle");
        if (frames.Length == 0)
        {
            Debug.LogError("[Wanderers] H-sirk's idle sprites are missing from Resources/HSirk.");
            return;
        }
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        var go = new GameObject("H Sirk Idle_0");
        go.transform.position = ScenePosition;
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = frames[0];
        sprite.sortingOrder = 12;
        var encounter = go.AddComponent<HSirkEncounter>();
        encounter.fallbackRenderer = sprite;
        encounter.fallbackFrames = frames;
    }

    void Awake()
    {
        challenge = ScriptableObject.CreateInstance<DialogueData>();
        challenge.name = "HSirkChallenge";
        challenge.speakerName = "H-sirk";
        challenge.lines = new[]
        {
            "I will not stop until I take over all the villages.",
            "If you want to stop me, you will have to take care of my guards first."
        };

        DialogueNPC npc = GetComponent<DialogueNPC>();
        if (npc == null) npc = gameObject.AddComponent<DialogueNPC>();
        npc.npcId = "hsirk";
        npc.range = TalkRange;
        npc.promptOffset = new Vector3(0f, 1.2f, 0f);
        npc.conversations = null;
        npc.dialogue = challenge;
    }

    void OnEnable() => DialogueNPC.ConversationFinishedByNpc += OnConversationFinished;
    void OnDisable() => DialogueNPC.ConversationFinishedByNpc -= OnConversationFinished;

    void OnDestroy()
    {
        if (challenge != null) Destroy(challenge);
    }

    void Update()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (guardsSummoned && !bossStarted && network != null && network.IsServer)
        {
            bool allDefeated = true;
            foreach (Health guard in guards)
                if (guard != null && !guard.IsDead) { allDefeated = false; break; }
            if (allDefeated) ServerStartBossFight();
        }

        if (fallbackRenderer == null || fallbackFrames == null || fallbackFrames.Length < 2 ||
            TimeStopWorld.IsStopped) return;
        frameTimer += Time.deltaTime;
        if (frameTimer < 0.16f) return;
        frameTimer = 0f;
        frameIndex = (frameIndex + 1) % fallbackFrames.Length;
        fallbackRenderer.sprite = fallbackFrames[frameIndex];
    }

    void OnConversationFinished(DialogueNPC speaker, DialogueData finished)
    {
        if (speaker == null || speaker.gameObject != gameObject || finished != challenge) return;
        NetworkManager network = NetworkManager.Singleton;
        NetworkObject player = network != null && network.IsClient && network.SpawnManager != null
            ? network.SpawnManager.GetLocalPlayerObject() : null;
        AbilityLoadout loadout = player != null ? player.GetComponent<AbilityLoadout>() : null;
        if (loadout != null) loadout.RequestHSirkAmbush();
    }

    public bool ServerSummonGuards()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (guardsSummoned || network == null || !network.IsServer || !network.IsListening ||
            network.NetworkConfig == null || network.NetworkConfig.Prefabs == null) return false;

        GameObject goblin = null;
        GameObject tntGoblin = null;
        foreach (var entry in network.NetworkConfig.Prefabs.Prefabs)
        {
            GameObject prefab = entry.Prefab;
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null) continue;
            if (prefab.name == "Goblin" && prefab.GetComponent<EnemyBrain>() != null)
                goblin = prefab;
            if (prefab.name == "TNTGoblin" && prefab.GetComponent<TNTGoblinBrain>() != null)
                tntGoblin = prefab;
        }
        if (goblin == null || tntGoblin == null)
        {
            Debug.LogError("[Wanderers] H-sirk needs registered Goblin and TNTGoblin network prefabs.");
            return false;
        }

        guardsSummoned = true;
        for (int i = 0; i < GoblinCount + TntCount; i++)
        {
            GameObject prefab = i < GoblinCount ? goblin : tntGoblin;
            float angle = i * Mathf.PI * 2f / (GoblinCount + TntCount);
            float radius = 4.5f + (i % 3) * 1.25f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 spawn = FindClearPosition((Vector2)transform.position + offset);
            GameObject guard = Instantiate(prefab, new Vector3(spawn.x, spawn.y, 0f), Quaternion.identity);
            EnemyBrain melee = guard.GetComponent<EnemyBrain>();
            if (melee != null) { melee.aggroRange = 18f; melee.giveUpRange = 25f; }
            TNTGoblinBrain bomber = guard.GetComponent<TNTGoblinBrain>();
            if (bomber != null) bomber.aggroRange = 18f;
            guard.GetComponent<NetworkObject>().Spawn(true);
            Health health = guard.GetComponent<Health>();
            if (health != null) guards.Add(health);
        }
        return true;
    }

    void ServerStartBossFight()
    {
        GameObject prefab = Resources.Load<GameObject>("PREFABS/HSirkBoss");
        if (prefab == null || prefab.GetComponent<HSirkBoss>() == null)
        {
            Debug.LogError("[Wanderers] H-sirk boss prefab is missing from Resources/PREFABS.");
            return;
        }
        bossStarted = true;
        GameObject boss = Instantiate(prefab, transform.position, Quaternion.identity);
        boss.GetComponent<NetworkObject>().Spawn(true);
        HideForBossFight();
    }

    public void HideForBossFight()
    {
        bossStarted = true;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;
        DialogueNPC npc = GetComponent<DialogueNPC>();
        if (npc != null) npc.enabled = false;
    }

    static Vector2 FindClearPosition(Vector2 desired)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 candidate = desired + new Vector2((attempt % 3 - 1) * 1.2f, (attempt / 3) * 1.2f);
            bool blocked = false;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(candidate, 0.55f))
            {
                if (hit == null || !hit.enabled || hit.isTrigger) continue;
                if (hit is UnityEngine.Tilemaps.TilemapCollider2D) continue;
                string obstacle = hit.gameObject.name.ToLowerInvariant();
                if (obstacle.Contains("ground") || obstacle.Contains("floor") ||
                    obstacle.Contains("terrain")) continue;
                blocked = true;
                break;
            }
            if (!blocked) return candidate;
        }
        return desired;
    }
}
