using Unity.Netcode;
using UnityEngine;

/// Adds the skeleton guardian quest to the monk already placed in Witchcraft Village.
/// The quest is started by DialogueNPC, then the server summons the boss once the
/// shared quest state becomes active.
public class WitchcraftMonkEncounter : MonoBehaviour
{
    const string QuestId = "quest_skeleton_boss";
    const string QuestResource = "STORY/quest_skeleton_boss";
    const string BossResource = "PREFABS/SkeletonBoss";
    static readonly Vector2 MonkScenePosition = new Vector2(38.71f, 41.69f);

    // Try several spots around the monk so the boss does not appear inside a
    // building or on top of a player. The first clear spot wins.
    static readonly Vector2[] SpawnOffsets =
    {
        new Vector2(0f, -8f), new Vector2(8f, 0f),
        new Vector2(-8f, 0f), new Vector2(0f, 8f),
        new Vector2(8f, -8f), new Vector2(-8f, -8f),
        new Vector2(10f, 5f), new Vector2(-10f, 5f)
    };

    QuestDefinition quest;
    DialogueData monkDialogue;
    QuestManager registeredManager;
    bool bossSpawned;
    bool missingBossLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToSceneMonk()
    {
        SpriteRenderer nearest = null;
        float nearestDistance = 2f * 2f;

        foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (renderer.gameObject.name != "Idle_0" || renderer.GetComponent<Animator>() == null)
                continue;

            float distance = ((Vector2)renderer.transform.position - MonkScenePosition).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearest = renderer;
            nearestDistance = distance;
        }

        if (nearest == null)
        {
            Debug.LogWarning("[Wanderers] Witchcraft Village monk was not found near (38.71, 41.69).");
            return;
        }

        if (nearest.GetComponent<WitchcraftMonkEncounter>() == null)
            nearest.gameObject.AddComponent<WitchcraftMonkEncounter>();
    }

    void Awake()
    {
        quest = Resources.Load<QuestDefinition>(QuestResource);
        if (quest == null)
        {
            Debug.LogError("[Wanderers] Missing Resources/STORY/quest_skeleton_boss quest asset.");
            return;
        }

        monkDialogue = ScriptableObject.CreateInstance<DialogueData>();
        monkDialogue.name = "Monk_SkeletonGuardian";
        monkDialogue.speakerName = "Monk";
        monkDialogue.startsQuest = quest;
        monkDialogue.lines = new[]
        {
            "A restless skeleton guards this village. I can summon it for you to face.",
            "Defeat the guardian, and its hold on Witchcraft Village will end."
        };
        monkDialogue.inProgressLines = new[]
        {
            "The skeleton guardian has answered my summons. Find it near the village and defeat it."
        };
        monkDialogue.completedLines = new[]
        {
            "You defeated the skeleton guardian. Witchcraft Village is in your debt.",
            "I grant you Time Stop. It costs 100 POW and lasts up to 10 seconds.",
            "After time resumes, wait 30 seconds before using it again. Press T, or use the TIME button on touch screens."
        };

        DialogueNPC npc = GetComponent<DialogueNPC>();
        if (npc == null) npc = gameObject.AddComponent<DialogueNPC>();
        npc.npcId = "witchcraft_monk";
        npc.range = 2.4f;
        npc.promptOffset = new Vector3(0f, 1.25f, 0f);
        npc.conversations = null;
        npc.dialogue = monkDialogue;
    }

    void OnEnable()
    {
        DialogueNPC.ConversationFinishedByNpc += OnConversationFinished;
    }

    void OnDisable()
    {
        DialogueNPC.ConversationFinishedByNpc -= OnConversationFinished;
    }

    void OnConversationFinished(DialogueNPC speaker, DialogueData finished)
    {
        if (speaker == null || speaker.gameObject != gameObject || finished != monkDialogue)
            return;
        QuestManager manager = QuestManager.Instance;
        if (manager == null || !manager.IsComplete(QuestId)) return;

        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsClient || network.SpawnManager == null) return;
        NetworkObject player = network.SpawnManager.GetLocalPlayerObject();
        AbilityLoadout loadout = player != null ? player.GetComponent<AbilityLoadout>() : null;
        if (loadout != null) loadout.RequestUnlockTimeStop();
    }

    void Update()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || quest == null) return;

        if (registeredManager != manager) RegisterQuest(manager);

        NetworkManager network = NetworkManager.Singleton;
        if (bossSpawned || !manager.IsActive(QuestId) ||
            network == null || !network.IsListening || !network.IsServer)
            return;

        SpawnBoss();
    }

    void RegisterQuest(QuestManager manager)
    {
        if (manager.quests != null)
        {
            foreach (QuestDefinition existing in manager.quests)
            {
                if (existing != null && existing.questId == QuestId)
                {
                    registeredManager = manager;
                    return;
                }
            }
        }

        int count = manager.quests != null ? manager.quests.Length : 0;
        var registered = new QuestDefinition[count + 1];
        if (count > 0) System.Array.Copy(manager.quests, registered, count);
        registered[count] = quest;
        manager.quests = registered;
        registeredManager = manager;
    }

    void SpawnBoss()
    {
        GameObject prefab = Resources.Load<GameObject>(BossResource);
        if (prefab == null)
        {
            if (!missingBossLogged)
                Debug.LogError("[Wanderers] Missing Resources/PREFABS/SkeletonBoss prefab.");
            missingBossLogged = true;
            return;
        }

        Vector3 position = FindClearSpawnPosition();
        GameObject boss = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject networkObject = boss.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("[Wanderers] SkeletonBoss prefab needs a NetworkObject.");
            Destroy(boss);
            return;
        }

        networkObject.Spawn(true);
        boss.GetComponent<SkeletonBoss>()?.PlayEmergence();
        bossSpawned = true;
        Debug.Log("[Wanderers] Skeleton guardian summoned at " + position + ".");
    }

    Vector3 FindClearSpawnPosition()
    {
        Vector2 origin = transform.position;
        Vector2 best = origin + SpawnOffsets[0];
        int fewestBlockers = int.MaxValue;

        foreach (Vector2 offset in SpawnOffsets)
        {
            Vector2 candidate = origin + offset;
            Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, 2.4f);
            int blockers = 0;

            foreach (Collider2D hit in hits)
            {
                if (hit != null && hit.enabled && !hit.isTrigger)
                    blockers++;
            }

            if (blockers == 0) return new Vector3(candidate.x, candidate.y, 0f);
            if (blockers >= fewestBlockers) continue;
            best = candidate;
            fewestBlockers = blockers;
        }

        Debug.LogWarning("[Wanderers] No clear skeleton arena found; using the least blocked summon spot.");
        return new Vector3(best.x, best.y, 0f);
    }

    void OnDestroy()
    {
        if (monkDialogue != null) Destroy(monkDialogue);
    }
}
