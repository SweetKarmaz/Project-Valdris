using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "Valdris/Quest")]
public class QuestData : ScriptableObject
{
    [Header("Identity")]
    public string title;
    [TextArea(2, 4)] public string description;

    [Header("Requirements")]
    [Tooltip("All of these world flags must be true before this quest is offered to the player.")]
    public string[] requiredWorldFlags;

    [Header("Trigger")]
    public QuestTriggerType triggerType;

    [Header("Objectives")]
    public List<QuestObjective> objectives = new();

    [Header("On Accept")]
    [Tooltip("Items handed to the player when the quest is accepted (e.g. the stick for a tutorial).")]
    public List<LootItem> itemGrantsOnAccept = new();

    [Header("Turn In")]
    [Tooltip("If true (or if no Turn-In NPC is set), the quest completes and pays out " +
             "automatically when its objectives are done — no NPC hand-in needed. Use for tutorials.")]
    public bool autoComplete = false;
    [Tooltip("saveId of the NPC the player must speak to in order to complete the quest. " +
             "Leave blank for an auto-completing quest.")]
    public string turnInNpcSaveId;
    [TextArea(2, 4)]
    [Tooltip("What the NPC says when the player turns in the quest.")]
    public string turnInDialogue;

    [TextArea(2, 4)]
    [Tooltip("Flavour text shown on the 'Quest Complete' popup when this quest is finished.")]
    public string completionText;

    [Header("Rewards")]
    public int goldReward;
    public int xpReward;
    public int statPointReward;
    [Tooltip("Corruption applied on turn-in. Positive taints the player (dark deeds); " +
             "negative cleanses (redemption quests).")]
    public float corruptionReward;
    [Tooltip("LootItem prefabs (from Assets/_Project/Prefabs/Loot/) granted on turn-in.")]
    public List<LootItem> itemRewards = new();
    public List<SpellData> spellRewards = new();

    [Header("Story")]
    [Tooltip("World flags set to true when the player accepts this quest.")]
    public string[] setsWorldFlagsOnAccept;
    [Tooltip("World flags set to true when the quest is turned in.")]
    public string[] setsWorldFlagsOnComplete;
}
