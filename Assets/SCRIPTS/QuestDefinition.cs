using UnityEngine;

/// A quest, authored as an asset rather than in code.
/// Create via Assets > Create > Wanderers > Quest.
[CreateAssetMenu(fileName = "Quest", menuName = "Wanderers/Quest")]
public class QuestDefinition : ScriptableObject
{
    public enum Objective
    {
        TalkTo,     // reach the NPC whose npcId matches targetId
        Kill        // kill requiredCount creatures whose creatureId matches targetId
    }

    [Tooltip("Stable id used on the wire. Renaming this breaks saves and any NPC " +
         "pointing at it, so treat it as permanent once used.")]
    public string questId = "find_the_blacksmith";

    public string title = "Find the Blacksmith";

    [TextArea(2, 4)]
    public string description = "";

    [Header("Objective")]
    public Objective objective = Objective.Kill;
    public string targetId = "goblin";
    public int requiredCount = 3;

    [Header("On completion")]
    [TextArea(2, 4)]
    public string completionText = "Well done.";

    [Tooltip("Optional: the quest this one unlocks.")]
    public QuestDefinition nextQuest;

    public string ProgressText(int counter)
    {
        if (objective == Objective.Kill)
            return string.Format("{0} ({1}/{2})", title, Mathf.Min(counter, requiredCount), requiredCount);

        return title;
    }
}
