using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSkill", menuName = "Valdris/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Unlocking")]
    [Tooltip("Skill points spent per rank.")]
    public int pointCost = 1;
    [Tooltip("Maximum ranks. Total bonus = the per-rank effects below x current rank.")]
    public int maxRank = 1;
    public SkillData[] prerequisites;

    [Header("Effect (per rank)")]
    [Tooltip("Stat changes granted PER RANK. Total = these amounts x current rank. " +
             "Same flat/percent system as buffs.")]
    public List<StatModifier> modifiers = new();

    [Tooltip("Per-rank changes to specific spells (e.g. Ember Mastery boosting only Ember).")]
    public List<SpellModifier> spellModifiers = new();

    [Header("Weapon Mastery (optional)")]
    [Tooltip("If set to a type other than None/Other, this skill grants its attackDamagePercentPerRank " +
             "ONLY while a weapon of this type is equipped.")]
    public WeaponType masteryWeaponType = WeaponType.None;
    [Tooltip("Attack-damage % per rank granted while the mastery weapon type is equipped.")]
    public float attackDamagePercentPerRank = 0f;

    [Header("Special Effects (per rank)")]
    [Tooltip("Heal this % of damage dealt, on hit (melee/ranged/thrown).")]
    public float lifestealPercentPerRank = 0f;
    [Tooltip("Reduce spell mana cost by this % (capped at 90% total).")]
    public float manaCostReductionPercentPerRank = 0f;
    [Tooltip("Increase XP gained by this %.")]
    public float xpGainPercentPerRank = 0f;
    [Tooltip("Increase gold found on corpses/containers by this %.")]
    public float goldFindPercentPerRank = 0f;
    [Tooltip("Loot rarity bias per rank (wiring staged).")]
    public float lootRarityBonusPerRank = 0f;
}
