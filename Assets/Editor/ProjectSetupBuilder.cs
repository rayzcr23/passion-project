using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Wires the gameplay pieces that would otherwise be a long Inspector session:
/// the Enemy layer, the player prefab's components and references, and a test
/// dummy to hit. Safe to run repeatedly - it only adds what is missing.
public static class ProjectSetupBuilder
{
    const string PlayerPrefab = "Assets/PREFABS/Warrior.prefab";
    const string EnemyPrefab = "Assets/PREFABS/TestDummy.prefab";
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
        SetupTestDummy(enemyLayer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(string.Format("[Wanderers] Gameplay wired. Enemy={0}, Player={1}", enemyLayer, playerLayer));
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

        AimInput aim = Ensure<AimInput>(root);
        aim.aimOrigin = root.transform;

        Health health = Ensure<Health>(root);
        health.maxHealth = 100;
        health.despawnOnDeath = false;          // players stay to be revived

        MeleeArcAbility swing = Ensure<MeleeArcAbility>(root);
        swing.displayName = "Sword Swing";
        swing.cooldown = 0.45f;
        swing.animatorTrigger = "Attack";
        swing.origin = root.transform;          // no pivot needed: the swing anim sells it
        swing.range = 1.4f;
        swing.arcDegrees = 120f;
        swing.damage = 10;
        swing.windupSeconds = 0.15f;
        swing.knockback = 2f;
        swing.friendlyFire = true;                        // brothers can duel
        // Both layers, or the swing would pass straight through other players.
        swing.hitLayers = (1 << enemyLayer) | (1 << playerLayer);

        PlayerDeath death = Ensure<PlayerDeath>(root);
        death.respawnSeconds = 5f;
        death.respawnAtStart = true;

        HealthBar bar = Ensure<HealthBar>(root);
        bar.health = health;
        bar.hideWhenFull = false;                         // always show your own
        bar.offset = new Vector3(0f, 0.95f, 0f);

        AbilityLoadout loadout = Ensure<AbilityLoadout>(root);
        loadout.aim = aim;
        loadout.m1 = swing;
        loadout.death = death;

        Movement move = root.GetComponent<Movement>();
        if (move != null)
        {
            move.aim = aim;
            move.death = death;
            if (move.body == null) move.body = root.GetComponent<SpriteRenderer>();
        }

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------ test dummy

    static void SetupTestDummy(int enemyLayer)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
        if (existing != null)
        {
            GameObject loaded = PrefabUtility.LoadPrefabContents(EnemyPrefab);
            loaded.layer = enemyLayer;
            ConfigureBrain(Ensure<EnemyBrain>(loaded));
            ConfigureEnemyBar(Ensure<HealthBar>(loaded), loaded.GetComponent<Health>());
            PrefabUtility.SaveAsPrefabAsset(loaded, EnemyPrefab);
            PrefabUtility.UnloadPrefabContents(loaded);
            return;
        }

        var go = new GameObject("TestDummy");
        go.layer = enemyLayer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FirstSpriteOf(
            "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Blue Units/Warrior/Warrior_Idle.png");
        sr.color = new Color(1f, 0.45f, 0.45f);   // red tint so it reads as hostile
        sr.sortingOrder = 1;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 6f;                    // knockback decays instead of sliding forever

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.35f;

        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();

        Health hp = go.AddComponent<Health>();
        hp.maxHealth = 30;
        hp.despawnOnDeath = true;                 // enemies leave the world

        ConfigureBrain(go.AddComponent<EnemyBrain>());
        ConfigureEnemyBar(go.AddComponent<HealthBar>(), hp);

        PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefab);
        Object.DestroyImmediate(go);
    }

    static void ConfigureBrain(EnemyBrain brain)
    {
        brain.aggroRange = 6f;
        brain.attackRange = 0.9f;
        brain.giveUpRange = 10f;
        brain.moveSpeed = 2f;
        brain.damage = 5;
        brain.attackCooldown = 1.2f;
    }

    static void ConfigureEnemyBar(HealthBar bar, Health hp)
    {
        bar.health = hp;
        bar.hideWhenFull = true;          // only appears once hurt, so the map stays clean
        bar.offset = new Vector3(0f, 0.85f, 0f);
        bar.width = 0.7f;
        bar.height = 0.1f;
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
