using UnityEditor;
using UnityEngine;

// Tools > Valdris > Generate NPC Presets
// Creates 19 starter NpcDefinition assets in Assets/_Project/ScriptableObjects/NpcDefinitions/.
// Safe to run again — existing assets are skipped, not overwritten.
public static class NpcPresetGenerator
{
    const string OutputFolder = "Assets/_Project/ScriptableObjects/NpcDefinitions";

    [MenuItem("Tools/Valdris/Generate NPC Presets")]
    static void Generate()
    {
        EnsureFolder("Assets/_Project/ScriptableObjects");
        EnsureFolder(OutputFolder);

        int created = 0;

        // ── Civilian / Neutral ────────────────────────────────────────────────

        created += Make("Prisoner", d =>
        {
            d.displayName            = "Prisoner";
            d.faction                = NpcFaction.None;
            d.level                  = 1;
            d.maxHealth              = 30f;
            d.xpReward               = 5;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 1.2f;
            d.runSpeed               = 3f;
            d.idleSecondsMin         = 2f;
            d.idleSecondsMax         = 8f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Flee;
            d.reactToFaction         = FactionFilter.Everyone;
            d.fleeDuration           = 60f;
            d.isLootable             = false;
        });

        created += Make("Villager", d =>
        {
            d.displayName            = "Villager";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 1;
            d.maxHealth              = 40f;
            d.xpReward               = 5;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 1.2f;
            d.runSpeed               = 3.5f;
            d.idleSecondsMin         = 3f;
            d.idleSecondsMax         = 10f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Flee;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.fleeDuration           = 60f;
            d.isLootable             = false;
        });

        created += Make("Farmer", d =>
        {
            d.displayName            = "Farmer";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 1;
            d.maxHealth              = 35f;
            d.xpReward               = 5;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 1.1f;
            d.runSpeed               = 3f;
            d.idleSecondsMin         = 5f;
            d.idleSecondsMax         = 15f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Flee;
            d.reactToFaction         = FactionFilter.Everyone;
            d.fleeDuration           = 90f;
            d.isLootable             = false;
        });

        created += Make("Innkeeper", d =>
        {
            d.displayName            = "Innkeeper";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 2;
            d.maxHealth              = 50f;
            d.xpReward               = 5;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 1.0f;
            d.runSpeed               = 2.5f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.None;
            d.reactToFaction         = FactionFilter.None;
            d.isLootable             = false;
        });

        created += Make("Merchant", d =>
        {
            d.displayName            = "Merchant";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 2;
            d.maxHealth              = 50f;
            d.xpReward               = 5;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 1.0f;
            d.runSpeed               = 2.5f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Flee;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.fleeDuration           = 60f;
            d.goldMin                = 20;
            d.goldMax                = 80;
            d.isLootable             = true;
        });

        created += Make("Priest", d =>
        {
            d.displayName            = "Priest";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 3;
            d.maxHealth              = 60f;
            d.maxMana                = 80f;
            d.manaRegenRate          = 2f;
            d.xpReward               = 10;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 1.0f;
            d.runSpeed               = 2.5f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Spells, AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.None;
            d.reactToFaction         = FactionFilter.None;
            d.isLootable             = false;
        });

        created += Make("Noble", d =>
        {
            d.displayName            = "Noble";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 3;
            d.maxHealth              = 60f;
            d.xpReward               = 10;
            d.isEssential            = true;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 1.0f;
            d.runSpeed               = 3f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Flee;
            d.reactToFaction         = FactionFilter.Everyone;
            d.fleeDuration           = 60f;
            d.goldMin                = 50;
            d.goldMax                = 150;
            d.isLootable             = false;
        });

        created += Make("QuestGiver", d =>
        {
            d.displayName            = "Quest Giver";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 1;
            d.maxHealth              = 100f;
            d.xpReward               = 0;
            d.isEssential            = true;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 1.0f;
            d.runSpeed               = 2.5f;
            d.isAggressive           = false;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.None;
            d.reactToFaction         = FactionFilter.None;
            d.isLootable             = false;
        });

        // ── Military / Guard ──────────────────────────────────────────────────

        created += Make("VillageGuard", d =>
        {
            d.displayName            = "Village Guard";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 3;
            d.maxHealth              = 100f;
            d.baseArmor              = 5f;
            d.xpReward               = 20;
            d.behaviorType           = NpcBehaviorType.Patrol;
            d.walkSpeed              = 2.0f;
            d.runSpeed               = 5f;
            d.isAggressive           = false;
            d.aggroRange             = 8f;
            d.attackPriority         = new[] { AttackType.Melee, AttackType.Ranged };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.goldMin                = 5;
            d.goldMax                = 20;
            d.isLootable             = true;
        });

        created += Make("Soldier", d =>
        {
            d.displayName            = "Soldier";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 5;
            d.maxHealth              = 120f;
            d.baseArmor              = 8f;
            d.xpReward               = 25;
            d.behaviorType           = NpcBehaviorType.Patrol;
            d.walkSpeed              = 2.5f;
            d.runSpeed               = 5.5f;
            d.isAggressive           = false;
            d.aggroRange             = 10f;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.goldMin                = 10;
            d.goldMax                = 30;
            d.isLootable             = true;
        });

        created += Make("Knight", d =>
        {
            d.displayName            = "Knight";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 8;
            d.maxHealth              = 180f;
            d.baseArmor              = 15f;
            d.xpReward               = 50;
            d.behaviorType           = NpcBehaviorType.Patrol;
            d.walkSpeed              = 2.0f;
            d.runSpeed               = 4.5f;
            d.isAggressive           = false;
            d.aggroRange             = 10f;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.goldMin                = 20;
            d.goldMax                = 60;
            d.isLootable             = true;
        });

        created += Make("Archer", d =>
        {
            d.displayName            = "Archer";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 4;
            d.maxHealth              = 80f;
            d.baseArmor              = 3f;
            d.xpReward               = 20;
            d.behaviorType           = NpcBehaviorType.Patrol;
            d.walkSpeed              = 2.0f;
            d.runSpeed               = 5f;
            d.isAggressive           = false;
            d.aggroRange             = 15f;
            d.attackPriority         = new[] { AttackType.Ranged, AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.goldMin                = 5;
            d.goldMax                = 20;
            d.isLootable             = true;
        });

        created += Make("Mage", d =>
        {
            d.displayName            = "Mage";
            d.faction                = NpcFaction.Aldenmoor;
            d.level                  = 6;
            d.maxHealth              = 70f;
            d.maxMana                = 120f;
            d.manaRegenRate          = 3f;
            d.xpReward               = 30;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 1.5f;
            d.runSpeed               = 3.5f;
            d.isAggressive           = false;
            d.aggroRange             = 12f;
            d.attackPriority         = new[] { AttackType.Spells, AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Aldenmoor;
            d.goldMin                = 15;
            d.goldMax                = 40;
            d.isLootable             = true;
        });

        // ── Hostile ───────────────────────────────────────────────────────────

        created += Make("Bandit", d =>
        {
            d.displayName            = "Bandit";
            d.faction                = NpcFaction.DraskConfederacy;
            d.level                  = 3;
            d.maxHealth              = 70f;
            d.xpReward               = 20;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 2.0f;
            d.runSpeed               = 5f;
            d.isAggressive           = true;
            d.aggroRange             = 12f;
            d.attackPriority         = new[] { AttackType.Melee, AttackType.Ranged };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Everyone;
            d.goldMin                = 5;
            d.goldMax                = 30;
            d.isLootable             = true;
        });

        created += Make("Cultist", d =>
        {
            d.displayName            = "Cultist";
            d.faction                = NpcFaction.AshfeldRemnants;
            d.level                  = 4;
            d.maxHealth              = 60f;
            d.maxMana                = 80f;
            d.manaRegenRate          = 2f;
            d.xpReward               = 25;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 1.8f;
            d.runSpeed               = 4f;
            d.isAggressive           = true;
            d.aggroRange             = 10f;
            d.attackPriority         = new[] { AttackType.Spells, AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.AshfeldRemnants;
            d.goldMin                = 0;
            d.goldMax                = 15;
            d.isLootable             = true;
        });

        created += Make("Undead", d =>
        {
            d.displayName            = "Undead";
            d.faction                = NpcFaction.None;
            d.level                  = 3;
            d.maxHealth              = 80f;
            d.baseArmor              = 2f;
            d.xpReward               = 15;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 1.0f;
            d.runSpeed               = 2.5f;
            d.isAggressive           = true;
            d.aggroRange             = 8f;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Everyone;
            d.isLootable             = true;
        });

        created += Make("WildAnimal", d =>
        {
            d.displayName            = "Wild Animal";
            d.faction                = NpcFaction.None;
            d.level                  = 2;
            d.maxHealth              = 50f;
            d.xpReward               = 10;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 3.0f;
            d.runSpeed               = 7f;
            d.isAggressive           = true;
            d.aggroRange             = 10f;
            d.fleeHealthThreshold    = 0.2f;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Everyone;
            d.isLootable             = true;
        });

        created += Make("Monster", d =>
        {
            d.displayName            = "Monster";
            d.faction                = NpcFaction.None;
            d.level                  = 7;
            d.maxHealth              = 200f;
            d.baseArmor              = 5f;
            d.xpReward               = 50;
            d.behaviorType           = NpcBehaviorType.Wander;
            d.walkSpeed              = 2.0f;
            d.runSpeed               = 4.5f;
            d.isAggressive           = true;
            d.aggroRange             = 12f;
            d.attackPriority         = new[] { AttackType.Melee };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Everyone;
            d.goldMin                = 10;
            d.goldMax                = 50;
            d.isLootable             = true;
        });

        created += Make("Boss", d =>
        {
            d.displayName            = "Boss";
            d.faction                = NpcFaction.None;
            d.level                  = 15;
            d.maxHealth              = 500f;
            d.maxMana                = 200f;
            d.baseArmor              = 15f;
            d.manaRegenRate          = 5f;
            d.xpReward               = 200;
            d.isEssential            = true;
            d.behaviorType           = NpcBehaviorType.Stationary;
            d.walkSpeed              = 3.0f;
            d.runSpeed               = 6f;
            d.isAggressive           = true;
            d.aggroRange             = 20f;
            d.attackPriority         = new[] { AttackType.Spells, AttackType.Melee, AttackType.Ranged };
            d.witnessedKillReaction  = WitnessedKillReaction.Attack;
            d.reactToFaction         = FactionFilter.Everyone;
            d.goldMin                = 100;
            d.goldMax                = 300;
            d.isLootable             = true;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[NpcPresetGenerator] Done — {created} asset(s) created in {OutputFolder}.");
        EditorUtility.DisplayDialog("NPC Preset Generator",
            $"{created} preset(s) created.\n\nFind them in:\n{OutputFolder}", "OK");
    }

    // Returns 1 if the asset was created, 0 if it already existed (skipped).
    static int Make(string assetName, System.Action<NpcDefinition> configure)
    {
        string path = $"{OutputFolder}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<NpcDefinition>(path) != null) return 0;

        var asset = ScriptableObject.CreateInstance<NpcDefinition>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        return 1;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }
    }
}
