using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds the "the old man was a spy" beat: his reveal, the villagers who come
/// out when the flock is gone, and the trigger that swaps one for the other.
///
/// Run it once. It only creates what is missing, so re-running will not stamp
/// over dialogue you have rewritten.
public static class StoryBuilder
{
    const string StoryFolder = "Assets/STORY";
    const string PawnSheet = "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Blue Units/Pawn/Pawn_Idle.png";

    [MenuItem("Tools/Wanderers/Build Sheep Betrayal")]
    public static void Build()
    {
        QuestDefinition sheepQuest = FindSheepQuest();
        if (sheepQuest == null)
        {
            Debug.LogError("[Wanderers] No Kill quest targeting 'sheep' found. Create it first.");
            return;
        }

        EnsureFolder(StoryFolder);

        DialogueData reveal = EnsureDialogue("OldMan_Reveal", "wise old man", new[]
        {
            "You actually did it.",
            "Ten sheep. The whole flock, gone - because a stranger asked.",
            "My people will be pleased. A village that cannot feed itself is a village that kneels.",
            "Do not look so wounded. You were the knife. I only pointed."
        });

        DialogueData accusation = EnsureDialogue("Villagers_Accusation", "villager", new[]
        {
            "What have you done?!",
            "Those sheep were the whole winter. Every one of them.",
            "Who told you to do this? WHO?",
            "...he is gone. Of course he is gone."
        });

        // The trial. One asset covers all three states: the offer, the nagging
        // while you are out there, and the forgiveness when you come back.
        QuestDefinition trial = EnsureQuest(
            "prove_innocence", "Prove Your Innocence", "goblin", 25);

        DialogueData villagerIdle = EnsureDialogue("Villager_Trial", "villager", new[]
        {
            "You say the old man used you. Perhaps.",
            "Words are cheap when the pens are empty.",
            "The goblins have been circling us for a season. Kill twenty-five of them.",
            "Do that, and we will believe you were a fool and not a traitor."
        });

        if (villagerIdle.startsQuest == null)
        {
            villagerIdle.startsQuest = trial;

            villagerIdle.inProgressLines = new[]
            {
                "Twenty-five. We are counting.",
                "The forest is thick with them. Go."
            };

            villagerIdle.completedLines = new[]
            {
                "...you actually came back.",
                "The pens are still empty. But the treeline is quiet for the first time in months.",
                "You were a fool. Not a traitor. That is something.",
                "Go north, to the blacksmith. Tell him what the old man told you."
            };

            EditorUtility.SetDirty(villagerIdle);
        }

        GameObject oldMan = FindOldMan(sheepQuest);
        if (oldMan == null)
        {
            Debug.LogError("[Wanderers] No DialogueNPC in the scene to use as the old man.");
            return;
        }

        WireOldMan(oldMan, reveal);

        List<GameObject> villagers = EnsureVillagers(oldMan.transform.position, villagerIdle);

        string triggerQuest = FinalQuestId(oldMan, reveal) ?? sheepQuest.questId;
        EnsureTrigger(triggerQuest, villagers, oldMan, accusation, reveal);

        EnsureRewardSheep(oldMan.transform.position, trial.questId);

        ProjectSetupBuilder.RegisterAllQuests();

        // Saving, not just dirtying. Leaving it unsaved means the objects exist in
        // memory only: the scene file has no RewardSheep, the trigger has no
        // afterDialogue, and a build ships none of it.
        EditorSceneManager.MarkSceneDirty(oldMan.scene);
        EditorSceneManager.SaveScene(oldMan.scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Wanderers] Sheep betrayal built in scene '" + oldMan.scene.name +
                  "'. Villagers appear when '" + triggerQuest + "' completes.");
    }

    static QuestDefinition FindSheepQuest()
    {
        QuestDefinition fallback = null;

        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var q = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (q == null) continue;

            if (fallback == null) fallback = q;
            if (q.objective == QuestDefinition.Objective.Kill && q.targetId == "sheep") return q;
        }
        return fallback;
    }

    /// The old man is the NPC who hands out the sheep quest.
    ///
    /// This used to be "any NPC with a conversation", which stopped working the
    /// moment the villagers got the trial dialogue: it picked a villager,
    /// appended the reveal to their chapters, and pointed the trigger's
    /// deactivate at them - so the trigger switched a villager on and straight
    /// back off while the old man stood there.
    static GameObject FindOldMan(QuestDefinition sheepQuest)
    {
        DialogueNPC fallback = null;

        foreach (DialogueNPC npc in Object.FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None))
        {
            if (npc.name.StartsWith("Villager")) continue;

            foreach (DialogueData d in Chapters(npc))
                if (d != null && d.startsQuest == sheepQuest) return npc.gameObject;

            if (fallback == null) fallback = npc;
        }

        return fallback != null ? fallback.gameObject : null;
    }

    static IEnumerable<DialogueData> Chapters(DialogueNPC npc)
    {
        if (npc.conversations != null)
            foreach (DialogueData d in npc.conversations) yield return d;

        yield return npc.dialogue;
    }

    /// Appends the reveal as the final chapter, keeping whatever chapters are
    /// already there. An earlier version replaced chapter 1, which quietly threw
    /// away the conversation that hands out the second quest.
    static void WireOldMan(GameObject oldMan, DialogueData reveal)
    {
        var npc = oldMan.GetComponent<DialogueNPC>();
        if (npc == null) return;

        var chapters = new List<DialogueData>();

        if (npc.conversations != null && npc.conversations.Length > 0)
        {
            foreach (DialogueData d in npc.conversations)
                if (d != null && d != reveal) chapters.Add(d);
        }
        else if (npc.dialogue != null)
        {
            chapters.Add(npc.dialogue);
        }

        if (chapters.Count == 0)
        {
            Debug.LogWarning("[Wanderers] Old man has no conversation to build on");
            return;
        }

        chapters.Add(reveal);           // the act drops last
        npc.conversations = chapters.ToArray();
        EditorUtility.SetDirty(npc);

        Debug.Log("[Wanderers] Old man now has " + chapters.Count + " chapters, reveal last");
    }

    /// The villagers should come out after the LAST sheep job, not the first, so
    /// the trigger follows the final chapter that actually hands out a quest.
    static string FinalQuestId(GameObject oldMan, DialogueData reveal)
    {
        var npc = oldMan.GetComponent<DialogueNPC>();
        if (npc == null || npc.conversations == null) return null;

        string last = null;
        foreach (DialogueData d in npc.conversations)
        {
            if (d == null || d == reveal) continue;
            if (d.startsQuest != null) last = d.startsQuest.questId;
        }
        return last;
    }

    static List<GameObject> EnsureVillagers(Vector3 near, DialogueData lines)
    {
        var made = new List<GameObject>();
        Sprite sprite = FirstSprite(PawnSheet);

        var offsets = new[]
        {
            new Vector3(-1.4f, -0.6f, 0f),
            new Vector3(0f, -1.1f, 0f),
            new Vector3(1.4f, -0.6f, 0f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            string name = "Villager " + (i + 1);
            GameObject existing = GameObject.Find(name);

            GameObject go = existing != null ? existing : new GameObject(name);
            go.transform.position = near + offsets[i];

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (sr.sprite == null) sr.sprite = sprite;
            sr.sortingOrder = 1;

            var npc = go.GetComponent<DialogueNPC>();
            if (npc == null) npc = go.AddComponent<DialogueNPC>();
            npc.npcId = "villager_" + (i + 1);

            // Villagers built before the trial existed carry Villager_Angry, which
            // starts no quest. Upgrade those, but leave anything hand-authored.
            bool placeholder = npc.dialogue == null ||
                               npc.dialogue.name == "Villager_Angry";

            if (placeholder && (npc.conversations == null || npc.conversations.Length == 0))
                npc.dialogue = lines;

            // A villager must never carry the old man's reveal. An earlier run
            // could append it here by mistaking a villager for him.
            if (npc.conversations != null && npc.conversations.Length > 0)
                npc.conversations = new DialogueData[0];

            EditorUtility.SetDirty(go);
            made.Add(go);
        }
        return made;
    }

    static void EnsureTrigger(string questId, List<GameObject> villagers,
                              GameObject oldMan, DialogueData announcement,
                              DialogueData afterDialogue)
    {
        GameObject go = GameObject.Find("SheepBetrayalTrigger");
        if (go == null) go = new GameObject("SheepBetrayalTrigger");

        var trigger = go.GetComponent<QuestTrigger>();
        if (trigger == null) trigger = go.AddComponent<QuestTrigger>();

        // Without a position it sits at world origin, and "is a player nearby"
        // then measures distance from (0,0,0) rather than from the village.
        go.transform.position = oldMan.transform.position;

        trigger.questId = questId;
        trigger.when = QuestTrigger.When.Completed;
        trigger.activate = villagers.ToArray();
        trigger.deactivate = new[] { oldMan };     // the spy slips away
        trigger.announcement = announcement;

        // The beat belongs on the old man admitting it, not on the fifth sheep.
        trigger.afterDialogue = afterDialogue;

        EditorUtility.SetDirty(trigger);
    }

    /// The sheep the villagers hand over once you have proved yourself. It waits
    /// hidden until the trial is done, then stands in the village to be claimed.
    static void EnsureRewardSheep(Vector3 near, string trialQuestId)
    {
        GameObject sheep = GameObject.Find("RewardSheep");
        if (sheep == null) sheep = new GameObject("RewardSheep");

        sheep.transform.position = near + new Vector3(2.6f, -1.6f, 0f);

        // The sheep sheet is 128px against the characters' 192, so at 1:1 it reads
        // as a lamb standing next to a knight.
        sheep.transform.localScale = Vector3.one * 1.8f;

        var sr = sheep.GetComponent<SpriteRenderer>();
        if (sr == null) sr = sheep.AddComponent<SpriteRenderer>();

        // Assigned unconditionally. This was created while the sheep sheet was
        // mis-sliced, so FirstSprite returned null and the renderer was saved
        // empty - and an "only if null" check then left it empty on every rerun.
        Sprite sheepSprite = FirstSprite(
            "Assets/Tiny Swords/Tiny Swords (Update 010)/Resources/Sheep/HappySheep_Idle.png");

        if (sheepSprite != null) sr.sprite = sheepSprite;
        else if (sr.sprite == null)
            Debug.LogError("[Wanderers] RewardSheep has no sprite - run Build Sheep first " +
                           "so the sheep sheet is sliced, then run this again.", sheep);

        sr.sortingOrder = 1;

        // It is claimed over the network, so it needs an identity of its own.
        if (sheep.GetComponent<Unity.Netcode.NetworkObject>() == null)
            sheep.AddComponent<Unity.Netcode.NetworkObject>();

        var rideable = sheep.GetComponent<RideableMount>();
        if (rideable == null) rideable = sheep.AddComponent<RideableMount>();
        rideable.requiresQuestId = trialQuestId;

        EditorUtility.SetDirty(sheep);

        // No QuestTrigger here on purpose: it hides objects with SetActive, and NGO
        // will not spawn an in-scene NetworkObject that starts inactive, so the
        // claim RPC would never reach the server. The sheep gates itself instead.
        Debug.Log("[Wanderers] Reward sheep appears when '" + trialQuestId + "' completes");
    }

    static QuestDefinition EnsureQuest(string id, string title, string target, int count)
    {
        string path = StoryFolder + "/" + id + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (existing != null) return existing;      // never restamp a tuned quest

        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        quest.questId = id;
        quest.title = title;
        quest.objective = QuestDefinition.Objective.Kill;
        quest.targetId = target;
        quest.requiredCount = count;
        quest.description = "";

        AssetDatabase.CreateAsset(quest, path);
        return quest;
    }

    static DialogueData EnsureDialogue(string assetName, string speaker, string[] lines)
    {
        string path = StoryFolder + "/" + assetName + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (existing != null) return existing;      // never overwrite edited text

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.speakerName = speaker;
        data.lines = lines;
        data.inProgressLines = new string[0];
        data.completedLines = new string[0];

        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    /// Loads only - never slices.
    ///
    /// This used to call SliceSheet, which defaults to 192px cells. The sheep
    /// sheets are 128, so every run re-sliced them at the wrong size, reminted
    /// every sub-sprite id and turned all the placed sheep invisible. A story
    /// script has no business reshaping someone else's texture import settings.
    static Sprite FirstSprite(string sheetPath)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(sheetPath);

        Sprite best = null;
        int bestIndex = int.MaxValue;

        foreach (Object o in all)
        {
            var sprite = o as Sprite;
            if (sprite == null) continue;

            int underscore = sprite.name.LastIndexOf('_');
            int index;
            if (!int.TryParse(underscore >= 0 ? sprite.name.Substring(underscore + 1) : "0", out index))
                index = 0;

            if (index < bestIndex) { bestIndex = index; best = sprite; }
        }

        if (best != null) return best;

        Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(sheetPath);
        if (single == null)
            Debug.LogWarning("[Wanderers] No sprite in " + sheetPath +
                             " - slice it first (Build Sheep / Build Character Animations).");

        return single;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
