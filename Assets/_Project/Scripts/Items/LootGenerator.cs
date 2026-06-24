using System.Collections.Generic;
using UnityEngine;

// Generates a randomized item of a requested rarity from the classified base
// prefabs in the LootRegistry. Produces an ItemRoll, then a live LootItem via
// LootItemFactory. See randomized_loot_system design notes for the rules.
public static class LootGenerator
{
    // ── Public entry ────────────────────────────────────────────────────────────

    // Builds one randomized item of the given rarity, or null if no bases exist.
    public static LootItem GenerateItem(ItemRarity rarity)
    {
        var registry = SaveSystem.Instance?.database?.lootRegistry;
        if (registry == null || registry.GenBases.Count == 0)
        {
            Debug.LogWarning("[LootGenerator] No generatable bases in LootRegistry " +
                             "(rebuild via Tools > Valdris > Rebuild Loot Registry).");
            return null;
        }

        var bases = registry.GenBases;
        var pick  = bases[Random.Range(0, bases.Count)];
        if (pick.item == null) return null;

        var roll = BuildRoll(pick.item, pick.category, rarity);
        return LootItemFactory.Build(roll);
    }

    // Generates a weapon matching an NPC's attack type (used for auto-gear).
    public static LootItem GenerateWeaponForAttack(AttackType atk, ItemRarity rarity)
    {
        System.Func<GenCategory, bool> filter = atk switch
        {
            AttackType.Ranged => c => c == GenCategory.Ranged,
            AttackType.Thrown => c => c == GenCategory.Thrown,
            AttackType.Spells => c => c == GenCategory.Staff || c == GenCategory.Sceptre,
            _                 => IsMartialMelee,   // Melee
        };
        return GenerateFiltered(rarity, filter);
    }

    static bool IsMartialMelee(GenCategory c) =>
        c == GenCategory.AxeOneHand || c == GenCategory.AxeTwoHand ||
        c == GenCategory.MaceOneHand || c == GenCategory.MaceTwoHand ||
        c == GenCategory.SwordOneHand || c == GenCategory.SwordTwoHand ||
        c == GenCategory.Dagger || c == GenCategory.Spear;

    static LootItem GenerateFiltered(ItemRarity rarity, System.Func<GenCategory, bool> filter)
    {
        var registry = SaveSystem.Instance?.database?.lootRegistry;
        if (registry == null) return null;

        var pool = new List<LootRegistry.GenBase>();
        foreach (var gb in registry.GenBases)
            if (gb.item != null && filter(gb.category)) pool.Add(gb);
        if (pool.Count == 0) return null;

        var pick = pool[Random.Range(0, pool.Count)];
        return LootItemFactory.Build(BuildRoll(pick.item, pick.category, rarity));
    }

    // ── Roll construction ────────────────────────────────────────────────────────

    static ItemRoll BuildRoll(LootItem baseItem, GenCategory cat, ItemRarity rarity)
    {
        bool isWeapon = LootClassifier.IsWeapon(cat);
        var (min, max) = RangeFor(cat);

        // Stat band: each rarity occupies its own ascending 20% of the range.
        int band = Mathf.Clamp((int)rarity, 0, 4);
        float span = (max - min) / 5f;
        float lo = min + band * span;
        float primary = Mathf.Round(Random.Range(lo, lo + span));

        var roll = new ItemRoll
        {
            basePrefabName = baseItem.ItemName,
            rarity         = rarity,
            isWeapon       = isWeapon,
            isTwoHanded    = LootClassifier.IsTwoHandedCategory(cat),
            category       = cat,
        };
        if (isWeapon) roll.weaponDamage = primary;
        else          roll.armorValue   = primary;

        // Magic weighting context.
        int magicBias = (cat == GenCategory.Staff || cat == GenCategory.Sceptre) ? 1   // favour magical
                      : isWeapon ? -1                                                   // favour martial
                      : 0;                                                              // armor: neutral

        int affixCount = AffixCount(rarity);
        var used      = new HashSet<StatType>();
        var usedRider = new HashSet<DamageType>();
        var named     = new List<(string word, float mag)>();
        var descs     = new List<string>();

        for (int i = 0; i < affixCount; i++)
            AddAffix(roll, cat, isWeapon, rarity, magicBias, used, usedRider, named, descs, negative: false);

        // Legendary: 10% chance of one extra negative affix at the Rare magnitude.
        if (rarity == ItemRarity.Legendary && Random.value < 0.1f)
            AddAffix(roll, cat, isWeapon, ItemRarity.Rare, magicBias, used, usedRider, named, descs, negative: true);

        NameAndFlavor(roll, cat, rarity, named, descs);
        roll.goldValue = ComputeValue(roll);
        return roll;
    }

    // ── Affixes ──────────────────────────────────────────────────────────────────

    struct Cand
    {
        public StatType   stat;       // for stat affixes
        public bool       isRider;    // weapon bonus damage type
        public DamageType dmg;        // for riders
        public string     word;       // naming word
        public int        magic;      // +1 magical, -1 martial, 0 neutral
        public byte       pool;       // 0 attr,1 vitals,2 combat,3 resist,4 rider
    }

    static void AddAffix(ItemRoll roll, GenCategory cat, bool isWeapon, ItemRarity rarity,
        int magicBias, HashSet<StatType> used, HashSet<DamageType> usedRider,
        List<(string, float)> named, List<string> descs, bool negative)
    {
        var cands = Candidates(isWeapon, used, usedRider);
        if (cands.Count == 0) return;

        // Weighted pick by magic bias.
        float total = 0f;
        foreach (var c in cands) total += Weight(c, magicBias);
        float r = Random.Range(0f, total);
        Cand chosen = cands[cands.Count - 1];
        foreach (var c in cands) { r -= Weight(c, magicBias); if (r <= 0f) { chosen = c; break; } }

        float sign = negative ? -1f : 1f;

        if (chosen.isRider)
        {
            // Riders are weapon-only and never negative.
            float dmg = Mathf.Round(Magnitude(4, rarity));
            roll.bonusDamage.Add(new RolledOnHit { type = chosen.dmg, damage = dmg });
            usedRider.Add(chosen.dmg);
            named.Add((chosen.word, dmg));
            descs.Add($"Adds {dmg:0} {chosen.word} damage.");
            return;
        }

        used.Add(chosen.stat);

        bool combat = chosen.pool == 2;
        bool resist = chosen.pool == 3;
        bool regen  = chosen.pool == 5;
        // Damage/speed/defense scale as a true Percent; crit stats are stored as
        // Flat percentage points (that's how PlayerStats reads them).
        bool percentMode = combat && !IsCritStat(chosen.stat);

        float amount = chosen.pool == 0
            ? AttributeMagnitude(rarity) * sign                 // attributes are fixed steps
            : regen
            ? RegenMagnitude(rarity) * sign                     // regen is a fixed fractional step
            : Mathf.Round(Magnitude(chosen.pool, rarity)) * sign;

        roll.statModifiers.Add(new StatModifier
        {
            stat   = chosen.stat,
            mode   = percentMode ? ModifierMode.Percent : ModifierMode.Flat,
            amount = amount,
        });
        named.Add((chosen.word, Mathf.Abs(amount)));

        string verb = negative ? "Reduces" : "Increases";
        string unit = combat ? "%" : resist ? "% resistance" : regen ? " per 5s" : "";
        string amt  = regen ? Mathf.Abs(amount).ToString("0.0") : Mathf.Abs(amount).ToString("0");
        descs.Add($"{verb} {chosen.word} by {amt}{unit}.");
    }

    static List<Cand> Candidates(bool isWeapon, HashSet<StatType> used, HashSet<DamageType> usedRider)
    {
        var list = new List<Cand>();
        void Add(StatType s, byte pool, string word, int magic)
        { if (!used.Contains(s)) list.Add(new Cand { stat = s, pool = pool, word = word, magic = magic }); }

        // Attributes (Flat +step)
        Add(StatType.Strength,     0, "Strength",  -1);
        Add(StatType.Dexterity,    0, "Dexterity", -1);
        Add(StatType.Constitution, 0, "Vigor",     -1);
        Add(StatType.Intelligence, 0, "Intellect",  1);
        Add(StatType.Wisdom,       0, "Wisdom",     1);
        Add(StatType.Charisma,     0, "Charm",      0);
        Add(StatType.SpellAcuity,  0, "Sorcery",    1);
        // Vitals (Flat)
        Add(StatType.MaxHealth,    1, "Health",    -1);
        Add(StatType.MaxMana,      1, "Mana",       1);
        // Regen (Flat, rarity-fixed amount — see RegenMagnitude)
        Add(StatType.HealthRegen,  5, "Renewal",     -1);
        Add(StatType.ManaRegen,    5, "Restoration",  1);
        // Combat (Percent)
        Add(StatType.AttackDamage,       2, "Power",     -1);
        Add(StatType.AttackSpeed,        2, "Speed",     -1);
        Add(StatType.Defense,            2, "Defense",    0);
        Add(StatType.PhysicalCritChance, 2, "Precision", -1);
        Add(StatType.SpellCritChance,    2, "Focus",      1);
        Add(StatType.CritDamage,         2, "Wrath",      0);
        // Resistances (Flat % points)
        Add(StatType.FireResist,       3, "Fire",     0);
        Add(StatType.IceResist,        3, "Frost",    0);
        Add(StatType.LightningResist,  3, "Storm",    0);
        Add(StatType.HolyResist,       3, "Light",    1);
        Add(StatType.CorruptionResist, 3, "Warding",  1);

        // Bonus damage types — weapons only (armor gets resistances instead).
        if (isWeapon)
        {
            void Rider(DamageType d, string word, int magic)
            { if (!usedRider.Contains(d)) list.Add(new Cand { isRider = true, dmg = d, pool = 4, word = word, magic = magic }); }
            Rider(DamageType.Fire,       "Fire",       0);
            Rider(DamageType.Ice,        "Frost",      0);
            Rider(DamageType.Lightning,  "Storm",      0);
            Rider(DamageType.Holy,       "Light",      1);
            Rider(DamageType.Corruption, "Corruption", 1);
        }
        return list;
    }

    static bool IsCritStat(StatType s) =>
        s == StatType.PhysicalCritChance || s == StatType.SpellCritChance || s == StatType.CritDamage;

    static float Weight(Cand c, int magicBias)
    {
        if (magicBias == 0) return 1f;                         // armor: neutral
        if (magicBias > 0)  return c.magic > 0 ? 3f : c.magic < 0 ? 0.35f : 1f;  // staff/sceptre
        return c.magic < 0 ? 2f : c.magic > 0 ? 0.4f : 1f;     // martial weapon
    }

    // ── Magnitude tables ─────────────────────────────────────────────────────────

    static int AffixCount(ItemRarity r) => r switch
    {
        ItemRarity.Uncommon  => 1,
        ItemRarity.Rare      => 2,
        ItemRarity.Epic      => 3,
        ItemRarity.Legendary => 4,
        _                    => 0,
    };

    static float AttributeMagnitude(ItemRarity r) => r switch
    {
        ItemRarity.Epic      => 2f,
        ItemRarity.Legendary => 4f,
        _                    => 1f,   // Uncommon / Rare
    };

    // Health/Mana regen per 5s, fixed per rarity (Common can't roll affixes).
    static float RegenMagnitude(ItemRarity r) => r switch
    {
        ItemRarity.Uncommon  => 0.2f,
        ItemRarity.Rare      => 0.4f,
        ItemRarity.Epic      => 0.6f,
        ItemRarity.Legendary => 1.0f,
        _                    => 0f,
    };

    // pool: 1 vitals, 2 combat, 3 resist, 4 rider. Returns a rolled magnitude.
    static float Magnitude(byte pool, ItemRarity r)
    {
        // tier: 0 = minor (Unc/Rare), 1 = major (Epic), 2 = legendary (double major)
        int tier = r == ItemRarity.Epic ? 1 : r == ItemRarity.Legendary ? 2 : 0;
        switch (pool)
        {
            case 1: return tier == 0 ? Random.Range(10f, 30f) : tier == 1 ? Random.Range(15f, 50f) : Random.Range(30f, 100f);
            case 2: return tier == 0 ? Random.Range(1f, 5f)   : tier == 1 ? Random.Range(3f, 10f)  : Random.Range(6f, 20f);
            case 3: return tier == 0 ? Random.Range(1f, 5f)   : tier == 1 ? Random.Range(3f, 10f)  : Random.Range(6f, 20f);
            case 4: return tier == 0 ? Random.Range(5f, 10f)  : tier == 1 ? Random.Range(15f, 50f) : Random.Range(30f, 100f);
            default: return 0f;
        }
    }

    // ── Value (sell price) ───────────────────────────────────────────────────────
    // Weighted sum: base damage/defense + per-stat gold for each affix + elemental
    // riders, scaled and offset by rarity. Negative affixes shave a little off.
    static int ComputeValue(ItemRoll roll)
    {
        float v = roll.isWeapon ? roll.weaponDamage * 2.5f : roll.armorValue * 2.5f;

        if (roll.statModifiers != null)
            foreach (var m in roll.statModifiers)
            {
                float gold = Mathf.Abs(m.amount) * StatGoldWeight(m.stat);
                v += m.amount < 0f ? -gold * 0.5f : gold;
            }
        if (roll.bonusDamage != null)
            foreach (var b in roll.bonusDamage) v += b.damage * 3f;

        float[] rarMult = { 1f, 1.5f, 2.25f, 3.5f, 5f };
        int[]   rarFlat = { 2, 15, 50, 150, 500 };
        int idx = Mathf.Clamp((int)roll.rarity, 0, 4);
        v = v * rarMult[idx] + rarFlat[idx];
        return Mathf.Max(1, Mathf.RoundToInt(v));
    }

    // Gold per point/percent of a given stat.
    static float StatGoldWeight(StatType s) => s switch
    {
        StatType.Strength or StatType.Dexterity or StatType.Constitution or
        StatType.Intelligence or StatType.Wisdom or StatType.Charisma or
        StatType.SpellAcuity        => 60f,   // per attribute point
        StatType.MaxHealth          => 2f,
        StatType.MaxMana            => 2.5f,
        StatType.HealthRegen or StatType.ManaRegen => 60f,   // per 5s point — scarce, valuable
        StatType.AttackDamage       => 12f,   // per %
        StatType.AttackSpeed        => 15f,
        StatType.Defense            => 8f,
        StatType.PhysicalCritChance => 14f,
        StatType.SpellCritChance    => 14f,
        StatType.CritDamage         => 8f,
        StatType.FireResist or StatType.IceResist or StatType.LightningResist or
        StatType.HolyResist or StatType.CorruptionResist => 7f,
        _                           => 5f,
    };

    // ── Ranges ───────────────────────────────────────────────────────────────────

    static (float min, float max) RangeFor(GenCategory c) => c switch
    {
        GenCategory.AxeOneHand    => (10f, 75f),
        GenCategory.AxeTwoHand    => (20f, 125f),
        GenCategory.Dagger        => (5f, 35f),
        GenCategory.MaceOneHand   => (10f, 75f),
        GenCategory.MaceTwoHand   => (20f, 125f),
        GenCategory.Ranged        => (10f, 30f),
        GenCategory.Sceptre       => (5f, 15f),
        GenCategory.Spear         => (20f, 125f),
        GenCategory.Staff         => (10f, 25f),
        GenCategory.SwordOneHand  => (10f, 75f),
        GenCategory.SwordTwoHand  => (20f, 125f),
        GenCategory.Thrown        => (5f, 15f),
        GenCategory.Shield        => (5f, 50f),
        GenCategory.ArmorBack     => (5f, 15f),
        GenCategory.ArmorChest    => (20f, 150f),
        GenCategory.ArmorHands    => (5f, 25f),
        GenCategory.ArmorHead     => (5f, 25f),
        GenCategory.ArmorHips     => (10f, 50f),
        GenCategory.ArmorLegs     => (5f, 25f),
        GenCategory.ArmorShoulders=> (5f, 25f),
        GenCategory.Ring          => (1f, 5f),
        GenCategory.Necklace      => (1f, 5f),
        _                         => (1f, 5f),
    };

    // ── Naming ───────────────────────────────────────────────────────────────────

    static void NameAndFlavor(ItemRoll roll, GenCategory cat, ItemRarity rarity,
        List<(string word, float mag)> named, List<string> descs)
    {
        string baseWord = BaseName(cat);

        switch (rarity)
        {
            case ItemRarity.Common:
                roll.displayName = baseWord;
                roll.flavorText  = $"An ordinary {baseWord.ToLowerInvariant()}.";
                break;

            case ItemRarity.Uncommon:
                roll.displayName = named.Count > 0 ? $"{baseWord} of {named[0].word}" : baseWord;
                roll.flavorText  = string.Join(" ", descs);
                break;

            case ItemRarity.Rare:
                roll.displayName = named.Count > 1
                    ? $"Rare {baseWord} of {named[0].word} and {named[1].word}"
                    : $"Rare {baseWord}" + (named.Count > 0 ? $" of {named[0].word}" : "");
                roll.flavorText  = string.Join(" ", descs);
                break;

            case ItemRarity.Epic:
                string top = baseWord;
                float best = float.NegativeInfinity;
                foreach (var n in named) if (n.mag > best) { best = n.mag; top = n.word; }
                roll.displayName = named.Count > 0 ? $"Epic {baseWord} of {top}" : $"Epic {baseWord}";
                roll.flavorText  = string.Join(" ", descs);
                break;

            case ItemRarity.Legendary:
                roll.displayName = LegendaryName(baseWord);
                roll.flavorText  = LegendaryFlavor();
                break;
        }
    }

    static string BaseName(GenCategory c) => c switch
    {
        GenCategory.AxeOneHand     => "Axe",
        GenCategory.AxeTwoHand     => "Great Axe",
        GenCategory.Dagger         => "Dagger",
        GenCategory.MaceOneHand    => "Mace",
        GenCategory.MaceTwoHand    => "Great Mace",
        GenCategory.Ranged         => "Bow",
        GenCategory.Sceptre        => "Sceptre",
        GenCategory.Spear          => "Spear",
        GenCategory.Staff          => "Staff",
        GenCategory.SwordOneHand   => "Sword",
        GenCategory.SwordTwoHand   => "Great Sword",
        GenCategory.Thrown         => "Throwing Knife",
        GenCategory.Shield         => "Shield",
        GenCategory.ArmorBack      => "Cloak",
        GenCategory.ArmorChest     => "Breastplate",
        GenCategory.ArmorHands     => "Gauntlets",
        GenCategory.ArmorHead      => "Helm",
        GenCategory.ArmorHips      => "Belt",
        GenCategory.ArmorLegs      => "Greaves",
        GenCategory.ArmorShoulders => "Pauldrons",
        GenCategory.Ring           => "Ring",
        GenCategory.Necklace       => "Necklace",
        _                          => "Relic",
    };

    // ── Legendary naming ─────────────────────────────────────────────────────────

    static readonly string[] Adjectives =
        { "Obsidian", "Ashen", "Gilded", "Dread", "Hallowed", "Ancient", "Bloodforged",
          "Frostbitten", "Stormforged", "Sundered", "Ebon", "Radiant" };
    static readonly string[] Nouns =
        { "Menace", "Reaper", "Vanquisher", "Bane", "Sorrow", "Doom", "Whisper", "Wrath",
          "Sentinel", "Oathkeeper", "Ruin", "Herald" };
    static readonly string[] Themes =
        { "Souls", "Smiting", "the Fallen", "Embers", "Storms", "Twilight", "the Vigil",
          "Ruin", "the Ages", "the Abyss" };
    static readonly string[] Saints =
        { "Joseph", "Aldric", "Mirabel", "Cuthbert", "Edran", "Seraphine", "Tobias",
          "Rowan", "Lysandra", "Garrick" };
    // Display forms of NpcFaction (skipping None).
    static readonly string[] Factions =
        { "Aldenmoor", "Veranthos", "the Drask Confederacy", "Solvane", "the Ashfeld Remnants" };
    static readonly string[] PossessiveFactions = { "Aldenmoor", "Veranthos", "Solvane" };

    static string LegendaryName(string baseWord)
    {
        string Adj()  => Adjectives[Random.Range(0, Adjectives.Length)];
        string Noun() => Nouns[Random.Range(0, Nouns.Length)];
        string Thm()  => Themes[Random.Range(0, Themes.Length)];

        switch (Random.Range(0, 6))
        {
            case 0:  return $"{Adj()} {baseWord} of {Thm()}";
            case 1:  return $"{Noun()} of {Thm()}";
            case 2:  return $"The {Noun()} of {Factions[Random.Range(0, Factions.Length)]}";
            case 3:  return $"{PossessiveFactions[Random.Range(0, PossessiveFactions.Length)]}'s {Noun()}";
            case 4:  return $"Saint {Saints[Random.Range(0, Saints.Length)]}'s {baseWord} of {Thm()}";
            default: return $"{baseWord} of the {Noun()}";
        }
    }

    static readonly string[] LegendaryLines =
    {
        "Forged in a fire the world has forgotten, it hungers yet.",
        "Those who wielded it are remembered only in their screams.",
        "It remembers every life it has taken.",
        "Light bends around its edge, unwilling to touch it.",
        "A relic of an age that ended in ash and silence.",
        "Kings have killed for it. None kept it long.",
    };

    static string LegendaryFlavor() => LegendaryLines[Random.Range(0, LegendaryLines.Length)];
}
