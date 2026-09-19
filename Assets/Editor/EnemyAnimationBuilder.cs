using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Builds the goblin enemy from the Tiny Swords Torch sheet.
///
/// That sheet is one 7x5 grid holding several animations, so clips are cut from
/// frame ranges rather than whole files:
///   row 0, cols 0-6  Idle
///   row 1, cols 0-5  Run
///   row 2, cols 0-5  Attack (side)
/// Rows 3 and 4 are down/up attack variants, unused - the goblin flips to face
/// its target like the players do.
public static class EnemyAnimationBuilder
{
    const string Sheet = "Assets/Tiny Swords/Tiny Swords (Update 010)/Factions/Goblins/Troops/Torch/Red/Torch_Red.png";
    const string OutRoot = "Assets/ANIMATIONS/Goblin/";
    const string ControllerPath = "Assets/ANIMATIONS/Goblin.controller";
    const int Columns = 7;
    const float FrameRate = 12f;

    class RangeDef
    {
        public string name;
        public int first;      // absolute frame index
        public int count;
        public bool loop;
        public RangeDef(string n, int row, int col, int c, bool l)
        {
            name = n; first = row * Columns + col; count = c; loop = l;
        }
    }

    static readonly RangeDef[] Ranges =
    {
        new RangeDef("Idle",   0, 0, 7, true),
        new RangeDef("Run",    1, 0, 6, true),
        new RangeDef("Attack", 2, 0, 6, false),
    };

    [MenuItem("Tools/Wanderers/Build Enemy Animations")]
    public static void BuildAll()
    {
        EnsureFolder(OutRoot);

        if (!CharacterAnimationBuilder.SliceSheet(Sheet))
        {
            Debug.LogError("[Wanderers] Could not slice " + Sheet);
            return;
        }
        AssetDatabase.Refresh();

        List<Sprite> frames = SortedSprites(Sheet);
        if (frames.Count == 0) { Debug.LogError("[Wanderers] No sprites after slicing"); return; }

        var clips = new Dictionary<string, AnimationClip>();
        foreach (RangeDef r in Ranges)
        {
            AnimationClip clip = BuildClip(frames, r, OutRoot + "Goblin_" + r.name + ".anim");
            if (clip != null) clips[r.name] = clip;
        }

        BuildController(clips);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Wanderers] Built " + clips.Count + " goblin clips and Goblin.controller");
    }

    static List<Sprite> SortedSprites(string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = new List<Sprite>();
        foreach (Object o in all) { var s = o as Sprite; if (s != null) sprites.Add(s); }

        sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
        return sprites;
    }

    static AnimationClip BuildClip(List<Sprite> frames, RangeDef range, string clipPath)
    {
        if (range.first + range.count > frames.Count)
        {
            Debug.LogWarning("[Wanderers] " + range.name + " runs past the end of the sheet");
            return null;
        }

        var clip = new AnimationClip { frameRate = FrameRate };

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[range.count];
        for (int i = 0; i < range.count; i++)
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / FrameRate,
                value = frames[range.first + i]
            };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = range.loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    static void BuildController(Dictionary<string, AnimationClip> clips)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // One Speed float rather than the players' Horizontal/Vertical: EnemyBrain
        // knows its own velocity, so a single value is simpler.
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle");
        AnimatorState run = sm.AddState("Run");
        AnimatorState attack = sm.AddState("Attack");

        AnimationClip clip;
        if (clips.TryGetValue("Idle", out clip)) idle.motion = clip;
        if (clips.TryGetValue("Run", out clip)) run.motion = clip;
        if (clips.TryGetValue("Attack", out clip)) attack.motion = clip;

        sm.defaultState = idle;

        AnimatorStateTransition go = idle.AddTransition(run);
        go.hasExitTime = false; go.duration = 0f;
        go.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

        AnimatorStateTransition stop = run.AddTransition(idle);
        stop.hasExitTime = false; stop.duration = 0f;
        stop.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

        AnimatorStateTransition swing = sm.AddAnyStateTransition(attack);
        swing.hasExitTime = false; swing.duration = 0f;
        swing.canTransitionToSelf = false;
        swing.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        AnimatorStateTransition done = attack.AddTransition(idle);
        done.hasExitTime = true; done.exitTime = 1f; done.duration = 0f;
    }

    static int IndexOf(string spriteName)
    {
        int u = spriteName.LastIndexOf('_');
        int n;
        return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out n)) ? n : 0;
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
