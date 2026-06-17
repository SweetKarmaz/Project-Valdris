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
    public int pointCost = 1;
    public SkillData[] prerequisites;

    [Header("Effect")]
    [Tooltip("Permanent stat changes granted while this skill is unlocked. Uses the same flat/percent system as buffs.")]
    public List<StatModifier> modifiers = new();

    [Tooltip("Permanent changes to specific spells (e.g. Ember Mastery boosting only Ember).")]
    public List<SpellModifier> spellModifiers = new();
}
