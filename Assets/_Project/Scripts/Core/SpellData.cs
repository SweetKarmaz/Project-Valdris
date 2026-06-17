using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSpell", menuName = "Valdris/Spell")]
public class SpellData : ScriptableObject
{
    [Header("Identity")]
    public string spellName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Casting")]
    public SpellType spellType;
    public float manaCost;
    public float cooldown;
    public float castTime;

    [Header("Effect")]
    public float damage;
    [Tooltip("Damage type of this spell (drives colour, resistances, and the debuff theme).")]
    public DamageType damageType = DamageType.Fire;
    public float duration;
    public float range;

    [Tooltip("Additional elemental riders applied on hit (bonus damage and/or debuff procs), " +
             "on top of the spell's primary damage + status buff.")]
    public List<OnHitEffect> onHitEffects = new();

    [Header("Status Effect")]
    [Tooltip("Buff/affliction applied to the target on a successful cast (e.g. Sleep). Subject to the target's immunities and resistances.")]
    public BuffData statusBuff;
    [Tooltip("Extra seconds of status duration per point of the caster's Intelligence above 10.")]
    public float durationPerIntelligence;

    [Header("Corruption")]
    public float corruptionGain;

    [Header("Visuals")]
    public GameObject projectilePrefab;
    public GameObject castVFXPrefab;
}

public enum SpellType { Projectile, Shield, Utility, Detection }

// A percentage adjustment to one specific spell. Skills and buffs can carry
// these to strengthen (or curse) individual spells — e.g. Ember Mastery:
// +25% damage, -25% mana cost on Ember only.
[System.Serializable]
public class SpellModifier
{
    public SpellData spell;
    [Tooltip("Percent change to damage. 25 = +25%.")]
    public float damagePercent;
    [Tooltip("Percent change to mana cost. -25 = 25% cheaper.")]
    public float manaCostPercent;
    [Tooltip("Percent change to cooldown. -50 = half cooldown.")]
    public float cooldownPercent;
}
