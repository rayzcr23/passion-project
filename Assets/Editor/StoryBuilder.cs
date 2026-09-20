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

        DialogueData villagerIdle = EnsureDialogue("Villager_Angry", "villager", new[]
        {
            "Do not speak to me.",
            "Find the old man. Find him and bring him back here."
        });

        GameObject oldMan = FindOldMan();
        if (oldMan == null)
        {
            Debug.LogError("[Wanderers] No DialogueNPC in the scene to use as the old man.");
            return;
        }

        WireOldMan(oldMan, reveal);

        List<GameObject> villagers = EnsureVillagers(oldMan.transform.position, villagerIdle);
        EnsureTrigger(sheepQuest.questId, villagers, oldMan, accusation);

        EditorSceneManager.MarkSceneDirty(oldMan.scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Wanderers] Sheep betrayal built. Kill " + sheepQuest.requiredCount +
                  " sheep and the old man vanishes.");
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

    static GameObject FindOldMan()
    {
        // The NPC already carrying a conversation is the old man.
        foreach (DialogueNPC npc in Object.FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None))
        {
            bool hasDialogue = npc.dialogue != null ||
                (npc.conversations != null && npc.conversations.Length > 0);

            if (hasDialogue) return npc.gameObject;
        }
        return null;
    }

    static void WireOldMan(GameObject oldMan, DialogueData reveal)
    {
        var npc = oldMan.GetComponent<DialogueNPC>();
        if (npc == null) return;

        DialogueData first = npc.conversations != null && npc.conversations.Length > 0
            ? npc.conversations[0]
            : npc.dialogue;

        if (first == null) { Debug.LogWarning("[Wanderers] Old man has no first conversation"); return; }

        // Chapter 0 gives the quest, chapter 1 opens once it is complete.
        npc.conversations = new[] { first, reveal };
        EditorUtility.SetDirty(npc);
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
            if (npc.dialogue == null) npc.dialogue = lines;

            EditorUtility.SetDirty(go);
            made.Add(go);
        }
        return made;
    }

    static void EnsureTrigger(string questId, List<GameObject> villagers,
                              GameObject oldMan, DialogueData announcement)
    {
        GameObject go = GameObject.Find("SheepBetrayalTrigger");
        if (go == null) go = new GameObject("SheepBetrayalTrigger");

        var trigger = go.GetComponent<QuestTrigger>();
        if (trigger == null) trigger = go.AddComponent<QuestTrigger>();

        trigger.questId = questId;
        trigger.when = QuestTrigger.When.Completed;
        trigger.activate = villagers.ToArray();
        trigger.deactivate = new[] { oldMan };     // the spy slips away
        trigger.announcement = announcement;

        EditorUtility.SetDirty(trigger);
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

    static Sprite FirstSprite(string sheetPath)
    {
        CharacterAnimationBuilder.SliceSheet(sheetPath);
        AssetDatabase.Refresh();

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        foreach (Object o in all) { var s = o as Sprite; if (s != null) return s; }

        return AssetDatabase.LoadAssetAtPath<Sprite>(sheetPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
