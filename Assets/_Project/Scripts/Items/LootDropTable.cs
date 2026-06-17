using System.Collections.Generic;
using UnityEngine;

// Drop behaviour for one source-rarity tier (a Common chest, a Legendary NPC, …).
// Guaranteed items roll at `floor` rarity; each bonus d100 roll may add one
// higher-rarity item on top. Floor rises with tier.
[System.Serializable]
public struct RarityTier
{
    public ItemRarity tier;             // which source tier this row configures
    [Min(0)] public int guaranteedMin;  // guaranteed item count (inclusive)
    [Min(0)] public int guaranteedMax;
    public ItemRarity floor;            // rarity the guaranteed items roll at
    [Min(0)] public int bonusRolls;     // extra d100 rolls for higher-rarity items
    [Range(0f, 100f)] public float uncommonPct;
    [Range(0f, 100f)] public float rarePct;
    [Range(0f, 100f)] public float epicPct;
    [Range(0f, 100f)] public float legendaryPct;
    [Min(0f)] public float goldMultiplier;
}

// Rolls the set of item rarities a loot source produces. Defaults are the
// designed progression; override via GameDatabase.lootTiers to retune in the
// Inspector without touching code.
public static class LootDropTable
{
    static readonly RarityTier[] Defaults =
    {
        new() { tier = ItemRarity.Common,    guaranteedMin = 1, guaranteedMax = 4, floor = ItemRarity.Common,   bonusRolls = 1, uncommonPct = 10f, rarePct = 3f,  epicPct = 2f,  legendaryPct = 1f,   goldMultiplier = 1.0f },
        new() { tier = ItemRarity.Uncommon,  guaranteedMin = 1, guaranteedMax = 4, floor = ItemRarity.Common,   bonusRolls = 1, uncommonPct = 16f, rarePct = 6f,  epicPct = 3f,  legendaryPct = 1.5f, goldMultiplier = 1.25f },
        new() { tier = ItemRarity.Rare,      guaranteedMin = 2, guaranteedMax = 4, floor = ItemRarity.Uncommon, bonusRolls = 1, uncommonPct = 24f, rarePct = 12f, epicPct = 5f,  legendaryPct = 2f,   goldMultiplier = 1.5f },
        new() { tier = ItemRarity.Epic,      guaranteedMin = 2, guaranteedMax = 5, floor = ItemRarity.Uncommon, bonusRolls = 4, uncommonPct = 30f, rarePct = 18f, epicPct = 9f,  legendaryPct = 4f,   goldMultiplier = 1.75f },
        new() { tier = ItemRarity.Legendary, guaranteedMin = 3, guaranteedMax = 5, floor = ItemRarity.Rare,     bonusRolls = 5, uncommonPct = 36f, rarePct = 25f, epicPct = 15f, legendaryPct = 8f,   goldMultiplier = 2.0f },
    };

    public static RarityTier For(ItemRarity sourceTier)
    {
        // Prefer an Inspector-authored override if GameDatabase supplies one.
        var overrides = SaveSystem.Instance?.database?.lootTiers;
        if (overrides != null)
            foreach (var t in overrides)
                if (t.tier == sourceTier && t.goldMultiplier > 0f) return t;

        int i = Mathf.Clamp((int)sourceTier, 0, Defaults.Length - 1);
        return Defaults[i];
    }

    // The list of item rarities to generate for a source of the given tier.
    public static List<ItemRarity> Roll(ItemRarity sourceTier)
    {
        var t = For(sourceTier);
        var result = new List<ItemRarity>();

        int guaranteed = t.guaranteedMax > t.guaranteedMin
            ? Random.Range(t.guaranteedMin, t.guaranteedMax + 1)
            : t.guaranteedMin;
        for (int i = 0; i < guaranteed; i++) result.Add(t.floor);

        // Each bonus roll: a d100 mapped from the top of the range downward.
        float legCut  = 100f - t.legendaryPct;
        float epicCut = legCut  - t.epicPct;
        float rareCut = epicCut - t.rarePct;
        float uncCut  = rareCut - t.uncommonPct;
        for (int i = 0; i < t.bonusRolls; i++)
        {
            float d = Random.Range(1, 101); // 1..100 inclusive
            if      (d > legCut)  result.Add(ItemRarity.Legendary);
            else if (d > epicCut) result.Add(ItemRarity.Epic);
            else if (d > rareCut) result.Add(ItemRarity.Rare);
            else if (d > uncCut)  result.Add(ItemRarity.Uncommon);
            // else: no bonus item from this roll
        }
        return result;
    }

    public static float GoldMultiplier(ItemRarity sourceTier) => For(sourceTier).goldMultiplier;
}
