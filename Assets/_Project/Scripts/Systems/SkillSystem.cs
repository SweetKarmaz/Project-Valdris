using UnityEngine;
using System;
using System.Collections.Generic;

public class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }
    private readonly HashSet<SkillData> _unlockedSkills = new();

    public static event Action<SkillData> OnSkillUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // A skill can be unlocked when it isn't already, all prerequisites are
    // met, and the player has enough unspent skill points.
    public bool CanUnlock(SkillData skill)
    {
        if (skill == null || _unlockedSkills.Contains(skill)) return false;
        if (LevelSystem.Instance == null || LevelSystem.Instance.UnspentSkillPoints < skill.pointCost) return false;
        if (skill.prerequisites != null)
            foreach (SkillData prereq in skill.prerequisites)
                if (prereq != null && !_unlockedSkills.Contains(prereq)) return false;
        return true;
    }

    public bool UnlockSkill(SkillData skill)
    {
        if (!CanUnlock(skill)) return false;
        if (!LevelSystem.Instance.SpendSkillPoints(skill.pointCost)) return false;
        _unlockedSkills.Add(skill);
        OnSkillUnlocked?.Invoke(skill);
        return true;
    }

    public bool HasSkill(SkillData skill) => _unlockedSkills.Contains(skill);

    // ---- Stat contributions from unlocked skills ----

    public float TotalFlat(StatType stat) => Total(stat, ModifierMode.Flat);
    public float TotalPercent(StatType stat) => Total(stat, ModifierMode.Percent);

    private float Total(StatType stat, ModifierMode mode)
    {
        float total = 0f;
        foreach (SkillData skill in _unlockedSkills)
            foreach (StatModifier mod in skill.modifiers)
                if (mod.stat == stat && mod.mode == mode) total += mod.amount;
        return total;
    }

    // Net percent change to one specific spell from all unlocked skills.
    public float SpellPercent(SpellData spell, Func<SpellModifier, float> selector)
    {
        float total = 0f;
        foreach (SkillData skill in _unlockedSkills)
            foreach (SpellModifier mod in skill.spellModifiers)
                if (mod.spell == spell) total += selector(mod);
        return total;
    }

    // ---- Save/load ----

    public List<string> CaptureState()
    {
        var names = new List<string>();
        foreach (SkillData skill in _unlockedSkills) names.Add(skill.name);
        return names;
    }

    public void RestoreState(List<string> skillNames, GameDatabase database)
    {
        _unlockedSkills.Clear();
        if (skillNames == null) return;
        foreach (string skillName in skillNames)
        {
            SkillData skill = database.FindSkill(skillName);
            if (skill != null) _unlockedSkills.Add(skill);
            else Debug.LogWarning($"Saved skill not found: {skillName}");
        }
    }
}

