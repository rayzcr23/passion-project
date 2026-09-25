using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds the passive sheep: clips, controller and prefab.
/// Sheep sheets are 128px cells, not the 192 the characters use.
public static class CritterBuilder
{
    const string Root = "Assets/Tiny Swords/Tiny Swords (Update 010)/Resources/Sheep/";
    const string IdleSheet = Root + "HappySheep_Idle.png";
    const string MoveSheet = Root + "HappySheep_Bouncing.png";
    const string OutRoot = "Assets/ANIMATIONS/Sheep/";
    const string ControllerPath = "Assets/ANIMATIONS/Sheep.controller";
    const string PrefabPath = "Assets/PREFABS/Sheep.prefab";
    const string DeathPoof = "Assets/PREFABS/DeathPoof.prefab";
    const int Cell = 128;
    const float FrameRate = 10f;

    [MenuItem("Tools/Wanderers/Build Sheep")]
    public static void Build()
    {
        EnsureFolder(OutRoot);

        if (!CharacterAnimationBuilder.SliceSheet(IdleSheet, Cell) ||
            !CharacterAnimationBuilder.SliceSheet(MoveSheet, Cell))
        {
            Debug.LogError("[Wanderers] Could not slice the sheep sheets");
            return;
        }
        AssetDatabase.Refresh();

        AnimationClip idle = BuildClip(IdleSheet, OutRoot + "Sheep_Idle.anim");
        AnimationClip move = BuildClip(MoveSheet, OutRoot + "Sheep_Move.anim");

        BuildController(idle, move);
        BuildPrefab();

        RepairSceneInstances();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Wanderers] Sheep built");
    }

    static AnimationClip BuildClip(string sheetPath, string clipPath)
    {
        List<Sprite> frames = SortedSprites(sheetPath);
        if (frames.Count == 0) { Debug.LogWarning("[Wanderers] No sprites in " + sheetPath); return null; }

        // Reuse the asset if it exists. CreateAsset over a live path mints a new
        // GUID, which silently breaks every prefab and controller pointing at it.
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNewClip = clip == null;
        if (isNewClip) clip = new AnimationClip();

        clip.frameRate = FrameRate;

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FrameRate, value = frames[i] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNewClip) AssetDatabase.CreateAsset(clip, clipPath);
        else EditorUtility.SetDirty(clip);

        return clip;
    }

    static void BuildController(AnimationClip idle, AnimationClip move)
    {
        // Same reasoning as the clips: keep the existing controller so its GUID,
        // and every reference to it, survives a rebuild.
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
        {
            foreach (var layer in existing.layers)
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.name == "Idle") state.state.motion = idle;
                    if (state.state.name == "Move") state.state.motion = move;
                }

            EditorUtility.SetDirty(existing);
            return;
        }

        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        c.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Idle");
        AnimatorState moveState = sm.AddState("Move");
        idleState.motion = idle;
        moveState.motion = move;
        sm.defaultState = idleState;

        AnimatorStateTransition go = idleState.AddTransition(moveState);
        go.hasExitTime = false; go.duration = 0f;
        go.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

        AnimatorStateTransition stop = moveState.AddTransition(idleState);
        stop.hasExitTime = false; stop.duration = 0f;
        stop.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
    }

    static void BuildPrefab()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
        {
            Debug.LogWarning("[Wanderers] No Enemy layer yet - run Wire Up Gameplay first");
            enemyLayer = 0;
        }

        bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
        GameObject go = isNew ? new GameObject("Sheep")
                              : PrefabUtility.LoadPrefabContents(PrefabPath);

        // Enemy here means "thing the sword can hit", not "hostile" - the sheep
        // has no brain that targets anyone, and goblins only ever seek players.
        go.layer = enemyLayer;

        var sr = Ensure<SpriteRenderer>(go);
        List<Sprite> frames = SortedSprites(IdleSheet);

        // Assigned unconditionally: a Missing reference reads as null here, and
        // leaving it alone is exactly how a broken prefab stays broken.
        if (frames.Count > 0) sr.sprite = frames[0];
        sr.sortingOrder = 1;

        var animator = Ensure<Animator>(go);
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        animator.applyRootMotion = false;

        var rb = Ensure<Rigidbody2D>(go);
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 5f;

        var col = Ensure<CircleCollider2D>(go);
        col.radius = 0.28f;

        Ensure<NetworkObject>(go);
        var nt = Ensure<NetworkTransform>(go);

        var ntSo = new SerializedObject(nt);
        foreach (string p in new[] { "SyncPositionZ", "SyncRotAngleX", "SyncRotAngleY",
                                     "SyncRotAngleZ", "SyncScaleX", "SyncScaleY", "SyncScaleZ" })
        {
            SerializedProperty prop = ntSo.FindProperty(p);
            if (prop != null) prop.boolValue = false;
        }
        ntSo.ApplyModifiedProperties();

        var netAnim = Ensure<NetworkAnimator>(go);
        var naSo = new SerializedObject(netAnim);
        SerializedProperty animProp = naSo.FindProperty("m_Animator");
        if (animProp != null) animProp.objectReferenceValue = animator;
        SerializedProperty authProp = naSo.FindProperty("AuthorityMode");
        if (authProp != null) authProp.enumValueIndex = 0;   // server drives the brain
        naSo.ApplyModifiedProperties();

        Health hp = Ensure<Health>(go);
        hp.maxHealth = 12;
        hp.creatureId = "sheep";                 // Kill objectives match on this, exactly
        hp.despawnOnDeath = true;
        hp.despawnDelay = 0.6f;

        Ensure<KnockbackReceiver>(go).recoverSeconds = 0.2f;
        Ensure<SheepBrain>(go);

        DamageFlash flash = Ensure<DamageFlash>(go);
        flash.health = hp;
        flash.flashColor = new Color(1f, 0.5f, 0.5f);
        flash.cameraShakeOnDamage = 0f;

        HealthBar bar = Ensure<HealthBar>(go);
        bar.health = hp;
        bar.hideWhenFull = true;
        bar.offset = new Vector3(0f, 0.6f, 0f);
        bar.width = 0.6f;
        bar.height = 0.09f;

        DeathEffect death = Ensure<DeathEffect>(go);
        death.effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPoof);
        death.effectScale = 0.5f;
        death.fadeSeconds = 0.4f;
        death.shrinkTo = 0.5f;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);

        if (isNew) Object.DestroyImmediate(go);
        else PrefabUtility.UnloadPrefabContents(go);
    }

    /// Sheep already placed in the scene keep whatever they were saved with, so a
    /// rebuild has to go and fix them too.
    static void RepairSceneInstances()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        List<Sprite> frames = SortedSprites(IdleSheet);
        Sprite first = frames.Count > 0 ? frames[0] : null;

        int repaired = 0;

        foreach (SheepBrain brain in Object.FindObjectsByType<SheepBrain>(FindObjectsSortMode.None))
        {
            bool changed = false;

            var sr = brain.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null && first != null) { sr.sprite = first; changed = true; }

            var anim = brain.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController == null && controller != null)
            {
                anim.runtimeAnimatorController = controller;
                changed = true;
            }

            if (changed) { EditorUtility.SetDirty(brain.gameObject); repaired++; }
        }

        if (repaired > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Wanderers] Repaired " + repaired + " sheep in the scene");
        }
    }

    static List<Sprite> SortedSprites(string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = new List<Sprite>();
        foreach (Object o in all) { var s = o as Sprite; if (s != null) sprites.Add(s); }

        sprites.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
        return sprites;
    }

    static int Index(string name)
    {
        int u = name.LastIndexOf('_');
        int n;
        return (u >= 0 && int.TryParse(name.Substring(u + 1), out n)) ? n : 0;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void EnsureFolder(string path)
    {
        path = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
