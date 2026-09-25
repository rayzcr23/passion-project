using System;
using Unity.Netcode;
using UnityEngine;

/// A villager you can walk up to and talk to.
///
/// An NPC can hold a whole questline. Conversations are listed in order and the
/// NPC uses the latest one whose previous conversation's quest is complete, so
/// the old man greets you differently once you have culled the flock. Within a
/// conversation, DialogueData still picks lines by that quest's own state.
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

    [Tooltip("Conversations in order. The NPC advances to the next once the " +
         "previous one's quest is complete. Leave Conversations empty to use " +
         "the single Dialogue below instead.")]
    public DialogueData[] conversations;

    [Tooltip("Used when Conversations is empty.")]
    public DialogueData dialogue;

    [Header("Interaction")]
    public float range = 1.6f;
    public KeyCode interactKey = KeyCode.F;
    public Vector3 promptOffset = new Vector3(0f, 1.1f, 0f);

    /// Raised on the client that finished a conversation, so QuestTrigger can
    /// stage a beat on a specific line landing rather than on a kill count.
    public static event Action<DialogueData> ConversationFinished;

    bool playerInRange;
    DialogueData active;

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
        active = CurrentConversation();
        if (active == null) return;

        DialogueUI.Instance.Open(active.speakerName, ChooseLines(active), OnConversationEnded);
    }

    /// Walks the list in order and stops at the first chapter whose gate is not
    /// yet open, so a questline plays out in sequence rather than all at once.
    DialogueData CurrentConversation()
    {
        DialogueData[] list = (conversations != null && conversations.Length > 0)
            ? conversations
            : new[] { dialogue };

        QuestManager qm = QuestManager.Instance;
        DialogueData chosen = null;

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null) continue;

            if (i > 0)
            {
                // The gate is the previous chapter's quest. No quest there means
                // it was pure conversation, so the chapter opens immediately.
                QuestDefinition gate = list[i - 1] != null ? list[i - 1].startsQuest : null;

                if (gate != null && (qm == null || !qm.IsComplete(gate.questId))) break;
            }
            chosen = list[i];
        }
        return chosen;
    }

    string[] ChooseLines(DialogueData data)
    {
        QuestManager qm = QuestManager.Instance;
        QuestDefinition quest = data.startsQuest;

        if (qm != null && quest != null)
        {
            if (qm.IsComplete(quest.questId) && data.completedLines.Length > 0)
                return data.completedLines;

            if (qm.IsActive(quest.questId) && data.inProgressLines.Length > 0)
                return data.inProgressLines;
        }
        return data.lines;
    }

    void OnConversationEnded()
    {
        if (active != null && ConversationFinished != null) ConversationFinished(active);

        QuestManager qm = QuestManager.Instance;
        if (qm == null || active == null) return;

        // Talking is itself an objective for some quests.
        qm.ReportTalkRpc(npcId);

        if (active.startsQuest != null && qm.StateOf(active.startsQuest.questId) == 0)
            qm.RequestStartRpc(active.startsQuest.questId);
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
