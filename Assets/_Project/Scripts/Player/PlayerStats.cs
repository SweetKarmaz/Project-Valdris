using UnityEngine;

// Player stats in two layers:
//
//   Attributes (STR, DEX, CON, INT, WIS, CHA, Spell Acuity) — the numbers the
//   player invests in. Buffs can modify them.
//
//   Derived stats (health, mana, damage...) — computed from attributes, base
//   values, gear bonuses, and buffs:
//     effective = (base + attribute scaling + gear flat + buff flat)
//                 * (1 + buff percent / 100)
//
// Everything is computed on demand, so equipping gear or gaining/losing a
// buff changes the stat immediately.
public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Attributes")]
    public float strength = 10f;     // melee damage
    public float dexterity = 10f;    // attack speed, defense
    public float constitution = 10f; // max health
    public float intelligence = 10f; // max mana
    public float wisdom = 10f;       // mana, corruption resistance
    public float charisma = 10f;     // dialogue and vendor prices (hooked up later)
    public float spellAcuity = 10f;  // spell damage/potency, independent of INT/WIS

    [Header("Base Derived Stats")]
    public float baseMaxHealth = 80f;
    public float baseMaxMana = 50f;
    public float baseAttackDamage = 5f;
    public float baseAttackSpeed = 1f;
    public float baseDefense = 0f;

    [Header("Attribute Scaling")]
    public float healthPerConstitution = 5f;
    public float manaPerIntelligence = 3f;
    public float manaPerWisdom = 2f;
    public float damagePerStrength = 1f;
    public float attackSpeedPerDexterity = 0.01f;
    public float defensePerDexterity = 0.25f;
    [Tooltip("Spell damage multiplier gained per point of Spell Acuity above 10. 0.02 = +2%/point.")]
    public float spellPowerPerAcuity = 0.02f;

    [Header("Critical Hits")]
    [Tooltip("Base crit chance in percent, before attributes.")]
    public float baseCritChance = 5f;
    [Tooltip("Physical crit chance percent gained per point of Dexterity above 10.")]
    public float critPerDexterity = 0.5f;
    [Tooltip("Spell crit chance percent gained per point of Spell Acuity above 10.")]
    public float critPerAcuity = 0.5f;
    [Tooltip("Damage multiplier on a critical hit.")]
    public float baseCritMultiplier = 1.5f;
    [Tooltip("Fraction of INCOMING corruption resisted per point of Wisdom above 10. Capped at 75%.")]
    public float corruptionResistPerWisdom = 0.01f;
    [Tooltip("Fraction of corruption GAIN (from your own actions/zones) reduced per point of " +
             "Wisdom above 10. A small effect — capped at 50%.")]
    public float corruptionGainResistPerWisdom = 0.01f;

    public float CurrentHealth { get; private set; }
    public float CurrentMana { get; private set; }

    private CharacterBuffs _buffs;
    CharacterAnimator _charAnim;
    CharacterAnimator CharAnim => _charAnim != null ? _charAnim : (_charAnim = GetComponent<CharacterAnimator>());

    // ---- Effective attributes (base + buff flat, then buff percent) ----

    public float Strength => Attribute(StatType.Strength, strength);
    public float Dexterity => Attribute(StatType.Dexterity, dexterity);
    public float Constitution => Attribute(StatType.Constitution, constitution);
    public float Intelligence => Attribute(StatType.Intelligence, intelligence);
    public float Wisdom => Attribute(StatType.Wisdom, wisdom);
    public float Charisma => Attribute(StatType.Charisma, charisma);
    public float SpellAcuity => Attribute(StatType.SpellAcuity, spellAcuity);

    // ---- Effective derived stats ----

    public float MaxHealth => Mathf.Max(1f, Derived(StatType.MaxHealth,
        baseMaxHealth + Constitution * healthPerConstitution));

    public float MaxMana => Mathf.Max(0f, Derived(StatType.MaxMana,
        baseMaxMana + Intelligence * manaPerIntelligence + Wisdom * manaPerWisdom));

    public float AttackDamage => Mathf.Max(0f, Derived(StatType.AttackDamage,
        baseAttackDamage + Strength * damagePerStrength + EquippedWeaponDamage));

    // Damage contributed by the currently equipped main-hand weapon (0 when unarmed
    // or when a ranged weapon is held — bows/thrown carry their damage on the projectile).
    float EquippedWeaponDamage
    {
        get
        {
            var w = InventorySystem.Instance?.GetEquippedLoot(EquipSlot.MainHand);
            if (w == null || w.itemType != LootItemType.Weapon) return 0f;
            if (w.weaponCategory == WeaponCategory.Ranged)      return 0f;
            return w.weaponDamage;
        }
    }

    public float AttackSpeed => Mathf.Max(0.1f, Derived(StatType.AttackSpeed,
        baseAttackSpeed + Dexterity * attackSpeedPerDexterity));

    public float Defense => Mathf.Max(0f, Derived(StatType.Defense,
        baseDefense + Dexterity * defensePerDexterity));

    public float MoveSpeedBonus => Derived(StatType.MoveSpeed, 0f);

    // Multiplier applied to spell damage/potency. Acuity 10 = 1.0 (neutral).
    public float SpellPowerMultiplier => Mathf.Max(0.1f, 1f + (SpellAcuity - 10f) * spellPowerPerAcuity);

    // Fraction of incoming corruption resisted. Wisdom 10 = none. (Reserved for
    // future incoming corruption-damage mitigation.)
    public float CorruptionResistance =>
        Mathf.Clamp((Wisdom - 10f) * corruptionResistPerWisdom, 0f, 0.75f);

    // Fraction of corruption GAIN reduced (self-corruption from your own actions).
    // A small, separate effect from incoming-corruption resistance. Wisdom 10 = none.
    public float CorruptionGainResistance =>
        Mathf.Clamp((Wisdom - 10f) * corruptionGainResistPerWisdom, 0f, 0.5f);

    // ---- Elemental resistances ----
    // Percent of incoming damage of a given type that is reduced. Sourced from
    // gear/buffs/skills (resistance StatTypes), plus a Wisdom contribution to
    // Corruption. Physical isn't a resistance — it's mitigated by Defense.
    // Capped at 75% so a type is never fully negated.
    public float ResistancePercent(DamageType type)
    {
        float total = type switch
        {
            DamageType.Fire       => Flat(StatType.FireResist),
            DamageType.Ice        => Flat(StatType.IceResist),
            DamageType.Lightning  => Flat(StatType.LightningResist),
            DamageType.Holy       => Flat(StatType.HolyResist),
            // Poison is folded into Corruption gameplay-wise (shares its resist).
            DamageType.Corruption => Flat(StatType.CorruptionResist) + CorruptionResistance * 100f,
            DamageType.Poison     => Flat(StatType.CorruptionResist) + CorruptionResistance * 100f,
            _                     => 0f,
        };
        return Mathf.Clamp(total, 0f, 75f);
    }

    // ---- Critical hits ----
    // Physical crit scales with Dexterity; spell crit with Spell Acuity.
    // Both capped at 75% so crits never become guaranteed.

    public float PhysicalCritChance => Mathf.Clamp(Derived(StatType.PhysicalCritChance,
        baseCritChance + Mathf.Max(0f, Dexterity - 10f) * critPerDexterity), 0f, 75f);

    public float SpellCritChance => Mathf.Clamp(Derived(StatType.SpellCritChance,
        baseCritChance + Mathf.Max(0f, SpellAcuity - 10f) * critPerAcuity), 0f, 75f);

    // CritDamage modifiers are percent points added to the multiplier
    // (+25 flat = +0.25x).
    public float CritMultiplier =>
        Mathf.Max(1f, baseCritMultiplier + Flat(StatType.CritDamage) / 100f);

    public bool RollPhysicalCrit() => UnityEngine.Random.Range(0f, 100f) < PhysicalCritChance;
    public bool RollSpellCrit() => UnityEngine.Random.Range(0f, 100f) < SpellCritChance;

    // ---- Stat math ----

    private float Attribute(StatType stat, float baseValue) =>
        Mathf.Max(0f, ApplyPercent(stat, baseValue + Flat(stat)));

    private float Derived(StatType stat, float baseValue) =>
        ApplyPercent(stat, baseValue + Flat(stat));

    private float ApplyPercent(StatType stat, float value) =>
        value * (1f + Percent(stat) / 100f);

    // Flat and percent modifiers come from temporary buffs, permanently unlocked
    // skills, and equipped gear (LootItem.statModifiers + armorValue→Defense);
    // all stack additively within their mode.
    private float Flat(StatType stat) =>
        (_buffs != null ? _buffs.TotalFlat(stat) : 0f)
        + (SkillSystem.Instance != null ? SkillSystem.Instance.TotalFlat(stat) : 0f)
        + (InventorySystem.Instance != null ? InventorySystem.Instance.GearFlat(stat) : 0f);

    private float Percent(StatType stat) =>
        (_buffs != null ? _buffs.TotalPercent(stat) : 0f)
        + (SkillSystem.Instance != null ? SkillSystem.Instance.TotalPercent(stat) : 0f)
        + (InventorySystem.Instance != null ? InventorySystem.Instance.GearPercent(stat) : 0f);

    // ---- Lifecycle ----

    private void Awake()
    {
        _buffs = GetComponent<CharacterBuffs>();
        // CharacterAnimator is resolved lazily via CharAnim — it may be added
        // after this component on dynamically built player GameObjects.
        CurrentHealth = MaxHealth;
        CurrentMana   = MaxMana;
    }

    private void Start()
    {
        // Push initial values so the HUD bars show full on spawn (the HUD may not
        // have existed yet during Awake).
        HUDController.Instance?.UpdateHealth(CurrentHealth, MaxHealth);
        HUDController.Instance?.UpdateMana(CurrentMana, MaxMana);
    }

    // Restores the player to full health/mana (used on load — vitals aren't saved —
    // and to clear a death state). Refreshes the HUD bars.
    public void ReviveFull()
    {
        CurrentHealth = MaxHealth;
        CurrentMana   = MaxMana;
        CharAnim?.Revive();
        HUDController.Instance?.UpdateHealth(CurrentHealth, MaxHealth);
        HUDController.Instance?.UpdateMana(CurrentMana, MaxMana);
    }

    private void OnEnable()
    {
        if (_buffs != null) _buffs.OnBuffRemoved += HandleEffectLost;
        if (InventorySystem.Instance != null)
            InventorySystem.Instance.OnLootUnequipped += HandleGearLost;
    }

    private void OnDisable()
    {
        if (_buffs != null) _buffs.OnBuffRemoved -= HandleEffectLost;
        if (InventorySystem.Instance != null)
            InventorySystem.Instance.OnLootUnequipped -= HandleGearLost;
    }

    // When a max-raising effect ends or gear is removed, current values must not
    // exceed the new max.
    private void HandleEffectLost(BuffData _) => ClampCurrentToMax();
    private void HandleGearLost(EquipSlot _, LootItem __) => ClampCurrentToMax();

    private void ClampCurrentToMax()
    {
        CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
        CurrentMana = Mathf.Min(CurrentMana, MaxMana);
    }

    // ---- Combat / resource methods ----

    public void TakeDamage(float amount) =>
        TakeDamage(new DamageInfo(amount, DamageType.Physical));

    public void TakeDamage(DamageInfo info)
    {
        // Physical is reduced by Defense; elemental/corruption are reduced by the
        // matching elemental resistance (percent of damage).
        float damage = info.type == DamageType.Physical
            ? Mathf.Max(0f, info.amount - Defense)
            : Mathf.Max(0f, info.amount * (1f - ResistancePercent(info.type) / 100f));

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        CombatTextSystem.Instance?.ShowDamage(transform, damage, info.isCrit, info.type);
        _buffs?.NotifyDamageTaken();
        HUDController.Instance?.UpdateHealth(CurrentHealth, MaxHealth);
        if (CurrentHealth <= 0f) Die();
        else if (damage > 0f) CharAnim?.TriggerHit();
    }

    public void Heal(float amount)
    {
        float healed = Mathf.Min(MaxHealth - CurrentHealth, amount);
        CurrentHealth += healed;
        if (healed > 0f)
            CombatTextSystem.Instance?.Show(transform, $"+{healed:0}", new Color(0.4f, 1f, 0.4f)); // green: healing
        HUDController.Instance?.UpdateHealth(CurrentHealth, MaxHealth);
    }

    public bool UseMana(float amount)
    {
        if (CurrentMana < amount) return false;
        CurrentMana -= amount;
        HUDController.Instance?.UpdateMana(CurrentMana, MaxMana);
        return true;
    }

    public void RestoreMana(float amount)
    {
        CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        HUDController.Instance?.UpdateMana(CurrentMana, MaxMana);
    }

    private void Die()
    {
        CharAnim?.TriggerDeath();
        Debug.Log("[PlayerStats] Player died.");
    }
}
