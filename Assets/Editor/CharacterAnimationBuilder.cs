using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// One-click setup for the player character. All three players are swordsman
/// brothers, so there is a single class - which means the animation budget goes
/// into depth (two attacks and a guard) rather than three shallow kits.
///
/// Lives in Assets/Editor so it is stripped from player builds.
public static class CharacterAnimationBuilder
{
    const string UnitsRoot = "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Blue Units/";
    const string OutRoot = "Assets/ANIMATIONS/";
    const int Cell = 192;
    const float FrameRate = 12f;

    class ClipDef
    {
        public string name;       // clip suffix
        public string sheet;      // path under UnitsRoot
        public bool loop;
        public ClipDef(string n, string s, bool l) { name = n; sheet = s; loop = l; }
    }

    // Git history has the three-class version (Wizard/Monk, Assassin/Archer) if
    // the brothers ever need different fighting styles.
    /// One brother per colour, so three identical warriors are tellable apart.
    static readonly string[] Colours = { "Blue", "Red", "Yellow" };

    static readonly ClipDef[] Clips =
    {
        new ClipDef("Idle",    "Warrior/Warrior_Idle.png",    true),
        new ClipDef("Run",     "Warrior/Warrior_Run.png",     true),
        new ClipDef("Attack",  "Warrior/Warrior_Attack1.png", false),
        new ClipDef("Attack2", "Warrior/Warrior_Attack2.png", false),
        new ClipDef("Guard",   "Warrior/Warrior_Guard.png",   true),
    };

    [MenuItem("Tools/Wanderers/Build Character Animations")]
    public static void BuildAll()
    {
        EnsureFolder(OutRoot);
        EnsureFolder(OutRoot + "Swordsman");

        int sliced = 0;
        var seen = new HashSet<string>();
        foreach (ClipDef c in Clips)
            if (seen.Add(UnitsRoot + c.sheet) && SliceSheet(UnitsRoot + c.sheet)) sliced++;

        AssetDatabase.Refresh();

        var built = new Dictionary<string, AnimationClip>();
        foreach (ClipDef def in Clips)
        {
            AnimationClip clip = BuildClip(UnitsRoot + def.sheet,
                                           OutRoot + "Swordsman/Swordsman_" + def.name + ".anim",
                                           def.loop);
            if (clip != null) built[def.name] = clip;
        }

        BuildController(built);
        BuildColourVariants();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[Wanderers] Sliced {0} sheets, built {1} clips and PlayerBase.controller in {2}",
            sliced, built.Count, OutRoot));
    }

    /// Builds one clip set and AnimatorOverrideController per colour. The base
    /// controller holds the state machine; each variant only swaps clips, so a
    /// change to the machine is inherited by every colour.
    static void BuildColourVariants()
    {
        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            OutRoot + "PlayerBase.controller");
        if (baseController == null) { Debug.LogError("[Wanderers] No PlayerBase.controller"); return; }

        foreach (string colour in Colours)
        {
            string folder = OutRoot + colour;
            EnsureFolder(folder);

            var clips = new Dictionary<string, AnimationClip>();

            foreach (ClipDef def in Clips)
            {
                string sheet = UnitsRoot + colour + " Units/Warrior/"
                             + System.IO.Path.GetFileName(def.sheet);

                if (!SliceSheet(sheet)) continue;

                AnimationClip clip = BuildClip(sheet,
                    folder + "/" + colour + "_" + def.name + ".anim", def.loop);

                if (clip != null) clips[def.name] = clip;
            }

            var ovr = new AnimatorOverrideController(baseController);
            ovr.name = colour;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            ovr.GetOverrides(pairs);

            for (int i = 0; i < pairs.Count; i++)
            {
                string key = pairs[i].Key != null ? pairs[i].Key.name : "";
                foreach (ClipDef def in Clips)
                {
                    if (!key.EndsWith("_" + def.name)) continue;

                    AnimationClip replacement;
                    if (clips.TryGetValue(def.name, out replacement))
                        pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, replacement);
                    break;
                }
            }
            ovr.ApplyOverrides(pairs);

            string path = OutRoot + colour + ".overrideController";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(ovr, path);
        }

        Debug.Log("[Wanderers] Built " + Colours.Length + " colour variants");
    }

    /// Shared with EnemyAnimationBuilder.
    public static bool SliceSheet(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("[Wanderers] Not found: " + path); return false; }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return false;

        int cols = tex.width / Cell;
        int rows = Mathf.Max(1, tex.height / Cell);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;   // crisp pixels, not blurry
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
        var meta = new List<SpriteMetaData>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var m = new SpriteMetaData();
                m.name = string.Format("{0}_{1}", baseName, r * cols + c);
                // Texture space starts bottom-left, so walk rows from the top.
                m.rect = new Rect(c * Cell, tex.height - (r + 1) * Cell, Cell, Cell);
                m.alignment = (int)SpriteAlignment.Center;
                m.pivot = new Vector2(0.5f, 0.5f);
                meta.Add(m);
            }
        }

#pragma warning disable 0618
        importer.spritesheet = meta.ToArray();
#pragma warning restore 0618

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return true;
    }

    static AnimationClip BuildClip(string sheetPath, string clipPath, bool loop)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        var frames = new List<Sprite>();
        foreach (Object o in assets) { var s = o as Sprite; if (s != null) frames.Add(s); }

        if (frames.Count == 0) { Debug.LogWarning("[Wanderers] No sprites in " + sheetPath); return null; }

        // "_10" must sort after "_9", so compare the trailing index numerically.
        frames.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));

        var clip = new AnimationClip { frameRate = FrameRate };

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
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    static int IndexOf(string spriteName)
    {
        int u = spriteName.LastIndexOf('_');
        int n;
        return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out n)) ? n : 0;
    }

    static void BuildController(Dictionary<string, AnimationClip> clips)
    {
        string path = OutRoot + "PlayerBase.controller";
        AssetDatabase.DeleteAsset(path);

        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Keep Horizontal/Vertical: Movement.cs already drives them.
        c.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        c.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Guard", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle");
        AnimatorState run = sm.AddState("Run");
        AnimatorState attack = sm.AddState("Attack");
        AnimatorState attack2 = sm.AddState("Attack2");
        AnimatorState guard = sm.AddState("Guard");

        idle.motion = Get(clips, "Idle");
        run.motion = Get(clips, "Run");
        attack.motion = Get(clips, "Attack");
        attack2.motion = Get(clips, "Attack2");
        guard.motion = Get(clips, "Guard");

        sm.defaultState = idle;

        // Mecanim conditions are AND-only, so moving on either axis needs its own transition.
        AddMoveTransition(idle, run, "Horizontal");
        AddMoveTransition(idle, run, "Vertical");

        AnimatorStateTransition stop = run.AddTransition(idle);
        stop.hasExitTime = false;
        stop.duration = 0f;
        stop.AddCondition(AnimatorConditionMode.Less, 0.01f, "Horizontal");
        stop.AddCondition(AnimatorConditionMode.Less, 0.01f, "Vertical");

        AddTriggerEntry(sm, attack, "Attack");
        AddTriggerEntry(sm, attack2, "Attack2");

        // Guard is held rather than fired, so it is a bool both ways.
        AnimatorStateTransition toGuard = sm.AddAnyStateTransition(guard);
        toGuard.hasExitTime = false;
        toGuard.duration = 0f;
        toGuard.canTransitionToSelf = false;
        toGuard.AddCondition(AnimatorConditionMode.If, 0f, "Guard");

        AnimatorStateTransition unguard = guard.AddTransition(idle);
        unguard.hasExitTime = false;
        unguard.duration = 0f;
        unguard.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");

        ReturnToIdle(attack, idle);
        ReturnToIdle(attack2, idle);
    }

    static void AddTriggerEntry(AnimatorStateMachine sm, AnimatorState state, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(state);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void ReturnToIdle(AnimatorState from, AnimatorState idle)
    {
        AnimatorStateTransition t = from.AddTransition(idle);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0f;
    }

    static void AddMoveTransition(AnimatorState from, AnimatorState to, string parameter)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.01f, parameter);
    }

    static AnimationClip Get(Dictionary<string, AnimationClip> d, string k)
    {
        AnimationClip c;
        return d.TryGetValue(k, out c) ? c : null;
    }

    static void EnsureFolder(string path)
    {
        path = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
