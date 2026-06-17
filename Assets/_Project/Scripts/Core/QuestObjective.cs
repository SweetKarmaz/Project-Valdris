using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestStatus
{
    NotStarted,
    InProgress,
    ReadyToTurnIn,
    TurnedIn,
    Cancelled
}

public enum QuestObjectiveType
{
    KillNpc,          // Kill X NPCs whose definition asset name matches targetId
    DeliverItem,      // Have item in inventory (checked on turn-in)
    TalkToNpc,        // Interact with NPC whose saveId matches targetId
    ReachZone,        // Enter zone whose id matches targetId
    InteractWithProp, // Interact with prop whose propId matches targetId
}

public enum QuestTriggerType
{
    Dialogue,        // NPC offers via a Yes/No dialogue line
    PropInteraction, // Player interacts with a QuestTriggerProp in the scene
    ZoneEnter,       // Player walks into a QuestTriggerZone collider
    Auto,            // Accepted automatically when required world flags are met
}

[Serializable]
public class QuestObjective
{
    [Tooltip("Shown in the quest log, e.g. 'Kill 5 Bandits'")]
    public string description;
    public QuestObjectiveType type;
    [Tooltip("NPC definition asset name for Kill; item asset name for Deliver; " +
             "NPC saveId for Talk; zone id for Reach; prop id for Interact.")]
    public string targetId;
    [Tooltip("How many times this objective must be fulfilled. Use 1 for single-step tasks.")]
    public int requiredCount = 1;
}

// ── Runtime state (not a ScriptableObject — tracked only at runtime) ─────────

public class QuestRuntimeState
{
    public QuestData data;
    public QuestStatus status;
    public int[] objectiveCounts;

    public QuestRuntimeState(QuestData data)
    {
        this.data      = data;
        status         = QuestStatus.InProgress;
        objectiveCounts = new int[data.objectives != null ? data.objectives.Count : 0];
    }
}

// ── Serializable save record ──────────────────────────────────────────────────

[Serializable]
public class QuestRuntimeSave
{
    public string questAssetName;
    public QuestStatus status;
    public List<int> objectiveCounts = new();
}
