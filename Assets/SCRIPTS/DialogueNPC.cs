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
    public static event Action<DialogueNPC, DialogueData> ConversationFinishedByNpc;

    bool playerInRange;
    DialogueData active;

    /// Returns true if the local player is currently within talking range of any NPC.
    public static bool IsAnyNpcInRange
    {
        get
        {
            var npcs = FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None);
            foreach (var n in npcs)
            {
                if (n != null && n.playerInRange) return true;
            }
            return false;
        }
    }

    void Start()
    {
        interactKey = KeyCode.F;

        // The fisherman lives on the far side of the map and gets his lines from
        // Resources - but the blacksmith village sits past x = 25 as well, so never
        // let this override touch the blacksmith's own conversation.
        bool isBlacksmith = name.ToLower().Contains("blacksmith") ||
                            (npcId != null && npcId.ToLower().Contains("blacksmith"));

        if (!isBlacksmith && transform.position.x > 25f && (dialogue == null || dialogue.name.Contains("Villager")))
        {
            // Load from Resources/STORY so they're always found even if not scene-referenced.
            var bowGrant = Resources.Load<DialogueData>("STORY/Fisherman_BowGrant");
            var guide    = Resources.Load<DialogueData>("STORY/Fisherman_BlacksmithGuide");

            // Conversation chain:
            //  [0] Fisherman_BowGrant        — first talk: grants bow, starts goblin quest
            //  [1] Fisherman_BlacksmithGuide — unlocks only after the full quest chain
            //                                  (goblins → fish) is complete
            if (bowGrant != null || guide != null)
            {
                npcId = "fisherman";
                dialogue = null; // use conversations[] instead

                var chain = new System.Collections.Generic.List<DialogueData>();
                if (bowGrant != null) chain.Add(bowGrant);
                if (guide    != null) chain.Add(guide);
                conversations = chain.ToArray();
            }
        }
    }

    void Update()
    {
        Transform player = LocalPlayer();
        if (player != null && TimeStopWorld.IsFrozenPlayer(player.GetComponent<AbilityLoadout>()))
        {
            playerInRange = false;
            return;
        }
        playerInRange = player != null &&
            (player.position - transform.position).sqrMagnitude <= range * range;

        if (!playerInRange) return;
        if (DialogueUI.Instance == null) return;

        // Do not reopen the conversation we are already having.
        if (DialogueUI.Instance.IsOpen) return;

        if (Input.GetKeyDown(interactKey) || TouchControls.InteractPressed) Talk();
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
                // Walk the entire nextQuest chain from the previous chapter.
                // All quests in the chain must be complete before this chapter unlocks.
                // e.g. BowGrant starts quest_tnt_goblins whose nextQuest is quest_giant_fish,
                // so BlacksmithGuide only unlocks once BOTH quests are done.
                QuestDefinition gate = list[i - 1] != null ? list[i - 1].startsQuest : null;
                bool chainComplete = true;
                while (gate != null)
                {
                    if (qm == null || !qm.IsComplete(gate.questId)) { chainComplete = false; break; }
                    gate = gate.nextQuest;
                }
                if (!chainComplete) break;
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
        if (active != null && ConversationFinishedByNpc != null) ConversationFinishedByNpc(this, active);

        // Only the lake fisherman drives the village event - the village blacksmith
        // has his own iron questline and must not hand out a bow or start the assault.
        bool isFisherman = (active != null && (active.name.ToLower().Contains("fisher") || (active.speakerName != null && active.speakerName.ToLower().Contains("fisher"))))
                         || (name != null && name.ToLower().Contains("fisher"))
                         || (npcId != null && npcId.ToLower().Contains("fisher"));

        if (isFisherman)
        {
            var villageEvent = BlacksmithVillageEvent.EnsureVillageInstance();
            if (villageEvent != null)
            {
                if (villageEvent.isFishDefeated)
                {
                    // Make sure fisherman #1 is deactivated — even if the instant
                    // swap in OnFishDefeated() was missed (e.g. offline or late join).
                    if (villageEvent.blacksmithNpc != null)
                        villageEvent.blacksmithNpc.gameObject.SetActive(false);
                }
                else
                {
                    villageEvent.GrantBowToLocalPlayer();
                    if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
                    {
                        if (!villageEvent.isEventStarted) villageEvent.ServerStartGoblinAssault();
                    }
                    else
                    {
                        villageEvent.RequestStartAssaultRpc();
                    }
                }
            }
        }

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
        string prompt = TouchControls.TouchModeActive ? "Press TALK" : "[" + interactKey + "] Talk";
        GUI.Label(rect, prompt, DialogueUI.CentredLabel);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
