using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    public static QuestSystem Instance { get; private set; }

    // Fired when any quest state changes — GameUI subscribes to refresh its display.
    public static event Action<QuestData> OnQuestAccepted;
    public static event Action<QuestData> OnQuestUpdated;
    public static event Action<QuestData> OnQuestReadyToTurnIn;
    public static event Action<QuestData> OnQuestTurnedIn;
    public static event Action<QuestData> OnQuestCancelled;

    // CharacterBuffs listens to this for UntilQuestComplete buff removal.
    public static event Action<QuestData> OnQuestCompleted;

    readonly Dictionary<string, QuestRuntimeState> _states = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Accepting quests ──────────────────────────────────────────────────────

    public bool CanOffer(QuestData quest)
    {
        if (quest == null) return false;
        if (_states.TryGetValue(quest.name, out var existing))
            return false; // already accepted or completed

        if (quest.requiredWorldFlags != null)
        {
            foreach (string flag in quest.requiredWorldFlags)
                if (WorldStateSystem.Instance == null || !WorldStateSystem.Instance.GetFlag(flag))
                    return false;
        }
        return true;
    }

    public void AcceptQuest(QuestData quest)
    {
        if (quest == null || _states.ContainsKey(quest.name)) return;

        var state = new QuestRuntimeState(quest);
        _states[quest.name] = state;

        if (quest.setsWorldFlagsOnAccept != null)
            foreach (string flag in quest.setsWorldFlagsOnAccept)
                WorldStateSystem.Instance?.SetFlag(flag, true);

        OnQuestAccepted?.Invoke(quest);
        Debug.Log($"[QuestSystem] Accepted: {quest.title}");
    }

    public void CancelQuest(QuestData quest)
    {
        if (quest == null || !_states.TryGetValue(quest.name, out var state)) return;
        state.status = QuestStatus.Cancelled;
        OnQuestCancelled?.Invoke(quest);
    }

    // ── Objective reporting ───────────────────────────────────────────────────

    public void ReportKill(string npcDefinitionAssetName)
        => UpdateObjectives(QuestObjectiveType.KillNpc, npcDefinitionAssetName, 1);

    public void ReportItemAcquired(string itemAssetName)
        => UpdateObjectives(QuestObjectiveType.DeliverItem, itemAssetName, 1);

    public void ReportTalkToNpc(string npcSaveId)
        => UpdateObjectives(QuestObjectiveType.TalkToNpc, npcSaveId, 1);

    public void ReportZoneReached(string zoneId)
        => UpdateObjectives(QuestObjectiveType.ReachZone, zoneId, 1);

    public void ReportPropInteracted(string propId)
        => UpdateObjectives(QuestObjectiveType.InteractWithProp, propId, 1);

    void UpdateObjectives(QuestObjectiveType type, string targetId, int increment)
    {
        if (string.IsNullOrEmpty(targetId)) return;

        foreach (var state in _states.Values)
        {
            if (state.status != QuestStatus.InProgress) continue;
            if (state.data.objectives == null) continue;

            bool changed = false;
            for (int i = 0; i < state.data.objectives.Count; i++)
            {
                var obj = state.data.objectives[i];
                if (obj.type != type) continue;
                if (obj.targetId != targetId) continue;
                if (state.objectiveCounts[i] >= obj.requiredCount) continue;

                state.objectiveCounts[i] = Mathf.Min(
                    state.objectiveCounts[i] + increment, obj.requiredCount);
                changed = true;
            }

            if (!changed) continue;

            OnQuestUpdated?.Invoke(state.data);
            CheckCompletion(state);
        }
    }

    void CheckCompletion(QuestRuntimeState state)
    {
        if (state.status != QuestStatus.InProgress) return;
        if (state.data.objectives == null || state.data.objectives.Count == 0)
        {
            state.status = QuestStatus.ReadyToTurnIn;
            OnQuestReadyToTurnIn?.Invoke(state.data);
            return;
        }

        for (int i = 0; i < state.data.objectives.Count; i++)
            if (state.objectiveCounts[i] < state.data.objectives[i].requiredCount) return;

        state.status = QuestStatus.ReadyToTurnIn;
        OnQuestReadyToTurnIn?.Invoke(state.data);
        OnQuestCompleted?.Invoke(state.data);
        Debug.Log($"[QuestSystem] Ready to turn in: {state.data.title}");
    }

    // ── Turn-in ───────────────────────────────────────────────────────────────

    // Returns the first quest ready to turn in with this NPC, or null.
    public QuestData GetTurnInQuestFor(string npcSaveId)
    {
        foreach (var state in _states.Values)
        {
            if (state.status != QuestStatus.ReadyToTurnIn) continue;
            if (state.data.turnInNpcSaveId == npcSaveId) return state.data;
        }
        return null;
    }

    public void TurnIn(QuestData quest)
    {
        if (quest == null || !_states.TryGetValue(quest.name, out var state)) return;
        if (state.status != QuestStatus.ReadyToTurnIn) return;

        state.status = QuestStatus.TurnedIn;

        // Deliver rewards
        if (quest.goldReward > 0)
            InventorySystem.Instance?.AddGold(quest.goldReward);
        if (quest.corruptionReward > 0f)
            CorruptionTracker.Instance?.AddCorruption(quest.corruptionReward);
        else if (quest.corruptionReward < 0f)
            CorruptionTracker.Instance?.ReduceCorruption(-quest.corruptionReward);
        if (quest.xpReward > 0)
            XPSystem.Instance?.AddXP(quest.xpReward);
        if (quest.itemRewards != null)
            foreach (var item in quest.itemRewards)
                if (item != null)
                    // Adds to inventory; any overflow drops in a bag at the player's feet.
                    LootContainer.GiveToPlayerOrDrop(item, 1);
        if (quest.spellRewards != null)
            foreach (var spell in quest.spellRewards)
                if (spell != null) SpellbookSystem.Instance?.LearnSpell(spell);

        if (quest.setsWorldFlagsOnComplete != null)
            foreach (string flag in quest.setsWorldFlagsOnComplete)
                WorldStateSystem.Instance?.SetFlag(flag, true);

        OnQuestTurnedIn?.Invoke(quest);
        Debug.Log($"[QuestSystem] Turned in: {quest.title}");
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public QuestStatus GetStatus(QuestData quest)
    {
        if (quest == null) return QuestStatus.NotStarted;
        return _states.TryGetValue(quest.name, out var state) ? state.status : QuestStatus.NotStarted;
    }

    public int GetObjectiveCount(QuestData quest, int objectiveIndex)
    {
        if (!_states.TryGetValue(quest.name, out var state)) return 0;
        if (objectiveIndex < 0 || objectiveIndex >= state.objectiveCounts.Length) return 0;
        return state.objectiveCounts[objectiveIndex];
    }

    public List<QuestRuntimeState> GetActiveQuests()
    {
        var result = new List<QuestRuntimeState>();
        foreach (var state in _states.Values)
            if (state.status == QuestStatus.InProgress || state.status == QuestStatus.ReadyToTurnIn)
                result.Add(state);
        return result;
    }

    public List<QuestRuntimeState> GetCompletedQuests()
    {
        var result = new List<QuestRuntimeState>();
        foreach (var state in _states.Values)
            if (state.status == QuestStatus.TurnedIn || state.status == QuestStatus.Cancelled)
                result.Add(state);
        return result;
    }

    // ── Legacy shim (used by CharacterBuffs to check quest completion) ────────

    public bool IsCompleted(QuestData quest) =>
        quest != null
        && _states.TryGetValue(quest.name, out var state)
        && (state.status == QuestStatus.TurnedIn);

    // ── Save / load ───────────────────────────────────────────────────────────

    public List<QuestRuntimeSave> CaptureState()
    {
        var saved = new List<QuestRuntimeSave>();
        foreach (var kv in _states)
        {
            var record = new QuestRuntimeSave
            {
                questAssetName = kv.Key,
                status         = kv.Value.status,
            };
            record.objectiveCounts.AddRange(kv.Value.objectiveCounts);
            saved.Add(record);
        }
        return saved;
    }

    public void RestoreState(List<QuestRuntimeSave> saved, GameDatabase database)
    {
        _states.Clear();
        if (saved == null) return;

        foreach (var record in saved)
        {
            QuestData quest = database.FindQuest(record.questAssetName);
            if (quest == null)
            {
                Debug.LogWarning($"[QuestSystem] Saved quest not found: {record.questAssetName}");
                continue;
            }

            var state = new QuestRuntimeState(quest) { status = record.status };
            for (int i = 0; i < state.objectiveCounts.Length && i < record.objectiveCounts.Count; i++)
                state.objectiveCounts[i] = record.objectiveCounts[i];

            _states[quest.name] = state;
        }
    }
}
