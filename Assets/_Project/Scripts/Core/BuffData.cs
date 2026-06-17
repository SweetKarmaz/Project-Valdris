using UnityEngine;
using System.Collections.Generic;

// Definition of a buff or affliction (debuff). Assets of this type form the
// catalog of every effect that can be applied to a character.
[CreateAssetMenu(fileName = "NewBuff", menuName = "Valdris/Buff")]
public class BuffData : ScriptableObject
{
    [Header("Identity")]
    public string buffName;
    public Sprite icon;
    [TextArea] public string description;
    public bool isDebuff;

    [Header("Stat Changes")]
    public List<StatModifier> modifiers = new();

    [Header("Damage Over Time")]
    [Tooltip("Damage applied each tick while active (0 = no DoT). Used for Burning, Corruption, etc.")]
    public float dotDamagePerTick;
    [Min(0.1f)]
    [Tooltip("Seconds between DoT ticks.")]
    public float dotTickInterval = 1f;
    [Tooltip("Damage type of the DoT (drives combat-text colour and future resistances/VFX).")]
    public DamageType dotDamageType = DamageType.Fire;

    [Tooltip("Changes to specific spells while this buff/affliction is active (e.g. a curse weakening Ward).")]
    public List<SpellModifier> spellModifiers = new();

    [Header("Status Effect")]
    [Tooltip("Crowd-control category, used for immunity/resilience checks. None for plain stat buffs.")]
    public StatusEffectType statusType = StatusEffectType.None;
    public bool preventsMovement;
    public bool preventsAbilities;
    [Tooltip("Removed when the affected character takes damage (typical for Sleep).")]
    public bool breaksOnDamage;

    [Header("Visibility")]
    [Tooltip("Hidden buffs are active but invisible in UI/inspection until revealed (e.g. by Detect Corruption).")]
    public bool isHidden;

    [Header("Duration")]
    public BuffDurationType durationType;
    [Tooltip("Only used when durationType is Timed. Seconds (3600 = 1 hour).")]
    public float durationSeconds;
    [Tooltip("Only used when durationType is UntilQuestComplete.")]
    public QuestData linkedQuest;
    [Tooltip("Only used when durationType is WhileInArea. Must match the AreaBuffZone's areaId.")]
    public string areaId;

    [Header("Visuals")]
    public GameObject persistentVFXPrefab;
}

[System.Serializable]
public class StatModifier
{
    public StatType stat;
    public ModifierMode mode;
    [Tooltip("Flat: added to the stat. Percent: 15 = +15%, -20 = -20%. Percent applies after all flat bonuses.")]
    public float amount;
}

public enum ModifierMode
{
    Flat,
    Percent
}

// NOTE: serialized as ints in assets — only append, never reorder.
public enum StatType
{
    // Derived stats
    MaxHealth,
    MaxMana,
    AttackDamage,
    AttackSpeed,
    Defense,
    MoveSpeed,
    // Attributes
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Wisdom,
    Charisma,
    SpellAcuity,
    // Crit (flat = percentage points; percent mode also works)
    PhysicalCritChance,
    SpellCritChance,
    CritDamage, // modifies the crit multiplier, in percent points (e.g. +25 = +0.25x)
    // Elemental damage resistances (flat mode = percentage points of damage reduced).
    FireResist,
    IceResist,
    LightningResist,
    HolyResist,
    CorruptionResist
}

// Crowd-control categories. Characters can be immune or resilient per type.
public enum StatusEffectType
{
    None,
    Sleep,
    Stun,
    Root,
    Silence,
    Slow,
    Fear,
    Charm,
    Corruption // hidden taint; immunity/resilience here resists *detection*
}

public enum BuffDurationType
{
    Timed,              // expires after durationSeconds
    UntilQuestComplete, // removed when linkedQuest is completed
    WhileInArea,        // active only inside the matching AreaBuffZone
    Permanent           // never expires (removed only explicitly)
}
