using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

/// Creates the network prefabs for the Witchcraft Village skeleton encounter.
[InitializeOnLoad]
public static class SkeletonBossSetup
{
    const string BossPath = "Assets/Resources/PREFABS/SkeletonBoss.prefab";
    const string OrbPath = "Assets/Resources/PREFABS/SkeletonOrb.prefab";
    const string SpriteFolder = "Assets/skelatonboss/";
    const string IdlePath = SpriteFolder + "Skeleton (Witchcraft Village Boss) (2) idle.png";
    const string WalkPath = SpriteFolder + "Skeleton (Witchcraft Village Boss) (1) walk.png";
    const string StompPath = SpriteFolder + "Skeleton (Witchcraft Village Boss) stomp.png";
    const string SessionKey = "Wanderers.SkeletonBossSetup.v1";

    static SkeletonBossSetup()
    {
        EditorApplication.delayCall += SetupOnce;
    }

    static void SetupOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += SetupOnce;
            return;
        }

        SessionState.SetBool(SessionKey, true);
        Setup();
    }

    [MenuItem("Tools/Wanderers/Setup Witchcraft Skeleton Boss")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/PREFABS"))
            AssetDatabase.CreateFolder("Assets/Resources", "PREFABS");

        Sprite[] idle = LoadFrames(IdlePath);
        Sprite[] walk = LoadFrames(WalkPath);
        Sprite[] stomp = LoadFrames(StompPath);
        if (idle.Length == 0 || walk.Length == 0 || stomp.Length == 0)
        {
            Debug.LogError("[Wanderers] Skeleton sprite sheets are missing or not sliced.");
            return;
        }

        GameObject orb = EnsureOrbPrefab();
        GameObject boss = EnsureBossPrefab(idle, walk, stomp, orb);
        RegisterNetworkPrefab(orb);
        RegisterNetworkPrefab(boss);
        AssetDatabase.SaveAssets();
        Debug.Log("[Wanderers] Witchcraft Village skeleton encounter prefabs are ready.");
    }

    static Sprite[] LoadFrames(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .Where(sprite => sprite.name.LastIndexOf('_') >= 0)
            .OrderBy(sprite => FrameNumber(sprite.name))
            .ToArray();
    }

    static int FrameNumber(string name)
    {
        int number;
        return int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out number)
            ? number : int.MaxValue;
    }

    static GameObject EnsureOrbPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPath);
        if (existing != null) return existing;

        var root = new GameObject("SkeletonOrb");
        root.AddComponent<SpriteRenderer>().sortingOrder = 31;
        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        root.AddComponent<NetworkObject>();
        ConfigureNetworkTransform(root.AddComponent<NetworkTransform>());
        root.AddComponent<SkeletonHomingOrb>();
        PrefabUtility.SaveAsPrefabAsset(root, OrbPath);
        UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(OrbPath);
    }

    static GameObject EnsureBossPrefab(Sprite[] idle, Sprite[] walk, Sprite[] stomp, GameObject orb)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        bool existing = root != null;
        if (existing) root = PrefabUtility.LoadPrefabContents(BossPath);
        else root = new GameObject("SkeletonBoss");

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;

        var sprite = GetOrAdd<SpriteRenderer>(root);
        sprite.sprite = idle[0];
        sprite.sortingOrder = 28;

        var body = GetOrAdd<Rigidbody2D>(root);
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.linearDamping = 8f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = GetOrAdd<CircleCollider2D>(root);
        collider.radius = 0.42f;
        collider.isTrigger = true;

        GetOrAdd<NetworkObject>(root);
        ConfigureNetworkTransform(GetOrAdd<NetworkTransform>(root));

        var health = GetOrAdd<Health>(root);
        health.maxHealth = 3200;
        health.creatureId = "skeleton_boss";
        health.isBoss = true;
        health.despawnOnDeath = true;
        health.despawnDelay = 1.5f;

        var flash = GetOrAdd<DamageFlash>(root);
        flash.flashColor = Color.white;
        flash.punchAmount = 0.1f;

        var bar = GetOrAdd<HealthBar>(root);
        bar.health = health;
        bar.offset = new Vector3(0f, 0.8f, 0f);
        bar.width = 1.4f;
        bar.height = 0.13f;
        bar.hideWhenFull = false;

        GetOrAdd<DeathEffect>(root);
        var boss = GetOrAdd<SkeletonBoss>(root);
        boss.idleFrames = idle;
        boss.walkFrames = walk;
        boss.stompFrames = stomp;
        boss.orbPrefab = orb.GetComponent<SkeletonHomingOrb>();

        PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        if (existing) PrefabUtility.UnloadPrefabContents(root);
        else UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
    }

    static T GetOrAdd<T>(GameObject root) where T : Component
    {
        var component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    static void ConfigureNetworkTransform(NetworkTransform transform)
    {
        transform.UseUnreliableDeltas = true;
        transform.PositionThreshold = 0.025f;
        transform.SyncPositionX = true;
        transform.SyncPositionY = true;
        transform.SyncPositionZ = false;
        transform.SyncRotAngleX = false;
        transform.SyncRotAngleY = false;
        transform.SyncRotAngleZ = false;
        transform.SyncScaleX = false;
        transform.SyncScaleY = false;
        transform.SyncScaleZ = false;
    }

    static void RegisterNetworkPrefab(GameObject prefab)
    {
        if (prefab == null) return;
        var listAsset = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (listAsset == null)
        {
            Debug.LogError("[Wanderers] DefaultNetworkPrefabs.asset is missing.");
            return;
        }

        var serialized = new SerializedObject(listAsset);
        var list = serialized.FindProperty("List");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)
                return;

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(listAsset);
    }
}
