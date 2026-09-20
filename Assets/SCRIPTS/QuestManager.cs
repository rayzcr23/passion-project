using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// Party-wide quest state, owned by the server.
///
/// Quests are shared rather than per-player: in a three-brother co-op, a goblin
/// one of you kills has to count for everyone, or you end up grinding the same
/// objective three times. Dialogue stays local (see DialogueNPC) - only the
/// progress is networked.
public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Tooltip("Every quest that can be started. Looked up by questId.")]
    public QuestDefinition[] quests;

    public struct Progress : INetworkSerializable, IEquatable<Progress>
    {
        public FixedString64Bytes id;
        public int state;       // 0 unstarted, 1 active, 2 complete
        public int counter;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref id);
            s.SerializeValue(ref state);
            s.SerializeValue(ref counter);
        }

        public bool Equals(Progress other) =>
            id.Equals(other.id) && state == other.state && counter == other.counter;
    }

    public NetworkList<Progress> Entries { get; private set; }

    /// Fired on every client when anything changes, so UI can refresh.
    public event Action OnQuestsChanged;

    void Awake()
    {
        Instance = this;
        Entries = new NetworkList<Progress>();
    }

    public override void OnNetworkSpawn()
    {
        Entries.OnListChanged += _ => OnQuestsChanged?.Invoke();
        OnQuestsChanged?.Invoke();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public QuestDefinition Find(string questId)
    {
        if (quests == null) return null;
        foreach (QuestDefinition q in quests)
            if (q != null && q.questId == questId) return q;
        return null;
    }

    public int StateOf(string questId)
    {
        for (int i = 0; i < Entries.Count; i++)
            if (Entries[i].id.ToString() == questId) return Entries[i].state;
        return 0;
    }

    public int CounterOf(string questId)
    {
        for (int i = 0; i < Entries.Count; i++)
            if (Entries[i].id.ToString() == questId) return Entries[i].counter;
        return 0;
    }

    public bool IsActive(string questId) => StateOf(questId) == 1;
    public bool IsComplete(string questId) => StateOf(questId) == 2;

    // ------------------------------------------------------------- server API

    public void ServerStart(string questId)
    {
        if (!IsServer || string.IsNullOrEmpty(questId)) return;
        if (StateOf(questId) != 0) return;          // already running or done

        Entries.Add(new Progress { id = questId, state = 1, counter = 0 });
    }

    /// Called when anything with a creatureId dies. Advances every active Kill
    /// objective that is watching for it.
    public void ServerReportKill(string creatureId)
    {
        if (!IsServer || string.IsNullOrEmpty(creatureId)) return;

        for (int i = 0; i < Entries.Count; i++)
        {
            Progress p = Entries[i];
            if (p.state != 1) continue;

            QuestDefinition def = Find(p.id.ToString());
            if (def == null) continue;
            if (def.objective != QuestDefinition.Objective.Kill) continue;
            if (def.targetId != creatureId) continue;

            p.counter++;
            if (p.counter >= def.requiredCount) p.state = 2;

            Entries[i] = p;
        }
    }

    public void ServerCompleteTalkTo(string npcId)
    {
        if (!IsServer || string.IsNullOrEmpty(npcId)) return;

        for (int i = 0; i < Entries.Count; i++)
        {
            Progress p = Entries[i];
            if (p.state != 1) continue;

            QuestDefinition def = Find(p.id.ToString());
            if (def == null) continue;
            if (def.objective != QuestDefinition.Objective.TalkTo) continue;
            if (def.targetId != npcId) continue;

            p.state = 2;
            Entries[i] = p;
        }
    }

    // --------------------------------------------------- called from anywhere

    [Rpc(SendTo.Server)]
    public void RequestStartRpc(FixedString64Bytes questId)
    {
        ServerStart(questId.ToString());
    }

    [Rpc(SendTo.Server)]
    public void ReportTalkRpc(FixedString64Bytes npcId)
    {
        ServerCompleteTalkTo(npcId.ToString());
    }

    /// Active quests, for the log display.
    public List<string> ActiveSummaries()
    {
        var lines = new List<string>();

        for (int i = 0; i < Entries.Count; i++)
        {
            Progress p = Entries[i];
            if (p.state != 1) continue;

            QuestDefinition def = Find(p.id.ToString());
            lines.Add(def != null ? def.ProgressText(p.counter) : p.id.ToString());
        }
        return lines;
    }
}
