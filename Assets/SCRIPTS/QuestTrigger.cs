using UnityEngine;

/// Fires a world event when a quest reaches a state: reveal the villagers, make
/// the spy slip away, open a scene.
///
/// Nothing here is networked. Quest state already is, so every client reaches
/// the same conclusion on the same frame and flips the same objects. Spawning
/// the villagers over the network instead would buy nothing and cost a
/// NetworkObject each.
public class QuestTrigger : MonoBehaviour
{
    public enum When
    {
        Started,     // the quest has been accepted
        Completed    // its objective is done
    }

    [Header("Condition")]
    [Tooltip("Fires once this conversation has been read to the end. Takes " +
         "priority over the quest below - a beat that should land on a line " +
         "should wait for the line, not for a kill count reached somewhere else.")]
    public DialogueData afterDialogue;

    [Tooltip("Used when After Dialogue is empty. Must match the questId exactly.")]
    public string questId = "";
    public When when = When.Completed;

    [Header("World changes")]
    [Tooltip("Switched on when the condition is met - the villagers coming out.")]
    public GameObject[] activate;

    [Tooltip("Switched off - the spy slipping away before he is questioned.")]
    public GameObject[] deactivate;

    [Header("Scene")]
    [Tooltip("Opened on every client the moment this fires, for a confrontation " +
         "the player should not be able to walk away from. Leave empty for none.")]
    public DialogueData announcement;

    public string announcementSpeaker = "";

    [Header("Staging")]
    [Tooltip("Wait until a player is close before firing. Without this the beat " +
         "plays the instant the quest completes - which, with sheep scattered " +
         "across the map, means villagers accusing you in an empty field.")]
    public bool requirePlayerNearby = true;

    public float nearbyRange = 9f;

    bool conditionMet;
    bool fired;
    bool subscribed;

    void OnEnable()
    {
        DialogueNPC.ConversationFinished += HandleConversationFinished;
    }

    void OnDisable()
    {
        DialogueNPC.ConversationFinished -= HandleConversationFinished;
    }

    void HandleConversationFinished(DialogueData data)
    {
        if (fired || afterDialogue == null || data != afterDialogue) return;

        conditionMet = true;
        Debug.Log("[Wanderers] " + name + " armed by conversation '" + data.name + "'", this);
    }

    void Start()
    {
        // Objects start hidden so the villagers are not standing around before
        // they have a reason to be.
        foreach (GameObject go in activate)
            if (go != null) go.SetActive(false);

        string watching = afterDialogue != null
            ? "conversation '" + afterDialogue.name + "'"
            : "quest '" + questId + "' for " + when;

        Debug.Log("[Wanderers] " + name + " is watching " + watching +
                  " (" + activate.Length + " to reveal, " + deactivate.Length + " to hide)", this);
    }

    void Update()
    {
        if (fired) return;

        QuestManager qm = QuestManager.Instance;
        if (qm == null) return;

        if (!subscribed)
        {
            qm.OnQuestsChanged += Evaluate;
            subscribed = true;
        }

        Evaluate();

        // Armed but waiting for someone to walk back into the village.
        if (conditionMet && PlayerIsNear())
        {
            fired = true;
            Debug.Log("[Wanderers] " + name + " fired on '" + questId + "'", this);
            Fire();
        }
    }

    bool PlayerIsNear()
    {
        if (!requirePlayerNearby) return true;

        Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || nm.SpawnManager == null) return false;

        Unity.Netcode.NetworkObject player = nm.SpawnManager.GetLocalPlayerObject();
        if (player == null) return false;

        return (player.transform.position - transform.position).sqrMagnitude
               <= nearbyRange * nearbyRange;
    }

    void OnDestroy()
    {
        if (subscribed && QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= Evaluate;
    }

    void Evaluate()
    {
        // A conversation-gated trigger ignores quest state entirely.
        if (fired || afterDialogue != null || string.IsNullOrEmpty(questId)) return;

        QuestManager qm = QuestManager.Instance;
        if (qm == null) return;

        bool met = when == When.Completed
            ? qm.IsComplete(questId)
            : qm.StateOf(questId) != 0;

        if (!met) return;

        // Arm only. Update decides when the player is actually here to see it.
        if (!conditionMet)
        {
            conditionMet = true;
            Debug.Log("[Wanderers] " + name + " armed on '" + questId + "'" +
                      (requirePlayerNearby ? " - waiting for a player within " + nearbyRange : ""),
                      this);
        }
    }

    void Fire()
    {
        foreach (GameObject go in activate)
            if (go != null) go.SetActive(true);

        foreach (GameObject go in deactivate)
            if (go != null) go.SetActive(false);

        if (announcement == null || DialogueUI.Instance == null) return;

        // Do not stomp a conversation already on screen.
        if (DialogueUI.Instance.IsOpen) return;

        string speaker = string.IsNullOrEmpty(announcementSpeaker)
            ? announcement.speakerName
            : announcementSpeaker;

        DialogueUI.Instance.Open(speaker, announcement.lines, null);
    }
}
