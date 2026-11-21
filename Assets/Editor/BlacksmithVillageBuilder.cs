using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Automated setup for the Blacksmith Village, Bow weapon, TNT Goblins, and Giant Fish Boss.
/// Run via Tools > Wanderers > Setup Blacksmith Village & Fish Boss, or automatically on compile.
[InitializeOnLoad]
public static class BlacksmithVillageBuilder
{
    const string ArrowPrefabPath = "Assets/PREFABS/Arrow.prefab";
    const string DynamitePrefabPath = "Assets/PREFABS/Dynamite.prefab";
    const string TNTGoblinPrefabPath = "Assets/PREFABS/TNTGoblin.prefab";
    const string ArcherGoblinPrefabPath = "Assets/PREFABS/ArcherGoblin.prefab";
    const string GiantFishPrefabPath = "Assets/PREFABS/GiantFishBoss.prefab";
    const string ChipmunkBossPrefabPath = "Assets/PREFABS/ChipmunkBoss.prefab";
    const string PlayerPrefabPath = "Assets/PREFABS/Warrior.prefab";

    const string IronRequestPath = "Assets/Resources/STORY/Blacksmith_IronRequest.asset";
    const string IronUpgradedPath = "Assets/Resources/STORY/Blacksmith_SwordUpgraded.asset";

    static readonly Vector3 BlacksmithSpawn = new Vector3(47f, 7.5f, 0f);

    const string ArrowSpritePath = "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Red Units/Archer/Arrow.png";
    const string ArcherSpritePath = "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Red Units/Archer/Archer_Idle.png";
    const string DynamiteSpritePath = "Assets/Tiny Swords/Tiny Swords (Update 010)/Factions/Goblins/Troops/TNT/Dynamite/Dynamite.png";
    const string TNTGoblinSpritePath = "Assets/Tiny Swords/Tiny Swords (Update 010)/Factions/Goblins/Troops/TNT/Blue/TNT_Blue.png";
    const string FishSpritePath = "Assets/FISH BOSS.png";
    const string ChipmunkSpritePath = "Assets/chipmunk.png";

    static BlacksmithVillageBuilder()
    {
        EditorApplication.delayCall += DelayRun;
    }

    static void DelayRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Auto-run once to populate prefabs and scene
        if (!SessionState.GetBool("Wanderers_BlacksmithSetupRan_v3", false))
        {
            SessionState.SetBool("Wanderers_BlacksmithSetupRan_v3", true);
            SetupAll();
        }
    }

    [MenuItem("Tools/Wanderers/Setup Blacksmith Village & Fish Boss")]
    public static void SetupAll()
    {
        Debug.Log("[Wanderers] Starting Blacksmith Village & Fish Boss setup...");

        EnsureFolder("Assets/PREFABS");
        EnsureFolder("Assets/STORY");

        GameObject arrowPrefab = BuildArrowPrefab();
        GameObject dynamitePrefab = BuildDynamitePrefab();
        GameObject tntGoblinPrefab = BuildTNTGoblinPrefab(dynamitePrefab);
        GameObject archerGoblinPrefab = BuildArcherGoblinPrefab(arrowPrefab);
        GameObject giantFishPrefab = BuildGiantFishPrefab();
        GameObject chipmunkBossPrefab = BuildChipmunkBossPrefab();

        RegisterNetworkPrefab(arrowPrefab);
        RegisterNetworkPrefab(dynamitePrefab);
        RegisterNetworkPrefab(tntGoblinPrefab);
        RegisterNetworkPrefab(archerGoblinPrefab);
        RegisterNetworkPrefab(giantFishPrefab);
        RegisterNetworkPrefab(chipmunkBossPrefab);

        WirePlayerBow(arrowPrefab);

        QuestDefinition questTNT = EnsureQuest("quest_tnt_goblins", "Explosive Threat", "Defeat 4 TNT Goblins near the village.", "tnt_goblin", 4);
        QuestDefinition questFish = EnsureQuest("quest_giant_fish", "Terror of the Deep", "Kill the Giant Fish with your Bow!", "giant_fish", 1);
        questTNT.nextQuest = questFish;
        EditorUtility.SetDirty(questTNT);

        SetupSceneVillage(tntGoblinPrefab, giantFishPrefab, chipmunkBossPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[Wanderers] Blacksmith Village & Giant Fish Boss setup complete!");
    }

    static GameObject BuildArrowPrefab()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        if (root == null)
        {
            root = new GameObject("Arrow");
            root.layer = LayerMask.NameToLayer("Default");

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
            sr.sortingOrder = 30;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.2f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<ArrowProjectile>();

            PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            Object.DestroyImmediate(root);
            root = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            Debug.Log("[Wanderers] Created Arrow.prefab");
        }
        return root;
    }

    static GameObject BuildDynamitePrefab()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(DynamitePrefabPath);
        if (root == null)
        {
            root = new GameObject("Dynamite");

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = LoadFirstSprite(DynamiteSpritePath);
            sr.sortingOrder = 30;

            root.AddComponent<NetworkObject>();
            root.AddComponent<DynamiteProjectile>();

            PrefabUtility.SaveAsPrefabAsset(root, DynamitePrefabPath);
            Object.DestroyImmediate(root);
            root = AssetDatabase.LoadAssetAtPath<GameObject>(DynamitePrefabPath);
            Debug.Log("[Wanderers] Created Dynamite.prefab");
        }
        return root;
    }

    static GameObject BuildTNTGoblinPrefab(GameObject dynamitePrefab)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(TNTGoblinPrefabPath);
        if (root == null)
        {
            root = new GameObject("TNTGoblin");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) root.layer = enemyLayer;

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = LoadFirstSprite(TNTGoblinSpritePath);
            sr.sortingOrder = 5;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 10f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;

            root.AddComponent<NetworkObject>();

            var nt = root.AddComponent<NetworkTransform>();
            nt.UseUnreliableDeltas = true;
            nt.PositionThreshold = 0.025f;
            nt.PositionMaxInterpolationTime = 0.15f;
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;

            var hp = root.AddComponent<Health>();
            hp.maxHealth = 85;
            hp.creatureId = "tnt_goblin";
            hp.despawnOnDeath = true;
            hp.despawnDelay = 0.3f;

            root.AddComponent<DamageFlash>();
            root.AddComponent<KnockbackReceiver>();

            var brain = root.AddComponent<TNTGoblinBrain>();
            if (dynamitePrefab != null)
                brain.dynamitePrefab = dynamitePrefab.GetComponent<DynamiteProjectile>();

            PrefabUtility.SaveAsPrefabAsset(root, TNTGoblinPrefabPath);
            Object.DestroyImmediate(root);
            root = AssetDatabase.LoadAssetAtPath<GameObject>(TNTGoblinPrefabPath);
            Debug.Log("[Wanderers] Created TNTGoblin.prefab");
        }
        return root;
    }

    static GameObject BuildArcherGoblinPrefab(GameObject arrowPrefab)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherGoblinPrefabPath);
        if (root == null)
        {
            root = new GameObject("ArcherGoblin");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) root.layer = enemyLayer;

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = LoadFirstSprite(ArcherSpritePath);
            sr.sortingOrder = 5;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 6f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.35f;

            root.AddComponent<NetworkObject>();

            var nt = root.AddComponent<NetworkTransform>();
            nt.UseUnreliableDeltas = true;
            nt.PositionThreshold = 0.025f;
            nt.PositionMaxInterpolationTime = 0.15f;
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;

            var hp = root.AddComponent<Health>();
            hp.maxHealth = 80;
            hp.creatureId = "archer_goblin";
            hp.despawnOnDeath = true;
            hp.despawnDelay = 0.6f;

            root.AddComponent<DamageFlash>();
            root.AddComponent<KnockbackReceiver>();

            var brain = root.AddComponent<ArcherGoblinBrain>();
            if (arrowPrefab != null)
                brain.arrowPrefab = arrowPrefab.GetComponent<ArrowProjectile>();
            brain.aggroRange = 24f;
            brain.idealRange = 16f;
            brain.tooCloseRange = 8f;
            brain.giveUpRange = 30f;
            brain.arrowDamage = 28;
            brain.arrowSpeed = 26f;
            brain.shootCooldown = 1.4f;
            brain.aimLeadFactor = 0.65f;

            PrefabUtility.SaveAsPrefabAsset(root, ArcherGoblinPrefabPath);
            Object.DestroyImmediate(root);
            root = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherGoblinPrefabPath);
            Debug.Log("[Wanderers] Created ArcherGoblin.prefab");
        }
        return root;
    }

    static GameObject BuildGiantFishPrefab()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(GiantFishPrefabPath);
        if (root == null)
        {
            root = new GameObject("GiantFishBoss");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) root.layer = enemyLayer;

            var sr = root.AddComponent<SpriteRenderer>();
            Sprite s0 = null, s1 = null;
            Object[] sub = AssetDatabase.LoadAllAssetsAtPath(FishSpritePath);
            foreach (var o in sub)
            {
                if (o is Sprite s)
                {
                    if (s.name.Contains("0")) s0 = s;
                    else if (s.name.Contains("1")) s1 = s;
                }
            }
            if (s0 == null && sub.Length > 1) s0 = sub[1] as Sprite;
            if (s1 == null && sub.Length > 2) s1 = sub[2] as Sprite;

            sr.sprite = s0;
            sr.sortingOrder = 25;

            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;

            root.AddComponent<NetworkObject>();

            var hp = root.AddComponent<Health>();
            hp.maxHealth = 2500;
            hp.creatureId = "giant_fish";
            hp.isBoss = true;
            hp.despawnOnDeath = true;
            hp.despawnDelay = 1.2f;

            root.AddComponent<DamageFlash>();

            var fish = root.AddComponent<GiantFishBoss>();
            fish.frameMouthClosed = s0;
            fish.frameMouthOpen = s1;

            PrefabUtility.SaveAsPrefabAsset(root, GiantFishPrefabPath);
            Object.DestroyImmediate(root);
            root = AssetDatabase.LoadAssetAtPath<GameObject>(GiantFishPrefabPath);
            Debug.Log("[Wanderers] Created GiantFishBoss.prefab");
        }
        return root;
    }

    static GameObject BuildChipmunkBossPrefab()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(ChipmunkBossPrefabPath);
        if (root == null)
        {
            root = new GameObject("ChipmunkBoss");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) root.layer = enemyLayer;

            Sprite idle = LoadSpriteByName(ChipmunkSpritePath, "chipmunk_0");
            Sprite warning = LoadSpriteByName(ChipmunkSpritePath, "chipmunk_1");
            if (idle == null) idle = LoadFirstSprite(ChipmunkSpritePath);
            if (warning == null) warning = idle;

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = idle;
            sr.sortingOrder = 28;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 8f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;

            root.AddComponent<NetworkObject>();

            var nt = root.AddComponent<NetworkTransform>();
            nt.UseUnreliableDeltas = true;
            nt.PositionThreshold = 0.025f;
            nt.PositionMaxInterpolationTime = 0.15f;
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;

            var hp = root.AddComponent<Health>();
            hp.maxHealth = 4500;
            hp.creatureId = "chipmunk_boss";
            hp.isBoss = true;
            hp.despawnOnDeath = true;
            hp.despawnDelay = 1.4f;

            var flash = root.AddComponent<DamageFlash>();
            flash.flashColor = Color.white;
            flash.punchAmount = 0.1f;

            var bar = root.AddComponent<HealthBar>();
            bar.health = hp;
            bar.offset = new Vector3(0f, 0.8f, 0f);
            bar.width = 1.25f;
            bar.height = 0.12f;
            bar.hideWhenFull = false;

            var death = root.AddComponent<DeathEffect>();
            death.fadeSeconds = 0.65f;
            death.shrinkTo = 0.45f;
            death.spinDegrees = 18f;

            root.AddComponent<KnockbackReceiver>().recoverSeconds = 0.18f;

            var boss = root.AddComponent<ChipmunkBoss>();
            boss.idleSprite = idle;
            boss.warningSprite = warning;
            boss.bossScale = 4.2f;
            boss.slashDamage = 95;

            PrefabUtility.SaveAsPrefabAsset(root, ChipmunkBossPrefabPath);
            Object.DestroyImmediate(root);
            root = AssetDatabase.LoadAssetAtPath<GameObject>(ChipmunkBossPrefabPath);
            Debug.Log("[Wanderers] Created ChipmunkBoss.prefab");
        }
        return root;
    }

    static void WirePlayerBow(GameObject arrowPrefab)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (player == null) return;

        var bow = player.GetComponent<BowAbility>();
        if (bow == null) bow = player.AddComponent<BowAbility>();

        if (arrowPrefab != null)
            bow.arrowPrefab = arrowPrefab.GetComponent<ArrowProjectile>();

        var loadout = player.GetComponent<AbilityLoadout>();
        if (loadout != null)
        {
            loadout.bow = bow;
        }

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(player);
        Debug.Log("[Wanderers] Wired BowAbility onto Warrior.prefab");
    }

    static void RegisterNetworkPrefab(GameObject prefab)
    {
        if (prefab == null) return;
        var asset = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (asset == null) return;

        var so = new SerializedObject(asset);
        var list = so.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++)
        {
            var p = list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
            if (p != null && p.objectReferenceValue == prefab) return;
        }

        int idx = list.arraySize;
        list.InsertArrayElementAtIndex(idx);
        var elem = list.GetArrayElementAtIndex(idx);
        elem.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        Debug.Log("[Wanderers] Registered network prefab: " + prefab.name);
    }

    static QuestDefinition EnsureQuest(string id, string title, string desc, string target, int count)
    {
        string path = "Assets/STORY/" + id + ".asset";
        var q = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (q == null)
        {
            q = ScriptableObject.CreateInstance<QuestDefinition>();
            q.questId = id;
            q.title = title;
            q.description = desc;
            q.targetId = target;
            q.requiredCount = count;
            q.objective = QuestDefinition.Objective.Kill;
            AssetDatabase.CreateAsset(q, path);
            Debug.Log("[Wanderers] Created Quest: " + id);
        }
        return q;
    }

    static DialogueData EnsureDialogue(string assetName, string speaker, string[] lines, QuestDefinition startsQuest, string[] inProgress, string[] completed)
    {
        string path = "Assets/STORY/" + assetName + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<DialogueData>();
            d.speakerName = speaker;
            d.lines = lines;
            d.startsQuest = startsQuest;
            d.inProgressLines = inProgress;
            d.completedLines = completed;
            AssetDatabase.CreateAsset(d, path);
            Debug.Log("[Wanderers] Created Dialogue: " + assetName);
        }
        return d;
    }

    static void SetupSceneVillage(GameObject tntGoblinPrefab, GameObject giantFishPrefab, GameObject chipmunkBossPrefab)
    {
        // Find or create BlacksmithVillageEvent
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

        if (tntGoblinPrefab != null) evt.tntGoblinPrefab = tntGoblinPrefab;
        if (giantFishPrefab != null) evt.giantFishPrefab = giantFishPrefab.GetComponent<GiantFishBoss>();
        if (chipmunkBossPrefab != null) evt.chipmunkBossPrefab = chipmunkBossPrefab.GetComponent<ChipmunkBoss>();

        // blacksmithNpc belongs to the lake fisherman, not the village blacksmith -
        // leave it null so the runtime hooks up the right NPC.
        evt.blacksmithNpc = null;

        SetupBlacksmithNpc();

        EditorUtility.SetDirty(evt);
    }

    /// Places the village blacksmith in front of the eastern houses (47, 7.5) and
    /// wires his iron-ore questline. He is never part of the fisherman bow chain.
    static void SetupBlacksmithNpc()
    {
        DialogueData ironRequest = AssetDatabase.LoadAssetAtPath<DialogueData>(IronRequestPath);
        DialogueData ironUpgraded = AssetDatabase.LoadAssetAtPath<DialogueData>(IronUpgradedPath);
        if (ironRequest == null || ironUpgraded == null)
            Debug.LogWarning("[Wanderers] Blacksmith iron dialogue missing under Assets/Resources/STORY.");

        DialogueNPC blacksmith = null;
        var allNpcs = Object.FindObjectsByType<DialogueNPC>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var npc in allNpcs)
        {
            if (npc.transform.position.x <= 35f || !npc.name.Contains("Blacksmith")) continue;
            if (blacksmith == null || (npc.gameObject.activeInHierarchy && !blacksmith.gameObject.activeInHierarchy))
                blacksmith = npc;
        }

        Sprite npcSprite = GetBlacksmithSprite();

        if (blacksmith == null)
        {
            // Place Blacksmith right in front of the middle house door!
            var bGo = new GameObject("Blacksmith");
            bGo.transform.position = BlacksmithSpawn;
            bGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var newSr = bGo.AddComponent<SpriteRenderer>();
            newSr.sprite = npcSprite;
            newSr.sortingOrder = 10;

            blacksmith = bGo.AddComponent<DialogueNPC>();
            blacksmith.range = 2.2f;
            blacksmith.promptOffset = new Vector3(0f, 1.2f, 0f);
            Debug.Log("[Wanderers] Created Blacksmith NPC at " + BlacksmithSpawn + " in front of the houses!");
        }

        blacksmith.name = "Blacksmith";
        blacksmith.npcId = "blacksmith";
        blacksmith.gameObject.SetActive(true);

        if (ironRequest != null && ironUpgraded != null)
        {
            blacksmith.conversations = new[] { ironRequest, ironUpgraded };
            blacksmith.dialogue = null;
        }
        else if (ironRequest != null)
        {
            blacksmith.conversations = null;
            blacksmith.dialogue = ironRequest;
        }

        var sr = blacksmith.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (npcSprite != null) sr.sprite = npcSprite;
            sr.sortingOrder = 10;
        }

        if (blacksmith.GetComponent<BlacksmithNPCMarker>() == null)
            blacksmith.gameObject.AddComponent<BlacksmithNPCMarker>();
        if (blacksmith.GetComponent<BlacksmithForgeNPC>() == null)
            blacksmith.gameObject.AddComponent<BlacksmithForgeNPC>();

        EditorUtility.SetDirty(blacksmith);
        Debug.Log("[Wanderers] Updated Blacksmith NPC at " + blacksmith.transform.position);
    }

    static Sprite GetBlacksmithSprite()
    {
        string[] candidates = new[]
        {
            "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Yellow Units/Pawn/Pawn_Idle Hammer.png",
            "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Yellow Units/Pawn/Pawn_Idle.png",
            "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Blue Units/Pawn/Pawn_Idle.png"
        };
        foreach (var path in candidates)
        {
            Sprite s = LoadFirstSprite(path);
            if (s != null) return s;
        }
        return null;
    }

    static Sprite LoadFirstSprite(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var o in assets)
        {
            if (o is Sprite s) return s;
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadSpriteByName(string path, string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var o in assets)
        {
            if (o is Sprite s && s.name == spriteName) return s;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash > 0)
        {
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
