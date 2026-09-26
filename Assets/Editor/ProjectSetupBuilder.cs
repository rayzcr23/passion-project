using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Wires the gameplay pieces that would otherwise be a long Inspector session:
/// the Enemy layer, the player prefab's components and references, and a test
/// dummy to hit. Safe to run repeatedly - it only adds what is missing.
public static class ProjectSetupBuilder
{
    const string PlayerPrefab = "Assets/PREFABS/Warrior.prefab";
    const string EnemyPrefab = "Assets/PREFABS/Goblin.prefab";
    const string EnemyController = "Assets/ANIMATIONS/Goblin.controller";
    const string DeathPoofPrefab = "Assets/PREFABS/DeathPoof.prefab";
    const string ArrowSprite = "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Blue Units/Archer/Arrow.png";
    const string GoblinSheet = "Assets/Tiny Swords/Tiny Swords (Update 010)/Factions/Goblins/Troops/Torch/Red/Torch_Red.png";
    const string Controller = "Assets/ANIMATIONS/PlayerBase.controller";
    const string EnemyLayer = "Enemy";
    const string PlayerLayer = "Player";

    [MenuItem("Tools/Wanderers/Wire Up Gameplay")]
    public static void WireUp()
    {
        int enemyLayer = EnsureLayer(EnemyLayer);
        int playerLayer = EnsureLayer(PlayerLayer);

        if (enemyLayer < 0 || playerLayer < 0)
        {
            Debug.LogError("[Wanderers] No free layer slots for Enemy/Player");
            return;
        }

        SetupPlayer(enemyLayer, playerLayer);
        SetupGoblin(enemyLayer);
        WarnAboutMissingCreatureIds();
        CleanNetworkPrefabs();
        FixSceneCamera();
        FixSessionHud();

        // Same reasoning as StoryBuilder: scene edits that are only dirtied are
        // invisible to a build and to anyone reading the scene file.
        UnityEngine.SceneManagement.Scene active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) EditorSceneManager.SaveScene(active);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(string.Format("[Wanderers] Gameplay wired in '{0}'. Enemy={1}, Player={2}",
            active.name, enemyLayer, playerLayer));
    }

    // ---------------------------------------------------------------- layers

    static int EnsureLayer(string name)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return -1;

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        // 0-7 are reserved by Unity.
        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        return -1;
    }

    /// Camera.main resolves by TAG. An untagged camera silently pins AimInput to
    /// Vector2.right, so every attack fires east regardless of where you point.
    static void FixSceneCamera()
    {
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) { Debug.LogWarning("[Wanderers] No camera in the open scene"); return; }

        bool changed = false;

        if (!cam.CompareTag("MainCamera"))
        {
            cam.gameObject.tag = "MainCamera";
            changed = true;
            Debug.Log("[Wanderers] Tagged '" + cam.name + "' as MainCamera");
        }

        if (cam.GetComponent<CameraFollow>() == null)
        {
            cam.gameObject.AddComponent<CameraFollow>();
            changed = true;
            Debug.Log("[Wanderers] Added CameraFollow to '" + cam.name + "'");
        }

        // Top-down 2D wants sprites sorted by Y, not Z: a character below a
        // building should draw in front of it, above should draw behind. Set on
        // the camera rather than GraphicsSettings so it lives in the scene.
        if (cam.transparencySortMode != TransparencySortMode.CustomAxis)
        {
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
            changed = true;
            Debug.Log("[Wanderers] Camera now sorts sprites by Y axis");
        }

        if (!cam.orthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            changed = true;
            Debug.Log("[Wanderers] Set camera '" + cam.name + "' to orthographic (size 5)");
        }

        if (changed) EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
    }

    /// Relay lets players on other devices actually connect; NetworkHUD only ever
    /// reaches 127.0.0.1.
    static void FixSessionHud()
    {
        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) { Debug.LogWarning("[Wanderers] No NetworkManager in the open scene"); return; }

        if (nm.GetComponent<SessionHUD>() != null)
        {
            if (nm.GetComponent<NetworkDiagnostics>() == null)
            {
                nm.gameObject.AddComponent<NetworkDiagnostics>();
                EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
            }
            EnsureQuestSystem(nm);
            EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
            return;
        }

        SessionHUD hud = nm.gameObject.AddComponent<SessionHUD>();
        hud.maxPlayers = 3;
        hud.localHud = nm.GetComponent<NetworkHUD>();

        if (nm.GetComponent<NetworkDiagnostics>() == null)
            nm.gameObject.AddComponent<NetworkDiagnostics>();

        EnsureQuestSystem(nm);

        EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
        Debug.Log("[Wanderers] Added SessionHUD for online play");
    }

    /// A killable creature with no creatureId reports nothing when it dies, so
    /// Kill objectives silently never advance. That is invisible at runtime, so
    /// flag it here instead.
    static void WarnAboutMissingCreatureIds()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/PREFABS" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            var health = go.GetComponent<Health>();
            if (health == null) continue;

            // Players are deliberately blank - they are not quest targets.
            bool isCreature = go.GetComponent<EnemyBrain>() != null
                           || go.GetComponent<SheepBrain>() != null;

            if (isCreature && string.IsNullOrEmpty(health.creatureId))
                Debug.LogWarning("[Wanderers] " + go.name + " has no creatureId, so killing it " +
                                 "will never advance a Kill quest.", go);
        }
    }

    /// Fills QuestSystem's lookup list from every QuestDefinition in the project.
    /// Public so StoryBuilder can call it straight after creating a quest asset.
    public static void RegisterAllQuests()
    {
        QuestManager qm = Object.FindFirstObjectByType<QuestManager>();
        if (qm == null) return;

        var found = new System.Collections.Generic.List<QuestDefinition>();

        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (quest != null) found.Add(quest);
        }

        qm.quests = found.ToArray();
        EditorUtility.SetDirty(qm);
    }

    /// QuestManager needs a NetworkObject of its own: quest state is replicated,
    /// and the NetworkManager object itself is not a spawnable network object.
    static void EnsureQuestSystem(NetworkManager nm)
    {
        QuestManager qm = Object.FindFirstObjectByType<QuestManager>();

        if (qm == null)
        {
            var go = new GameObject("QuestSystem");
            go.AddComponent<NetworkObject>();
            qm = go.AddComponent<QuestManager>();
            Debug.Log("[Wanderers] Added QuestSystem");
        }

        // A quest missing from this list still starts, but cannot be looked up -
        // so it shows its raw id, draws no progress bar, and never advances.
        // Far too quiet a failure to leave to a manual drag.
        string[] questGuids = AssetDatabase.FindAssets("t:QuestDefinition");
        var found = new System.Collections.Generic.List<QuestDefinition>();

        foreach (string guid in questGuids)
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (quest != null) found.Add(quest);
        }

        qm.quests = found.ToArray();
        EditorUtility.SetDirty(qm);

        // Quest state is keyed by questId, so two assets sharing one id become a
        // single entry wearing two hats: whichever definition loads first decides
        // the title and the required count, and the other silently does nothing.
        var seen = new System.Collections.Generic.Dictionary<string, QuestDefinition>();

        foreach (QuestDefinition q in found)
        {
            if (string.IsNullOrEmpty(q.questId))
            {
                Debug.LogError("[Wanderers] " + q.name + " has a blank questId.", q);
                continue;
            }

            QuestDefinition other;
            if (seen.TryGetValue(q.questId, out other))
                Debug.LogError("[Wanderers] '" + q.name + "' and '" + other.name +
                               "' share questId '" + q.questId + "'. They will behave as " +
                               "one quest. Give each a unique id.", q);
            else
                seen[q.questId] = q;
        }

        Debug.Log("[Wanderers] QuestSystem knows " + found.Count + " quest(s)");

        if (Object.FindFirstObjectByType<SkillTreeUI>() == null)
        {
            var tree = new GameObject("SkillTreeUI");
            tree.AddComponent<SkillTreeUI>();
            Debug.Log("[Wanderers] Added SkillTreeUI - press K in play mode");
        }

        if (Object.FindFirstObjectByType<DialogueUI>() == null)
        {
            var ui = new GameObject("DialogueUI");
            ui.AddComponent<DialogueUI>();
            Debug.Log("[Wanderers] Added DialogueUI");
        }
    }

    /// Deleting a prefab leaves a null entry behind in the auto-populated list,
    /// which NGO complains about on startup.
    static void CleanNetworkPrefabs()
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>("Assets/DefaultNetworkPrefabs.asset");
        if (asset == null) return;

        var so = new SerializedObject(asset);
        SerializedProperty list = so.FindProperty("List");
        if (list == null || !list.isArray) return;

        int removed = 0;
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty prefab = list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
            if (prefab != null && prefab.objectReferenceValue == null)
            {
                list.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }

        if (removed > 0)
        {
            so.ApplyModifiedProperties();
            Debug.Log("[Wanderers] Removed " + removed + " dead network prefab entries");
        }
    }

    /// Drops a configured enemy camp wherever the Scene view is looking.
    [MenuItem("Tools/Wanderers/Add Enemy Camp Here")]
    public static void AddEnemyCamp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
        if (prefab == null)
        {
            Debug.LogError("[Wanderers] No Goblin prefab yet - run Wire Up Gameplay first.");
            return;
        }

        Vector3 position = Vector3.zero;
        if (SceneView.lastActiveSceneView != null) position = SceneView.lastActiveSceneView.pivot;
        position.z = 0f;

        var go = new GameObject("EnemyCamp");
        go.transform.position = position;

        var spawner = go.AddComponent<EnemySpawner>();
        spawner.enemyPrefab = prefab;
        spawner.maxAlive = 4;
        spawner.spawnInterval = 4f;
        spawner.spawnRadius = 3f;
        spawner.activationRange = 18f;

        Undo.RegisterCreatedObjectUndo(go, "Add Enemy Camp");
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log("[Wanderers] Enemy camp added at " + position);
    }

    // ---------------------------------------------------------------- player

    static void SetupPlayer(int enemyLayer, int playerLayer)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        if (root == null) { Debug.LogError("[Wanderers] Missing " + PlayerPrefab); return; }

        root.layer = playerLayer;

        // Swap in the generated controller if the builder has been run.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        var animator = root.GetComponent<Animator>();
        if (controller != null && animator != null) animator.runtimeAnimatorController = controller;

        // NetworkAnimator ships with its Animator reference empty, which null-refs
        // every frame in CheckParametersChanged. Owner authority to match
        // NetworkTransform, since Movement drives the parameters on the owner.
        var netAnimator = root.GetComponent<NetworkAnimator>();
        if (netAnimator != null && animator != null)
        {
            var so = new SerializedObject(netAnimator);

            SerializedProperty animProp = so.FindProperty("m_Animator");
            if (animProp != null) animProp.objectReferenceValue = animator;

            SerializedProperty authProp = so.FindProperty("AuthorityMode");
            if (authProp != null) authProp.enumValueIndex = 1;   // Owner

            so.ApplyModifiedProperties();
        }

        TrimNetworkTransform(root);

        // One colour per brother, assigned by the server at spawn.
        PlayerColour colour = Ensure<PlayerColour>(root);
        string[] names = { "Blue", "Red", "Yellow" };
        var variants = new System.Collections.Generic.List<RuntimeAnimatorController>();

        foreach (string n in names)
        {
            var ovr = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/ANIMATIONS/" + n + ".overrideController");

            if (ovr != null) variants.Add(ovr);
            else Debug.LogWarning("[Wanderers] Missing " + n + ".overrideController - run Build Character Animations");
        }
        colour.variants = variants.ToArray();

        AimInput aim = Ensure<AimInput>(root);
        aim.aimOrigin = root.transform;
        aim.debugDrawAim = true;          // magenta ray = live aim direction

        Health health = Ensure<Health>(root);
        health.maxHealth = 100;
        health.despawnOnDeath = false;          // players stay to be revived

        MeleeArcAbility swing = Ensure<MeleeArcAbility>(root);
        swing.displayName = "Sword Swing";
        swing.cooldown = 0.45f;
        swing.animatorTrigger = "Attack";
        swing.origin = root.transform;          // no pivot needed: the swing anim sells it
        swing.range = 1.4f;
        // Facing is left/right only, so the arc has to cover the whole front half
        // or enemies above and below you sit outside it.
        swing.arcDegrees = 180f;
        swing.damage = 10;
        swing.windupSeconds = 0.15f;
        swing.pointBlankRadius = 1.0f;   // must exceed the goblin's 0.9 attack range
        swing.debugDraw = true;
        swing.debugLog = true;
        swing.knockback = 4.5f;                           // punchier now that hitstun lets it read
        swing.friendlyFire = true;                        // brothers can duel
        // Both layers, or the swing would pass straight through other players.
        swing.hitLayers = (1 << enemyLayer) | (1 << playerLayer);

        PlayerDeath death = Ensure<PlayerDeath>(root);
        death.respawnSeconds = 5f;
        death.respawnAtStart = true;

        Rigidbody2D playerRb = Ensure<Rigidbody2D>(root);
        playerRb.gravityScale = 0f;
        playerRb.freezeRotation = true;
        playerRb.linearDamping = 4f;

        KnockbackReceiver recoil = Ensure<KnockbackReceiver>(root);
        recoil.recoverSeconds = 0.2f;

        DamageFlash flash = Ensure<DamageFlash>(root);
        flash.health = health;
        flash.cameraShakeOnDamage = 0.4f;                 // getting hit shakes your view
        flash.flashColor = new Color(1f, 0.35f, 0.35f);

        HealthBar bar = Ensure<HealthBar>(root);
        bar.health = health;
        bar.hideWhenFull = false;                         // always show your own
        bar.offset = new Vector3(0f, 0.95f, 0f);

        GuardAbility guard = Ensure<GuardAbility>(root);
        guard.useRightMouse = true;
        guard.blockAllDirections = true;
        guard.moveSpeedMultiplier = 0.35f;

        // Special: ring of small swords (PRD).
        OrbitRingAbility ring = Ensure<OrbitRingAbility>(root);
        ring.displayName = "Ring of Swords";
        ring.cooldown = 10f;
        ring.duration = 6f;
        ring.damage = 5;
        ring.radius = 1.4f;
        ring.bladeCount = 6;
        ring.hitLayers = (1 << enemyLayer);
        ring.bladeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSprite);

        // Super: Hollow Purple. Replaces the ground crack - a corridor rather
        // than a ring, so it rewards lining enemies up.
        var oldCrack = root.GetComponent<GroundCrackAbility>();
        if (oldCrack != null) Object.DestroyImmediate(oldCrack, true);

        HollowPurpleAbility purple = Ensure<HollowPurpleAbility>(root);
        purple.displayName = "Hollow Purple";
        purple.cooldown = 1f;                 // POW is the real gate, not time
        purple.powerCost = 100f;              // a full meter
        purple.animatorTrigger = "Attack2";
        purple.length = 22f;
        purple.width = 2.4f;
        purple.hitLayers = (1 << enemyLayer);
        purple.oneShotsNonBosses = true;
        purple.impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPoofPrefab);

        PowerMeter meter = Ensure<PowerMeter>(root);
        meter.max = 100f;

        // The sheep mount. Frames come from the same bouncing sheet the critter
        // uses, so the thing you ride animates like the thing you killed.
        SkillTree skills = Ensure<SkillTree>(root);
        skills.seed = 1337;

        MountState mount = Ensure<MountState>(root);
        mount.speedMultiplier = 2.2f;
        mount.mountScale = 1.9f;
        mount.mountOffset = new Vector3(0f, -0.46f, 0f);
        mount.mountFrames = LoadSheepFrames();

        // The ring is a mid-cost spender now, so POW has two sinks.
        ring.powerCost = 35f;

        AbilityLoadout loadout = Ensure<AbilityLoadout>(root);
        loadout.aim = aim;
        loadout.m1 = swing;
        loadout.aimMode = AbilityLoadout.AimMode.Facing;   // hitbox sits in front of the sprite
        loadout.special = ring;                            // Q
        loadout.super = purple;                            // E
        loadout.death = death;
        loadout.guard = guard;

        Movement move = root.GetComponent<Movement>();
        if (move != null)
        {
            move.aim = aim;
            move.death = death;
            move.faceAimAlways = false;   // face where you walk; attacks still aim at the cursor
            move.knockback = recoil;
            move.guard = guard;
            if (move.body == null) move.body = root.GetComponent<SpriteRenderer>();
        }

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// A 2D game has no business replicating Z position, any rotation, or scale.
    /// Scale in particular is written every time DamageFlash punches, and rotation
    /// every time DeathEffect tips a body over - both of which already run locally
    /// on each client, so syncing them is pure waste.
    static void TrimNetworkTransform(GameObject go)
    {
        var nt = go.GetComponent<NetworkTransform>();
        if (nt == null) return;

        var so = new SerializedObject(nt);
        string[] off =
        {
            "SyncPositionZ",
            "SyncRotAngleX", "SyncRotAngleY", "SyncRotAngleZ",
            "SyncScaleX", "SyncScaleY", "SyncScaleZ"
        };

        foreach (string name in off)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.boolValue = false;
        }
        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------ test dummy

    static void SetupGoblin(int enemyLayer)
    {
        bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab) == null;

        GameObject go = isNew ? new GameObject("Goblin")
                              : PrefabUtility.LoadPrefabContents(EnemyPrefab);
        go.layer = enemyLayer;

        var sr = Ensure<SpriteRenderer>(go);
        if (sr.sprite == null) sr.sprite = FirstSpriteOf(GoblinSheet);
        sr.sortingOrder = 1;

        var animator = Ensure<Animator>(go);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyController);
        if (controller != null) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        var rb = Ensure<Rigidbody2D>(go);
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 6f;                    // knockback decays instead of sliding forever

        var col = Ensure<CircleCollider2D>(go);
        col.radius = 0.35f;

        Ensure<NetworkObject>(go);
        Ensure<NetworkTransform>(go);

        // Server authority: EnemyBrain runs on the server, so the server drives
        // the animator and replicates it out. Unity leaves m_Animator empty.
        var netAnimator = Ensure<NetworkAnimator>(go);
        var so = new SerializedObject(netAnimator);
        SerializedProperty animProp = so.FindProperty("m_Animator");
        if (animProp != null) animProp.objectReferenceValue = animator;
        SerializedProperty authProp = so.FindProperty("AuthorityMode");
        if (authProp != null) authProp.enumValueIndex = 0;   // Server
        so.ApplyModifiedProperties();

        Health hp = Ensure<Health>(go);
        hp.maxHealth = 30;
        hp.creatureId = "goblin";                 // for Kill quest objectives
        hp.despawnOnDeath = true;                 // enemies leave the world
        hp.despawnDelay = 0.6f;                   // long enough for DeathEffect to play

        DeathEffect death = Ensure<DeathEffect>(go);
        death.effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPoofPrefab);
        death.effectScale = 0.7f;
        death.fadeSeconds = 0.45f;
        death.shrinkTo = 0.55f;
        death.spinDegrees = 25f;
        if (death.effectPrefab == null)
            Debug.LogWarning("[Wanderers] No DeathPoof yet - run Build Death Effect first.");

        ConfigureBrain(Ensure<EnemyBrain>(go));
        ConfigureEnemyBar(Ensure<HealthBar>(go), hp);

        TrimNetworkTransform(go);
        Ensure<KnockbackReceiver>(go).recoverSeconds = 0.15f;

        DamageFlash enemyFlash = Ensure<DamageFlash>(go);
        enemyFlash.health = hp;
        enemyFlash.cameraShakeOnDamage = 0f;              // enemies never shake your camera
        enemyFlash.flashColor = Color.white;              // reads well on the red goblin

        PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefab);

        if (isNew) Object.DestroyImmediate(go);
        else PrefabUtility.UnloadPrefabContents(go);
    }

    static void ConfigureBrain(EnemyBrain brain)
    {
        brain.aggroRange = 6f;
        brain.attackRange = 0.9f;
        brain.giveUpRange = 10f;
        brain.moveSpeed = 2f;
        brain.damage = 5;
        brain.attackCooldown = 1.2f;
        brain.knockback = 3.5f;
    }

    static void ConfigureEnemyBar(HealthBar bar, Health hp)
    {
        bar.health = hp;
        bar.hideWhenFull = true;          // only appears once hurt, so the map stays clean
        bar.offset = new Vector3(0f, 0.85f, 0f);
        bar.width = 0.7f;
        bar.height = 0.1f;
    }

    static Sprite[] LoadSheepFrames()
    {
        const string sheet = "Assets/Tiny Swords/Tiny Swords (Update 010)/Resources/Sheep/HappySheep_Bouncing.png";

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(sheet);
        var frames = new System.Collections.Generic.List<Sprite>();

        foreach (Object o in all) { var s = o as Sprite; if (s != null) frames.Add(s); }

        if (frames.Count == 0)
        {
            Debug.LogWarning("[Wanderers] Sheep sheet not sliced yet - run Build Sheep first.");
            return new Sprite[0];
        }

        frames.Sort((a, b) =>
        {
            int ia = a.name.LastIndexOf('_'), ib = b.name.LastIndexOf('_');
            int na, nb;
            int.TryParse(ia >= 0 ? a.name.Substring(ia + 1) : "0", out na);
            int.TryParse(ib >= 0 ? b.name.Substring(ib + 1) : "0", out nb);
            return na.CompareTo(nb);
        });

        return frames.ToArray();
    }

    static Sprite FirstSpriteOf(string sheetPath)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        var found = new List<Sprite>();
        foreach (Object o in all) { var s = o as Sprite; if (s != null) found.Add(s); }
        if (found.Count == 0) return null;

        found.Sort((a, b) => a.name.CompareTo(b.name));
        return found[0];
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
