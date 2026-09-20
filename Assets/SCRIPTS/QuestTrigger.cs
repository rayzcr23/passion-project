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
    [Tooltip("Must match the questId on the QuestDefinition exactly.")]
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

    bool fired;
    bool subscribed;

    void Start()
    {
        // Objects start hidden so the villagers are not standing around before
        // they have a reason to be.
        foreach (GameObject go in activate)
            if (go != null) go.SetActive(false);
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
    }

    void OnDestroy()
    {
        if (subscribed && QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= Evaluate;
    }

    void Evaluate()
    {
        if (fired || string.IsNullOrEmpty(questId)) return;

        QuestManager qm = QuestManager.Instance;
        if (qm == null) return;

        bool met = when == When.Completed
            ? qm.IsComplete(questId)
            : qm.StateOf(questId) != 0;

        if (!met) return;

        fired = true;
        Fire();
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
