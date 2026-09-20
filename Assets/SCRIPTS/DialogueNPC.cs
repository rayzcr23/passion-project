using Unity.Netcode;
using UnityEngine;

/// A villager you can walk up to and talk to.
///
/// The conversation runs entirely on the client who pressed the key - nobody
/// else's screen is interrupted. Only the outcome (a quest starting, a TalkTo
/// objective completing) goes to the server, where quest state lives.
///
/// Needs no NetworkObject: the NPC is scenery until someone talks to it.
public class DialogueNPC : MonoBehaviour
{
    [Tooltip("Stable id for TalkTo objectives.")]
    public string npcId = "villager";

    public DialogueData dialogue;

    [Header("Interaction")]
    public float range = 1.6f;
    public KeyCode interactKey = KeyCode.F;
    public Vector3 promptOffset = new Vector3(0f, 1.1f, 0f);

    bool playerInRange;

    void Update()
    {
        Transform player = LocalPlayer();
        playerInRange = player != null &&
            (player.position - transform.position).sqrMagnitude <= range * range;

        if (!playerInRange) return;
        if (DialogueUI.Instance == null) return;

        // Do not reopen the conversation we are already having.
        if (DialogueUI.Instance.IsOpen) return;

        if (Input.GetKeyDown(interactKey)) Talk();
    }

    void Talk()
    {
        if (dialogue == null) return;

        string[] lines = ChooseLines();
        DialogueUI.Instance.Open(dialogue.speakerName, lines, OnConversationEnded);
    }

    string[] ChooseLines()
    {
        QuestManager qm = QuestManager.Instance;
        QuestDefinition quest = dialogue.startsQuest;

        if (qm != null && quest != null)
        {
            if (qm.IsComplete(quest.questId) && dialogue.completedLines.Length > 0)
                return dialogue.completedLines;

            if (qm.IsActive(quest.questId) && dialogue.inProgressLines.Length > 0)
                return dialogue.inProgressLines;
        }
        return dialogue.lines;
    }

    void OnConversationEnded()
    {
        QuestManager qm = QuestManager.Instance;
        if (qm == null) return;

        // Talking is itself an objective for some quests.
        qm.ReportTalkRpc(npcId);

        if (dialogue.startsQuest != null && qm.StateOf(dialogue.startsQuest.questId) == 0)
            qm.RequestStartRpc(dialogue.startsQuest.questId);
    }

    static Transform LocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || nm.SpawnManager == null) return null;

        NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
        return player != null ? player.transform : null;
    }

    void OnGUI()
    {
        if (!playerInRange) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;
        if (Camera.main == null) return;

        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + promptOffset);
        if (screen.z < 0f) return;

        var rect = new Rect(screen.x - 70f, Screen.height - screen.y - 14f, 140f, 26f);
        GUI.Label(rect, "[" + interactKey + "] Talk", DialogueUI.CentredLabel);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
