using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Valdris > Build Default Skills
//
// Creates the ranked skill assets (Vitals / Combat / Resistances / Weapon
// Mastery / Special) in Assets/_Project/ScriptableObjects/Skills with the agreed
// per-rank values and caps. Existing assets are left untouched, so it's safe to
// re-run after hand-tweaking — only missing skills are created.
public static class SkillTreeBuilder
{
    const string Dir = "Assets/_Project/ScriptableObjects/Skills";

    [MenuItem("Tools/Valdris/Build Default Skills")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Skills");

        int made = 0;

        // ── Vitals ──
        made += Stat("Skill_Toughness",  "Toughness",  "Increases maximum Health.",        25, StatType.MaxHealth, ModifierMode.Percent, 2f);
        made += Stat("Skill_Vitality",   "Vitality",   "Increases Health regeneration.",   20, StatType.HealthRegen, ModifierMode.Flat,  0.1f);
        made += Stat("Skill_Meditation", "Meditation", "Increases Mana regeneration.",     20, StatType.ManaRegen,  ModifierMode.Flat,   0.1f);

        // ── Combat ──
        made += Stat("Skill_Power",     "Power",     "Increases Attack Damage.",       30, StatType.AttackDamage,       ModifierMode.Percent, 1f);
        made += Stat("Skill_Swiftness", "Swiftness", "Increases Attack Speed.",        20, StatType.AttackSpeed,        ModifierMode.Percent, 1f);
        made += Stat("Skill_Bulwark",   "Bulwark",   "Increases Defense.",             25, StatType.Defense,            ModifierMode.Percent, 1f);
        made += Stat("Skill_Precision", "Precision", "Increases Physical Crit chance.",20, StatType.PhysicalCritChance, ModifierMode.Flat,    0.5f);
        made += Stat("Skill_Focus",     "Focus",     "Increases Spell Crit chance.",   20, StatType.SpellCritChance,    ModifierMode.Flat,    0.5f);
        made += Stat("Skill_Wrath",     "Wrath",     "Increases Critical Damage.",     25, StatType.CritDamage,         ModifierMode.Flat,    2f);

        // ── Resistances (flat % points; PlayerStats caps each type at 75%) ──
        made += Stat("Skill_FireWard",       "Fire Ward",       "Increases Fire resistance.",       20, StatType.FireResist,       ModifierMode.Flat, 1f);
        made += Stat("Skill_FrostWard",      "Frost Ward",      "Increases Ice resistance.",        20, StatType.IceResist,        ModifierMode.Flat, 1f);
        made += Stat("Skill_StormWard",      "Storm Ward",      "Increases Lightning resistance.",  20, StatType.LightningResist,  ModifierMode.Flat, 1f);
        made += Stat("Skill_HallowWard",     "Hallowed Ward",   "Increases Holy resistance.",       20, StatType.HolyResist,       ModifierMode.Flat, 1f);
        made += Stat("Skill_CorruptionWard", "Corruption Ward", "Increases Corruption resistance.", 20, StatType.CorruptionResist, ModifierMode.Flat, 1f);

        // ── Weapon Mastery (+1% damage/rank while that type is equipped) ──
        made += Mastery("Skill_SwordMastery",    "Sword Mastery",    WeaponType.Sword);
        made += Mastery("Skill_AxeMastery",      "Axe Mastery",      WeaponType.Axe);
        made += Mastery("Skill_MaceMastery",     "Mace Mastery",     WeaponType.Mace);
        made += Mastery("Skill_DaggerMastery",   "Dagger Mastery",   WeaponType.Dagger);
        made += Mastery("Skill_PolearmMastery",  "Polearm Mastery",  WeaponType.Polearm);
        made += Mastery("Skill_StaffMastery",    "Staff Mastery",    WeaponType.Staff);
        made += Mastery("Skill_BowMastery",      "Bow Mastery",      WeaponType.Bow);
        made += Mastery("Skill_CrossbowMastery", "Crossbow Mastery", WeaponType.Crossbow);
        made += Mastery("Skill_ThrownMastery",   "Thrown Mastery",   WeaponType.Thrown);
        made += Mastery("Skill_Unarmed",         "Martial Arts",     WeaponType.Unarmed);

        // ── Special (tier-2) ──
        made += Special("Skill_Bloodthirst",   "Bloodthirst",    "Heal a share of damage dealt.",   15, s => s.lifestealPercentPerRank          = 1f);
        made += Special("Skill_Efficiency",    "Efficiency",     "Spells cost less mana.",          20, s => s.manaCostReductionPercentPerRank  = 2f);
        made += Special("Skill_QuickStudy",    "Quick Study",    "Gain more XP.",                   10, s => s.xpGainPercentPerRank             = 5f);
        made += Special("Skill_Prospector",    "Prospector",     "Find more gold.",                 10, s => s.goldFindPercentPerRank           = 10f);
        made += Special("Skill_TreasureHunter", "Treasure Hunter","Find loot more often, and rarer.",20, s => s.lootRarityBonusPerRank          = 5f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int registered = RegisterInDatabase();

        EditorUtility.DisplayDialog("Build Default Skills",
            $"Created {made} new skill asset(s) in {Dir}.\n" +
            $"Registered {registered} skill(s) into GameDatabase.skills.\n\n" +
            "Existing skills were left untouched.", "OK");
    }

    // Adds every SkillData under Dir that isn't already in GameDatabase.skills.
    static int RegisterInDatabase()
    {
        var dbGuids = AssetDatabase.FindAssets("t:GameDatabase");
        if (dbGuids.Length == 0) return 0;
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));
        if (db == null) return 0;

        var so   = new SerializedObject(db);
        var prop = so.FindProperty("skills");

        // Collect existing references to avoid duplicates.
        var existing = new HashSet<Object>();
        for (int i = 0; i < prop.arraySize; i++)
            existing.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { Dir }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null || existing.Contains(skill)) continue;
            prop.arraySize++;
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = skill;
            existing.Add(skill);
            added++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        return added;
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    static int Stat(string asset, string name, string desc, int maxRank,
                    StatType stat, ModifierMode mode, float perRank)
    {
        var s = New(asset, name, desc, maxRank);
        if (s == null) return 0;
        s.modifiers = new List<StatModifier> { new StatModifier { stat = stat, mode = mode, amount = perRank } };
        Save(s, asset);
        return 1;
    }

    static int Mastery(string asset, string name, WeaponType type)
    {
        var s = New(asset, name, $"Increases damage with {type} weapons while equipped.", 25);
        if (s == null) return 0;
        s.masteryWeaponType = type;
        s.attackDamagePercentPerRank = 1f;
        Save(s, asset);
        return 1;
    }

    static int Special(string asset, string name, string desc, int maxRank, System.Action<SkillData> set)
    {
        var s = New(asset, name, desc, maxRank);
        if (s == null) return 0;
        set(s);
        Save(s, asset);
        return 1;
    }

    // Returns a new instance, or null if the asset already exists (skip).
    static SkillData New(string asset, string name, string desc, int maxRank)
    {
        string path = $"{Dir}/{asset}.asset";
        if (AssetDatabase.LoadAssetAtPath<SkillData>(path) != null) return null;
        var s = ScriptableObject.CreateInstance<SkillData>();
        s.skillName = name;
        s.description = desc;
        s.pointCost = 1;
        s.maxRank = maxRank;
        return s;
    }

    static void Save(SkillData s, string asset)
    {
        AssetDatabase.CreateAsset(s, $"{Dir}/{asset}.asset");
        EditorUtility.SetDirty(s);
    }
}
