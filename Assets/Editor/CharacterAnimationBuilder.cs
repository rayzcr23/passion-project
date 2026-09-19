using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// One-click setup for the three player classes: slices the Tiny Swords sheets,
/// builds Idle/Run/Attack clips, and wires a shared controller plus one
/// Animator Override Controller per class.
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
        public string name;       // clip suffix: Idle / Run / Attack
        public string sheet;      // path under UnitsRoot
        public bool loop;
        public ClipDef(string n, string s, bool l) { name = n; sheet = s; loop = l; }
    }

    static readonly Dictionary<string, ClipDef[]> Classes = new Dictionary<string, ClipDef[]>
    {
        { "Swordsman", new[] {
            new ClipDef("Idle",   "Warrior/Warrior_Idle.png",    true),
            new ClipDef("Run",    "Warrior/Warrior_Run.png",     true),
            new ClipDef("Attack", "Warrior/Warrior_Attack1.png", false) } },

        { "Wizard", new[] {
            new ClipDef("Idle",   "Monk/Idle.png", true),
            new ClipDef("Run",    "Monk/Run.png",  true),
            new ClipDef("Attack", "Monk/Heal.png", false) } },

        { "Assassin", new[] {
            new ClipDef("Idle",   "Archer/Archer_Idle.png",  true),
            new ClipDef("Run",    "Archer/Archer_Run.png",   true),
            new ClipDef("Attack", "Archer/Archer_Shoot.png", false) } },
    };

    [MenuItem("Tools/Wanderers/Build Character Animations")]
    public static void BuildAll()
    {
        EnsureFolder(OutRoot);

        // 1. Slice every sheet the three classes need.
        var sheets = new HashSet<string>();
        foreach (var kv in Classes)
            foreach (ClipDef c in kv.Value) sheets.Add(UnitsRoot + c.sheet);

        int sliced = 0;
        foreach (string s in sheets) if (SliceSheet(s)) sliced++;

        AssetDatabase.Refresh();

        // 2. Build clips per class.
        var built = new Dictionary<string, Dictionary<string, AnimationClip>>();
        foreach (var kv in Classes)
        {
            EnsureFolder(OutRoot + kv.Key);
            var clips = new Dictionary<string, AnimationClip>();

            foreach (ClipDef def in kv.Value)
            {
                AnimationClip clip = BuildClip(UnitsRoot + def.sheet,
                                               OutRoot + kv.Key + "/" + kv.Key + "_" + def.name + ".anim",
                                               def.loop);
                if (clip != null) clips[def.name] = clip;
            }
            built[kv.Key] = clips;
        }

        // 3. Shared controller (Swordsman clips act as the defaults), then overrides.
        AnimatorController baseController = BuildBaseController(built["Swordsman"]);
        foreach (var kv in built) BuildOverride(baseController, kv.Key, kv.Value);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[Wanderers] Sliced {0} sheets, built {1} clips, 1 base controller and {2} override controllers in {3}",
            sliced, built.Count * 3, built.Count, OutRoot));
    }

    static bool SliceSheet(string path)
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

    static AnimatorController BuildBaseController(Dictionary<string, AnimationClip> clips)
    {
        string path = OutRoot + "PlayerBase.controller";
        AssetDatabase.DeleteAsset(path);

        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Keep Horizontal/Vertical: Movement.cs already drives them.
        c.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        c.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle");
        AnimatorState run = sm.AddState("Run");
        AnimatorState attack = sm.AddState("Attack");

        idle.motion = Get(clips, "Idle");
        run.motion = Get(clips, "Run");
        attack.motion = Get(clips, "Attack");

        sm.defaultState = idle;

        // Mecanim conditions are AND-only, so moving on either axis needs its own transition.
        AddMoveTransition(idle, run, "Horizontal");
        AddMoveTransition(idle, run, "Vertical");

        AnimatorStateTransition stop = run.AddTransition(idle);
        stop.hasExitTime = false;
        stop.duration = 0f;
        stop.AddCondition(AnimatorConditionMode.Less, 0.01f, "Horizontal");
        stop.AddCondition(AnimatorConditionMode.Less, 0.01f, "Vertical");

        AnimatorStateTransition toAttack = sm.AddAnyStateTransition(attack);
        toAttack.hasExitTime = false;
        toAttack.duration = 0f;
        toAttack.canTransitionToSelf = false;
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        AnimatorStateTransition done = attack.AddTransition(idle);
        done.hasExitTime = true;
        done.exitTime = 1f;
        done.duration = 0f;

        return c;
    }

    static void AddMoveTransition(AnimatorState from, AnimatorState to, string parameter)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.01f, parameter);
    }

    static void BuildOverride(AnimatorController baseController, string className,
                              Dictionary<string, AnimationClip> clips)
    {
        var ovr = new AnimatorOverrideController(baseController);
        ovr.name = className;

        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ovr.GetOverrides(pairs);

        for (int i = 0; i < pairs.Count; i++)
        {
            string key = pairs[i].Key != null ? pairs[i].Key.name : "";
            if (key.EndsWith("_Idle")) pairs[i] = Pair(pairs[i].Key, Get(clips, "Idle"));
            else if (key.EndsWith("_Run")) pairs[i] = Pair(pairs[i].Key, Get(clips, "Run"));
            else if (key.EndsWith("_Attack")) pairs[i] = Pair(pairs[i].Key, Get(clips, "Attack"));
        }
        ovr.ApplyOverrides(pairs);

        string path = OutRoot + className + ".overrideController";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(ovr, path);
    }

    static KeyValuePair<AnimationClip, AnimationClip> Pair(AnimationClip a, AnimationClip b)
    {
        return new KeyValuePair<AnimationClip, AnimationClip>(a, b);
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
