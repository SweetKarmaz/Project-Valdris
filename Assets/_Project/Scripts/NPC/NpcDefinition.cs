using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Valdris/NPC Definition", fileName = "NewNpcDefinition")]
public class NpcDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    public string title;
    public NpcClass npcClass = NpcClass.Peasant;
    public NpcFaction faction = NpcFaction.None;
    public bool isEssential;
    public Sprite portrait;

    [Header("Stats")]
    public int level = 1;
    public float maxHealth = 100f;
    public float maxMana = 0f;
    public float baseArmor = 0f;
    public int xpReward = 10;

    [Header("Movement & Behavior")]
    public NpcBehaviorType behaviorType = NpcBehaviorType.Wander;
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;
    public string[] allowedZones = new string[0];
    public float idleSecondsMin = 2f;
    public float idleSecondsMax = 8f;

    [Header("Combat")]
    public bool isAggressive;
    public float aggroRange = 10f;
    public float fleeHealthThreshold = 0f;
    // attackRange / attackDamage / attackCooldown are derived from the NPC's
    // equipped weapon (LootItem) at runtime — they are no longer archetype data.

    [Tooltip("NPC cycles through these left to right, falling back when out of mana or ammo.")]
    public AttackType[] attackPriority = { AttackType.Melee };

    [Header("Reactions")]
    public WitnessedKillReaction witnessedKillReaction = WitnessedKillReaction.None;
    public FactionFilter reactToFaction = FactionFilter.None;
    [Tooltip("Seconds the NPC runs from the kill location before returning to their prior position.")]
    public float fleeDuration = 60f;

    [Header("Spells")]
    public List<SpellData> knownSpells = new();
    public float manaRegenRate = 1f;

    [Header("Loot")]
    [Tooltip("Archetype gold range dropped on death. Item contents are set per-placed-NPC " +
             "on the NpcController component, not here.")]
    public int goldMin;
    public int goldMax;
    [Tooltip("When false, no loot window appears on death (e.g. essential story NPCs).")]
    public bool isLootable = true;

    // Story flags (requiredWorldFlags / setsWorldFlagsOnDeath) and dialogue
    // (canTalk / greetingLine) are per-placed-NPC and live on NpcController.
}
