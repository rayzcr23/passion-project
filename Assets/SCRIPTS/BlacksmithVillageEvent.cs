using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Coordinates the Blacksmith Village questline:
/// 1. Villager/Blacksmith stands in front of the eastern houses and grants the Bow.
/// 2. Spawns TNT Goblins that throw dynamite and suicide-rush at half health.
/// 3. Upon goblin defeat, as villagers congratulate you, the Giant Fish leaps from the water!
/// 4. Player defeats the Giant Fish with the Bow.
public class BlacksmithVillageEvent : NetworkBehaviour
{
    public static BlacksmithVillageEvent Instance { get; private set; }

    [Header("Quest NPC")]
    public DialogueNPC blacksmithNpc;       // Fisherman #1 — bow grant + goblin quest (starts active)
    public DialogueNPC guideFishermanNpc;   // Fisherman #2 — blacksmith village guide (starts inactive)
    public string blacksmithNpcId = "fisherman";

    [Header("Village")]
    [Tooltip("Where the TNT goblin assault spawns. Falls back to the scene Blacksmith.")]
    public Transform villageCenter;

    [Header("TNT Goblin Spawns")]
    public GameObject tntGoblinPrefab;
    public Transform[] goblinSpawnPoints;
    public int requiredGoblins = 4;

    [Header("Giant Fish Boss")]
    public GiantFishBoss giantFishPrefab;
    public Vector2 fishWaterCenter = new Vector2(40f, 2f);

    [Header("Chipmunk Boss")]
    public ChipmunkBoss chipmunkBossPrefab;
    public Vector2 chipmunkBossSpawnOffset = new Vector2(6f, 2.5f);

    [Header("Event State")]
    public bool isEventStarted;
    public bool areGoblinsDefeated;
    public bool isFishSpawned;
    public bool isFishDefeated;
    public bool isChipmunkBossSpawned;
    public bool isGlacialBurstGranted;

    readonly NetworkVariable<int> goblinsKilled = new NetworkVariable<int>(0);
    readonly NetworkVariable<bool> fishActive = new NetworkVariable<bool>(false);

    int spawnedGoblinCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitOnLoad()
    {
        EnsureVillageInstance();
    }

    public static BlacksmithVillageEvent EnsureVillageInstance()
    {
        if (Instance != null)
        {
            Instance.EnsureBlacksmithNpc();
            return Instance;
        }

        var evt = Object.FindFirstObjectByType<BlacksmithVillageEvent>();
        if (evt == null)
        {
            var go = new GameObject("BlacksmithVillageEvent");
            go.AddComponent<NetworkObject>();
            evt = go.AddComponent<BlacksmithVillageEvent>();
            evt.fishWaterCenter = new Vector2(40f, 2f);
        }
        else if (evt.GetComponent<NetworkObject>() == null)
        {
            evt.gameObject.AddComponent<NetworkObject>();
        }
        Instance = evt;
        evt.EnsureBlacksmithNpc();
        return evt;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        EnsureBlacksmithNpc();
        EnsureVillageAmbientSpawns();
    }

    void Update()
    {
        if (blacksmithNpc == null)
        {
            EnsureBlacksmithNpc();
        }
    }

    public void EnsureBlacksmithNpc()
    {
        blacksmithNpcId = "fisherman";

        // Find ALL DialogueNPCs named "fisherman" in the scene (active or not).
        var allNpcs = Object.FindObjectsByType<DialogueNPC>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var fishermen = new System.Collections.Generic.List<DialogueNPC>();
        foreach (var n in allNpcs)
            if (n.gameObject.name.ToLower().Contains("fisherman"))
                fishermen.Add(n);

        // The real Blacksmith is never part of the fisherman chain - if he was wired
        // into blacksmithNpc, keep him only as the assault's village centre.
        if (blacksmithNpc != null && !blacksmithNpc.gameObject.name.ToLower().Contains("fisherman"))
        {
            if (villageCenter == null) villageCenter = blacksmithNpc.transform;
            blacksmithNpc = null;
        }

        if (villageCenter == null)
        {
            foreach (var n in allNpcs)
            {
                string nm = n.gameObject.name.ToLower();
                if (nm.Contains("blacksmith") && !nm.Contains("village") && !nm.Contains("event"))
                { villageCenter = n.transform; break; }
            }
        }

        // Sort by hierarchy sibling index: top of hierarchy = #1 (bow grant), next = #2 (guide).
        fishermen.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        // #1 — the bow grant fisherman (starts active).
        if (fishermen.Count >= 1 && blacksmithNpc == null)
        {
            blacksmithNpc = fishermen[0];
            blacksmithNpc.gameObject.SetActive(true);
            blacksmithNpc.npcId = blacksmithNpcId;
            EnsureBlacksmithDialogue(blacksmithNpc);
            if (blacksmithNpc.GetComponent<BlacksmithNPCMarker>() == null)
                blacksmithNpc.gameObject.AddComponent<BlacksmithNPCMarker>();
        }

        // #2 — the guide fisherman (starts inactive until fish dies).
        if (fishermen.Count >= 2 && guideFishermanNpc == null)
        {
            guideFishermanNpc = fishermen[1];
            guideFishermanNpc.gameObject.SetActive(false);
            guideFishermanNpc.npcId = blacksmithNpcId;
        }

        // The village blacksmith looks after his own iron questline - make sure the
        // component that drives it exists even when the scene shipped without it.
        BlacksmithForgeNPC.EnsureInstance();

        if (blacksmithNpc != null) return; // found from scene, done

        // Fallback: spawn a fisherman NPC if none exist in the scene.
        var bGo = new GameObject("fisherman");
        bGo.transform.position = new Vector3(47f, 7.5f, 0f);
        bGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        var sr = bGo.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        sr.sprite = ResolveBlacksmithSprite();

        var npc = bGo.AddComponent<DialogueNPC>();
        npc.npcId = blacksmithNpcId;
        npc.range = 2.2f;
        npc.promptOffset = new Vector3(0f, 1.2f, 0f);
        EnsureBlacksmithDialogue(npc);

        bGo.AddComponent<BlacksmithNPCMarker>();

        blacksmithNpc = npc;
        Debug.Log("[Wanderers] Fisherman NPC spawned as fallback.");
    }

    Sprite ResolveBlacksmithSprite()
    {
        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s == null) continue;
            string n = s.name.ToLower();
            if (n.Contains("pawn_idle") && n.Contains("blue")) return s;
        }
        foreach (var s in allSprites)
        {
            if (s == null) continue;
            string n = s.name.ToLower();
            if (n.Contains("meat") && n.Contains("pawn")) return s;
        }
        foreach (var s in allSprites)
        {
            if (s == null) continue;
            string n = s.name.ToLower();
            if (n.Contains("pawn_idle")) return s;
        }
        foreach (var s in allSprites)
        {
            if (s == null) continue;
            string n = s.name.ToLower();
            if (n.Contains("pawn") || n.Contains("warrior") || n.Contains("archer")) return s;
        }

        // Fall back to copying sprite from any existing NPC in the scene
        var otherNpcs = Object.FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None);
        foreach (var other in otherNpcs)
        {
            var otherSr = other.GetComponent<SpriteRenderer>();
            if (otherSr != null && otherSr.sprite != null) return otherSr.sprite;
        }

        return null;
    }

    /// This NPC is always the lake fisherman, so never let a blind Resources scan
    /// hand him the blacksmith's iron-quest conversation.
    void EnsureBlacksmithDialogue(DialogueNPC npc)
    {
        if (npc.dialogue != null && !npc.dialogue.name.Contains("Villager")) return;

        var bowGrant = Resources.Load<DialogueData>("STORY/Fisherman_BowGrant");
        if (bowGrant != null)
        {
            npc.dialogue = bowGrant;
            return;
        }

        // Runtime fallback dialogue
        var fallback = ScriptableObject.CreateInstance<DialogueData>();
        fallback.speakerName = "Fisherman";
        fallback.lines = new[]
        {
            "Traveler! Goblins found dynamite and are attacking our village by the lake!",
            "Take this fishing bow. Aim with your mouse and Left-Click to pick them off!",
            "Defeat 4 TNT Goblins before they blow us to pieces!"
        };
        fallback.inProgressLines = new[] { "The dynamite goblins are still out there! Aim with your bow and shoot them!" };
        fallback.completedLines = new[] { "You drove off the goblins! Now we can fish in peace!" };
        npc.dialogue = fallback;
    }

    bool isConversationHooked;

    void OnEnable()
    {
        HookConversation(true);
    }

    void OnDisable()
    {
        HookConversation(false);
    }

    public override void OnNetworkSpawn()
    {
        HookConversation(true);
    }

    public override void OnNetworkDespawn()
    {
        HookConversation(false);
    }

    void HookConversation(bool hook)
    {
        if (hook)
        {
            if (!isConversationHooked)
            {
                DialogueNPC.ConversationFinished += HandleConversationFinished;
                isConversationHooked = true;
            }
        }
        else
        {
            if (isConversationHooked)
            {
                DialogueNPC.ConversationFinished -= HandleConversationFinished;
                isConversationHooked = false;
            }
        }
    }

    void HandleConversationFinished(DialogueData data)
    {
        if (data == null) return;
        string spk = data.speakerName != null ? data.speakerName.ToLower() : "";
        string dName = data.name != null ? data.name.ToLower() : "";

        // Strictly match ONLY the lake fisherman - the village blacksmith runs his
        // own iron quest and must not start the assault or hand out a bow.
        bool isFisherman = spk.Contains("fisher") || dName.Contains("fisher");

        if (isFisherman)
        {
            if (isFishDefeated)
            {
                GrantGlacialBurstToLocalPlayer();
            }
            else
            {
                GrantBowToLocalPlayer();

                if (NetworkManager.Singleton == null || IsServer)
                {
                    if (!isEventStarted) ServerStartGoblinAssault();
                }
                else
                {
                    RequestStartAssaultRpc();
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RequestStartAssaultRpc()
    {
        if (!isEventStarted) ServerStartGoblinAssault();
    }

    /// Grants the bow to the local player so they can shoot immediately
    public void GrantBowToLocalPlayer()
    {
        var localPlayer = Movement.FindAnyObjectByType<Movement>();
        if (localPlayer != null)
        {
            var loadout = localPlayer.GetComponent<AbilityLoadout>();
            if (loadout != null && !loadout.hasBow)
            {
                loadout.UnlockBow();
                CameraFollow.Shake(0.15f);
                ShowBanner("🏹 Hunter's Bow Acquired! Press [Tab] or [2] to swap weapon, aim with mouse!");
            }
        }
    }

    /// Grants the Glacial Burst ice ability taught by the Fisherman after defeating the Giant Fish
    public void GrantGlacialBurstToLocalPlayer()
    {
        if (isGlacialBurstGranted) return;
        isGlacialBurstGranted = true;

        var localPlayer = Movement.FindAnyObjectByType<Movement>();
        if (localPlayer != null)
        {
            var loadout = localPlayer.GetComponent<AbilityLoadout>();
            if (loadout != null && !loadout.hasIceAbility)
            {
                loadout.UnlockGlacialBurst();
                ImpactFrameEffect.TriggerCustom(new Color(0.35f, 0.9f, 1f), 0.35f, 0.85f, true);
                ShowBanner("❄️ NEW SPECIAL MOVE LEARNED: Glacial Burst (35 POW)! Press [G] to unleash a 360° ring of Ice Spikes!");
            }
        }
    }

    /// Called when the Giant Fish Boss is defeated
    public void OnFishDefeated()
    {
        isFishDefeated = true;

        ShowBanner("🏆 LEVIATHAN SLAIN! The waters are calm! Speak to the Fisherman to claim your reward!");
        CameraFollow.Shake(0.5f);

        // ── Swap fishermen ────────────────────────────────────────────
        // Deactivate Fisherman #1 (bow grant / goblin quest NPC).
        if (blacksmithNpc != null)
            blacksmithNpc.gameObject.SetActive(false);

        // Activate Fisherman #2 and give it the blacksmith guide dialogue.
        if (guideFishermanNpc != null)
        {
            // Assign dialogue before activating so Start() sees it correctly.
            var guide = Resources.Load<DialogueData>("STORY/Fisherman_BlacksmithGuide");
            if (guide != null)
            {
                guideFishermanNpc.dialogue      = guide;
                guideFishermanNpc.conversations = null;
            }
            else
            {
                // Inline fallback if asset isn't found
                var fallback = ScriptableObject.CreateInstance<DialogueData>();
                fallback.speakerName = "Fisherman";
                fallback.lines = new[]
                {
                    "You did it! That beast is finally gone.",
                    "Head north to the blacksmith village — up at the top of the island.",
                    "They'll have gear worthy of someone like you!"
                };
                fallback.completedLines = new[] { "The blacksmith village is north, up on the hill. Safe travels!" };
                guideFishermanNpc.dialogue = fallback;
            }

            guideFishermanNpc.npcId = blacksmithNpcId;
            guideFishermanNpc.gameObject.SetActive(true);
        }
    }

    public GameObject ResolveGoblinPrefab()
    {
        if (tntGoblinPrefab != null) return tntGoblinPrefab;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
        {
            foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (p.Prefab != null && (p.Prefab.name.Contains("TNT") || p.Prefab.GetComponent<TNTGoblinBrain>() != null))
                {
                    tntGoblinPrefab = p.Prefab;
                    return p.Prefab;
                }
            }
            foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (p.Prefab != null && p.Prefab.GetComponent<EnemyBrain>() != null)
                    return p.Prefab;
            }
        }

        var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allGos)
        {
            if (go != null && (go.name.Contains("TNTGoblin") || go.GetComponent<TNTGoblinBrain>() != null))
            {
                tntGoblinPrefab = go;
                return go;
            }
        }

        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner != null && spawner.enemyPrefab != null) return spawner.enemyPrefab;
        return null;
    }

    public void EnsureVillageAmbientSpawns()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        // Check if there is already a spawner for TNT Goblins
        var spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
        foreach (var s in spawners)
        {
            if (s != null && s.enemyPrefab != null && (s.enemyPrefab.name.Contains("TNT") || s.enemyPrefab.GetComponent<TNTGoblinBrain>() != null))
                return;
        }

        GameObject prefab = ResolveGoblinPrefab();
        if (prefab == null) return;

        // Create an ambient EnemySpawner near the road entering the village (X = 38, Y = 5.5)
        var spawnerGo = new GameObject("TNTGoblinVillageSpawner");
        spawnerGo.transform.position = new Vector3(38f, 5.5f, 0f);

        var spawner = spawnerGo.AddComponent<EnemySpawner>();
        spawner.enemyPrefab = prefab;
        spawner.maxAlive = 3;
        spawner.spawnInterval = 6f;
        spawner.initialDelay = 1.5f;
        spawner.spawnRadius = 3.5f;
        spawner.activationRange = 22f;

        var netObj = spawnerGo.AddComponent<NetworkObject>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }
        Debug.Log("[Wanderers] Created ambient TNT Goblin spawner at " + spawnerGo.transform.position);
    }

    int localGoblinsKilled = 0;

    [ContextMenu("Test Start Goblin Assault")]
    public void ServerStartGoblinAssault()
    {
        if (isEventStarted) return;
        isEventStarted = true;
        localGoblinsKilled = 0;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            goblinsKilled.Value = 0;
        }

        Vector2 assaultCenter = (villageCenter != null) ? (Vector2)villageCenter.position
                              : (blacksmithNpc != null ? (Vector2)blacksmithNpc.transform.position
                                                       : new Vector2(47f, 7.5f));
        GameObject prefabToUse = ResolveGoblinPrefab();

        int count = Mathf.Max(requiredGoblins, (goblinSpawnPoints != null && goblinSpawnPoints.Length > 0) ? goblinSpawnPoints.Length : 4);

        // Spawn points directly in the village plaza in front of the blacksmith / houses
        Vector2[] assaultOffsets = new Vector2[]
        {
            new Vector2(-3.5f, -3.0f),
            new Vector2(3.5f, -3.0f),
            new Vector2(-1.5f, -5.0f),
            new Vector2(2.5f, -5.0f)
        };

        for (int i = 0; i < count; i++)
        {
            Vector2 spawnPos;
            if (goblinSpawnPoints != null && i < goblinSpawnPoints.Length && goblinSpawnPoints[i] != null)
            {
                spawnPos = (Vector2)goblinSpawnPoints[i].position;
            }
            else if (i < assaultOffsets.Length)
            {
                spawnPos = assaultCenter + assaultOffsets[i];
            }
            else
            {
                spawnPos = assaultCenter + new Vector2(Random.Range(-5f, 5f), Random.Range(-3f, -6f));
            }

            if (prefabToUse != null)
            {
                GameObject gob = Instantiate(prefabToUse, spawnPos, Quaternion.identity);

                var baseBrain = gob.GetComponent<EnemyBrain>();
                var tntBrain = gob.GetComponent<TNTGoblinBrain>();
                if (tntBrain == null)
                {
                    if (baseBrain != null) Destroy(baseBrain);
                    tntBrain = gob.AddComponent<TNTGoblinBrain>();

                    var sr = gob.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(0.45f, 0.75f, 1f);
                }

                var netObj = gob.GetComponent<NetworkObject>();
                if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    netObj.Spawn(true);
                }

                var hp = gob.GetComponent<Health>();
                if (hp != null)
                {
                    hp.creatureId = "tnt_goblin";
                    hp.OnDied += ServerOnGoblinDied;
                }
            }
            spawnedGoblinCount++;
        }

        ShowBanner("🏹 Goblins attacking! Defeat 4 TNT Goblins near the village!");
    }

    void ServerOnGoblinDied()
    {
        localGoblinsKilled++;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            goblinsKilled.Value++;
        }

        int totalKills = Mathf.Max(localGoblinsKilled, (NetworkObject != null && NetworkObject.IsSpawned) ? goblinsKilled.Value : 0);
        if (totalKills >= requiredGoblins && !areGoblinsDefeated)
        {
            areGoblinsDefeated = true;
            StartCoroutine(CongratulateAndSpawnFishRoutine());
        }
    }

    IEnumerator CongratulateAndSpawnFishRoutine()
    {
        ShowBanner("💥 The TNT Goblins are defeated! The villagers cheer!");
        yield return new WaitForSeconds(3.0f);

        ShowBanner("Fisherman: 'Incredible shot! The village is sa— wait, WHAT IS IN THE WATER?!'");
        CameraFollow.Shake(0.45f);

        yield return new WaitForSeconds(2.5f);

        ServerSpawnGiantFish();
    }

    void ServerSpawnGiantFish()
    {
        if (isFishSpawned) return;
        isFishSpawned = true;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            fishActive.Value = true;
        }

        if (giantFishPrefab == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
            {
                foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
                {
                    if (p.Prefab != null && p.Prefab.GetComponent<GiantFishBoss>() != null)
                    {
                        giantFishPrefab = p.Prefab.GetComponent<GiantFishBoss>();
                        break;
                    }
                }
            }
            if (giantFishPrefab == null)
            {
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    if (go != null && go.GetComponent<GiantFishBoss>() != null)
                    {
                        giantFishPrefab = go.GetComponent<GiantFishBoss>();
                        break;
                    }
                }
            }
        }

        if (giantFishPrefab != null)
        {
            GiantFishBoss fish = Instantiate(giantFishPrefab, (Vector3)fishWaterCenter, Quaternion.identity);
            fish.waterCenter = fishWaterCenter;
            var netObj = fish.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn(true);
            }
        }

        ShowBanner("🌊 A GIANT FISH HAS BREACHED THE WATER! KILL IT WITH YOUR BOW!");
    }

    public void TriggerChipmunkBossAfterSwordUpgrade(Vector3 blacksmithPosition)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !IsServer)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                RequestSpawnChipmunkBossRpc(blacksmithPosition);
            else
                Debug.LogWarning("[Wanderers] Cannot request chipmunk boss spawn: BlacksmithVillageEvent is not spawned.");
            return;
        }

        ServerSpawnChipmunkBoss(blacksmithPosition);
    }

    [Rpc(SendTo.Server)]
    void RequestSpawnChipmunkBossRpc(Vector3 blacksmithPosition)
    {
        ServerSpawnChipmunkBoss(blacksmithPosition);
    }

    void ServerSpawnChipmunkBoss(Vector3 blacksmithPosition)
    {
        if (isChipmunkBossSpawned) return;
        isChipmunkBossSpawned = true;

        if (chipmunkBossPrefab == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
            {
                foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
                {
                    if (p.Prefab == null) continue;
                    ChipmunkBoss boss = p.Prefab.GetComponent<ChipmunkBoss>();
                    if (boss != null)
                    {
                        chipmunkBossPrefab = boss;
                        break;
                    }
                }
            }
        }

        if (chipmunkBossPrefab == null)
        {
            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGos)
            {
                if (go == null) continue;
                ChipmunkBoss boss = go.GetComponent<ChipmunkBoss>();
                if (boss == null) continue;
                chipmunkBossPrefab = boss;
                break;
            }
        }

        if (chipmunkBossPrefab == null)
        {
            Debug.LogWarning("[Wanderers] ChipmunkBoss prefab was not found. Run Tools > Wanderers > Setup Blacksmith Village & Fish Boss.");
            isChipmunkBossSpawned = false;
            return;
        }

        Vector3 spawnPosition = blacksmithPosition + (Vector3)chipmunkBossSpawnOffset;
        ChipmunkBoss chipmunk = Instantiate(chipmunkBossPrefab, spawnPosition, Quaternion.identity);

        var netObj = chipmunk.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            netObj.Spawn(true);

        chipmunk.PlayEmergence(spawnPosition);
        ShowBanner("A CHIPMUNK BOSS BURSTS OUT! Watch for chipmunk_1, then dodge the slash!");
    }

    void ShowBanner(string message)
    {
        if (NetworkObject != null && NetworkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NotifyBannerRpc(message);
        }
        else
        {
            BannerOverlay.Show(message);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyBannerRpc(string message)
    {
        BannerOverlay.Show(message);
    }

    /// Public wrapper so BlacksmithForgeNPC can show a banner.
    public void ShowBannerPublic(string message) => ShowBanner(message);
}
