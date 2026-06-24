using UnityEngine;
using System;
using System.Collections.Generic;

// Ranked skills: each skill can be invested in multiple times (up to maxRank),
// one point per rank. A skill's effect = its per-rank modifiers × current rank,
// which keeps growth bounded (small per-rank, hard rank cap).
public class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }

    // skill → current rank (>0 means owned).
    private readonly Dictionary<SkillData, int> _ranks = new();

    public static event Action<SkillData> OnSkillUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int GetRank(SkillData skill) =>
        skill != null && _ranks.TryGetValue(skill, out int r) ? r : 0;

    public bool HasSkill(SkillData skill) => GetRank(skill) > 0;

    static int MaxRankOf(SkillData s) => Mathf.Max(1, s.maxRank);

    public bool IsMaxed(SkillData skill) => skill != null && GetRank(skill) >= MaxRankOf(skill);

    // Can add another rank: below max, prerequisites owned, and enough points.
    public bool CanUnlock(SkillData skill)
    {
        if (skill == null || IsMaxed(skill)) return false;
        if (LevelSystem.Instance == null || LevelSystem.Instance.UnspentSkillPoints < skill.pointCost) return false;
        if (skill.prerequisites != null)
            foreach (SkillData prereq in skill.prerequisites)
                if (prereq != null && GetRank(prereq) == 0) return false;
        return true;
    }

    // Spends a point and adds one rank.
    public bool UnlockSkill(SkillData skill)
    {
        if (!CanUnlock(skill)) return false;
        if (!LevelSystem.Instance.SpendSkillPoints(skill.pointCost)) return false;
        _ranks[skill] = GetRank(skill) + 1;
        OnSkillUnlocked?.Invoke(skill);
        return true;
    }

    // Force-grant a skill rank without spending points or checking prerequisites
    // (used by quest rewards, e.g. unlocking Detect Corruption).
    public void GrantSkill(SkillData skill, int ranks = 1)
    {
        if (skill == null) return;
        _ranks[skill] = Mathf.Min(MaxRankOf(skill), GetRank(skill) + Mathf.Max(1, ranks));
        OnSkillUnlocked?.Invoke(skill);
    }

    // Fresh start (New Game).
    public void ClearAll() => _ranks.Clear();

    // ---- Stat contributions (per-rank × rank) ----

    public float TotalFlat(StatType stat) => Total(stat, ModifierMode.Flat);
    public float TotalPercent(StatType stat) => Total(stat, ModifierMode.Percent);

    private float Total(StatType stat, ModifierMode mode)
    {
        float total = 0f;
        foreach (var kv in _ranks)
            foreach (StatModifier mod in kv.Key.modifiers)
                if (mod.stat == stat && mod.mode == mode) total += mod.amount * kv.Value;
        return total;
    }

    // Per-rank spell tweaks × rank, for one spell.
    public float SpellPercent(SpellData spell, Func<SpellModifier, float> selector)
    {
        float total = 0f;
        foreach (var kv in _ranks)
            foreach (SpellModifier mod in kv.Key.spellModifiers)
                if (mod.spell == spell) total += selector(mod) * kv.Value;
        return total;
    }

    // Total attack-damage % from weapon-mastery skills matching the given type.
    // PlayerStats applies this only for the currently equipped weapon's type.
    public float WeaponMasteryPercent(WeaponType type)
    {
        if (type == WeaponType.None) return 0f;
        float total = 0f;
        foreach (var kv in _ranks)
            if (kv.Key.masteryWeaponType == type && kv.Key.attackDamagePercentPerRank != 0f)
                total += kv.Key.attackDamagePercentPerRank * kv.Value;
        return total;
    }

    // ---- Special tier-2 effects (summed per-rank across owned skills) ----

    float SumPerRank(Func<SkillData, float> selector)
    {
        float total = 0f;
        foreach (var kv in _ranks) total += selector(kv.Key) * kv.Value;
        return total;
    }

    public float LifestealPercent()          => SumPerRank(s => s.lifestealPercentPerRank);
    public float ManaCostReductionPercent()  => Mathf.Min(90f, SumPerRank(s => s.manaCostReductionPercentPerRank));
    public float XpGainPercent()             => SumPerRank(s => s.xpGainPercentPerRank);
    public float GoldFindPercent()           => SumPerRank(s => s.goldFindPercentPerRank);
    public float LootRarityBonus()           => SumPerRank(s => s.lootRarityBonusPerRank);

    // ---- Save/load (encoded as "skillName:rank"; legacy "skillName" = rank 1) ----

    public List<string> CaptureState()
    {
        var list = new List<string>();
        foreach (var kv in _ranks) list.Add($"{kv.Key.name}:{kv.Value}");
        return list;
    }

    public void RestoreState(List<string> entries, GameDatabase database)
    {
        _ranks.Clear();
        if (entries == null) return;
        foreach (string entry in entries)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            int colon = entry.LastIndexOf(':');
            string name = colon >= 0 ? entry.Substring(0, colon) : entry;
            int rank = 1;
            if (colon >= 0) int.TryParse(entry.Substring(colon + 1), out rank);

            SkillData skill = database.FindSkill(name);
            if (skill != null) _ranks[skill] = Mathf.Max(1, rank);
            else Debug.LogWarning($"Saved skill not found: {name}");
        }
    }
}
