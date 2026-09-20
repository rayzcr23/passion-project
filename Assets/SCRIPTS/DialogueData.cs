using UnityEngine;

/// A conversation, authored as an asset.
/// Create via Assets > Create > Wanderers > Dialogue.
[CreateAssetMenu(fileName = "Dialogue", menuName = "Wanderers/Dialogue")]
public class DialogueData : ScriptableObject
{
    public string speakerName = "Villager";

    [TextArea(2, 5)]
    public string[] lines =
    {
        "The goblins came at dusk.",
        "Drive them off and the blacksmith will speak with you."
    };

    [Header("Quest")]
    [Tooltip("Started when this conversation ends.")]
    public QuestDefinition startsQuest;

    [Header("Alternate lines")]
    [Tooltip("Said instead while startsQuest is still active.")]
    [TextArea(2, 5)]
    public string[] inProgressLines = { "They are still out there." };

    [Tooltip("Said instead once startsQuest is complete.")]
    [TextArea(2, 5)]
    public string[] completedLines = { "You have our thanks." };
}
