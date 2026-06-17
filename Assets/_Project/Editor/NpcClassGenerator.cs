using UnityEditor;
using UnityEngine;

// Tools > Valdris > Generate NPC Appearance Classes
//
// Creates one NpcAppearanceClass ScriptableObject per class under
// Assets/_Project/ScriptableObjects/NpcClasses/.
//
// Slot index ranges are set based on general knowledge of the Synty
// PolygonFantasyHeroCharacters pack layout:
//   Index 0      = bare skin / undergarment (no armour)
//   Low indices  = light civilian clothing
//   Mid indices  = light-to-medium armour
//   High indices = heavy armour / elaborate gear
//
// IMPORTANT: After running this tool, open the Synty demo scene
// (Scenes/Demo_RandomCharacter) and use the randomizer to preview
// which slot indices correspond to which visual styles. Adjust the
// min/max values in each generated class to taste.
//
// Safe to re-run: existing assets are skipped.
public static class NpcClassGenerator
{
    const string OutFolder = "Assets/_Project/ScriptableObjects/NpcClasses";

    [MenuItem("Tools/Valdris/Generate NPC Appearance Classes")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(OutFolder))
        {
            var parts = OutFolder.Split('/');
            string built = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{built}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(built, parts[i]);
                built = next;
            }
        }

        int created = 0, skipped = 0;

        void Make(string fileName, System.Action<NpcAppearanceClass> configure)
        {
            string path = $"{OutFolder}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<NpcAppearanceClass>(path) != null) { skipped++; return; }
            var asset = ScriptableObject.CreateInstance<NpcAppearanceClass>();
            configure(asset);
            AssetDatabase.CreateAsset(asset, path);
            created++;
        }

        // ── Civilian / No Armour ──────────────────────────────────────────────

        Make("NpcClass_Peasant", c => {
            c.className   = "Peasant";
            c.description = "Common folk. Plain clothing, no armour.";
            c.torso       = new SlotRange(1, 6);
            c.armUpper    = new SlotRange(0, 4);
            c.armLower    = new SlotRange(0, 4);
            c.hands       = new SlotRange(0, 3);
            c.hips        = new SlotRange(1, 5);
            c.legs        = new SlotRange(0, 5);
            c.shoulderAttach        = new SlotRange(0, 0); // no pauldrons
            c.allowChestAttachment  = false;
            c.allowBackAttachment   = false;
            c.allowHipsAttachment   = true;
            c.hipsAttach            = new SlotRange(0, 4);
            c.facialHair            = FacialHairChance.Sometimes;
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
        });

        Make("NpcClass_Farmer", c => {
            c.className   = "Farmer";
            c.description = "Rural worker. Practical work clothes.";
            c.torso       = new SlotRange(1, 5);
            c.armUpper    = new SlotRange(0, 3);
            c.armLower    = new SlotRange(0, 3);
            c.hands       = new SlotRange(0, 2);
            c.hips        = new SlotRange(1, 5);
            c.legs        = new SlotRange(0, 4);
            c.shoulderAttach        = new SlotRange(0, 0);
            c.allowChestAttachment  = false;
            c.allowBackAttachment   = false;
            c.allowHipsAttachment   = true;
            c.hipsAttach            = new SlotRange(0, 3);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        Make("NpcClass_Beggar", c => {
            c.className   = "Beggar";
            c.description = "Destitute. Ragged minimal clothing.";
            c.torso       = new SlotRange(1, 3);
            c.armUpper    = new SlotRange(0, 2);
            c.armLower    = new SlotRange(0, 2);
            c.hands       = new SlotRange(0, 1);
            c.hips        = new SlotRange(1, 3);
            c.legs        = new SlotRange(0, 3);
            c.shoulderAttach        = new SlotRange(0, 0);
            c.allowChestAttachment  = false;
            c.allowBackAttachment   = false;
            c.allowHipsAttachment   = false;
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.facialHair            = FacialHairChance.Always;
        });

        Make("NpcClass_Merchant", c => {
            c.className   = "Merchant";
            c.description = "Travelling trader. Sturdy clothing, no weapons.";
            c.gender      = GenderPreference.Random;
            c.torso       = new SlotRange(3, 8);
            c.armUpper    = new SlotRange(2, 6);
            c.armLower    = new SlotRange(2, 6);
            c.hands       = new SlotRange(1, 4);
            c.hips        = new SlotRange(2, 6);
            c.legs        = new SlotRange(2, 6);
            c.shoulderAttach        = new SlotRange(0, 0);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 4);
            c.allowBackAttachment   = false;
            c.allowHipsAttachment   = true;
            c.hipsAttach            = new SlotRange(2, 6);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
        });

        Make("NpcClass_Innkeeper", c => {
            c.className   = "Innkeeper";
            c.description = "Tavern owner. Practical indoor clothing, slightly prosperous look.";
            c.torso       = new SlotRange(3, 8);
            c.armUpper    = new SlotRange(2, 5);
            c.armLower    = new SlotRange(2, 5);
            c.hands       = new SlotRange(1, 3);
            c.hips        = new SlotRange(2, 6);
            c.legs        = new SlotRange(2, 6);
            c.shoulderAttach        = new SlotRange(0, 0);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 5);
            c.allowHipsAttachment   = true;
            c.hipsAttach            = new SlotRange(0, 4);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
        });

        Make("NpcClass_Scholar", c => {
            c.className   = "Scholar";
            c.description = "Academic. Robes and bookish attire.";
            c.torso       = new SlotRange(4, 10);
            c.armUpper    = new SlotRange(3, 7);
            c.armLower    = new SlotRange(3, 7);
            c.hands       = new SlotRange(2, 5);
            c.hips        = new SlotRange(3, 7);
            c.legs        = new SlotRange(3, 7);
            c.shoulderAttach        = new SlotRange(0, 0);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 5);
            c.allowBackAttachment   = false;
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        Make("NpcClass_Bard", c => {
            c.className   = "Bard";
            c.description = "Performer. Colourful, flamboyant clothing.";
            c.torso       = new SlotRange(4, 10);
            c.armUpper    = new SlotRange(3, 8);
            c.armLower    = new SlotRange(3, 8);
            c.hands       = new SlotRange(2, 5);
            c.hips        = new SlotRange(3, 7);
            c.legs        = new SlotRange(3, 7);
            c.shoulderAttach        = new SlotRange(0, 2);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 6);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 4);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.colourVariant         = ColourVariant.Random;
        });

        Make("NpcClass_Blacksmith", c => {
            c.className   = "Blacksmith";
            c.description = "Armourer. Heavy work clothes, leather apron.";
            c.torso       = new SlotRange(4, 9);
            c.armUpper    = new SlotRange(3, 7);
            c.armLower    = new SlotRange(3, 7);
            c.hands       = new SlotRange(3, 6);
            c.hips        = new SlotRange(3, 7);
            c.legs        = new SlotRange(3, 7);
            c.shoulderAttach        = new SlotRange(0, 3);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 5);
            c.allowHipsAttachment   = true;
            c.hipsAttach            = new SlotRange(0, 4);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        // ── Spellcasters / Robes ──────────────────────────────────────────────

        Make("NpcClass_Mage", c => {
            c.className   = "Mage";
            c.description = "Arcane spellcaster. Robes, no heavy armour.";
            c.torso       = new SlotRange(5, 12);
            c.armUpper    = new SlotRange(4, 9);
            c.armLower    = new SlotRange(4, 9);
            c.hands       = new SlotRange(3, 7);
            c.hips        = new SlotRange(4, 9);
            c.legs        = new SlotRange(4, 9);
            c.shoulderAttach        = new SlotRange(0, 3);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 6);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 5);
            c.headCoveringStyle     = HeadCoveringStyle.Random;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        Make("NpcClass_Priest", c => {
            c.className   = "Priest";
            c.description = "Cleric. Religious robes, holy symbols.";
            c.torso       = new SlotRange(5, 11);
            c.armUpper    = new SlotRange(4, 8);
            c.armLower    = new SlotRange(4, 8);
            c.hands       = new SlotRange(3, 6);
            c.hips        = new SlotRange(4, 8);
            c.legs        = new SlotRange(4, 8);
            c.shoulderAttach        = new SlotRange(0, 2);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 7);
            c.allowBackAttachment   = false;
            c.headCoveringStyle     = HeadCoveringStyle.Random;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        Make("NpcClass_Cultist", c => {
            c.className   = "Cultist";
            c.description = "Dark worshipper. Hooded robes, sinister accessories.";
            c.torso       = new SlotRange(5, 12);
            c.armUpper    = new SlotRange(4, 9);
            c.armLower    = new SlotRange(4, 9);
            c.hands       = new SlotRange(3, 7);
            c.hips        = new SlotRange(4, 9);
            c.legs        = new SlotRange(4, 9);
            c.shoulderAttach        = new SlotRange(0, 4);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 6);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 6);
            c.headCoveringStyle     = HeadCoveringStyle.NoFacialHair;
            c.facialHair            = FacialHairChance.Never;
        });

        Make("NpcClass_Shaman", c => {
            c.className   = "Shaman";
            c.description = "Tribal spirit-caller. Primitive robes and bone decoration.";
            c.torso       = new SlotRange(3, 9);
            c.armUpper    = new SlotRange(2, 7);
            c.armLower    = new SlotRange(2, 7);
            c.hands       = new SlotRange(2, 6);
            c.hips        = new SlotRange(3, 8);
            c.legs        = new SlotRange(3, 8);
            c.shoulderAttach        = new SlotRange(0, 5);
            c.allowChestAttachment  = true;
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 7);
            c.headCoveringStyle     = HeadCoveringStyle.Random;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        // ── Light Armour ──────────────────────────────────────────────────────

        Make("NpcClass_Archer", c => {
            c.className   = "Archer";
            c.description = "Ranged fighter. Light leather armour, unencumbered.";
            c.torso       = new SlotRange(6, 12);
            c.armUpper    = new SlotRange(4, 8);
            c.armLower    = new SlotRange(4, 8);
            c.hands       = new SlotRange(3, 6);
            c.hips        = new SlotRange(5, 9);
            c.legs        = new SlotRange(5, 9);
            c.shoulderAttach        = new SlotRange(0, 4);
            c.elbowAttach           = new SlotRange(0, 3);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 5);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 5);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
        });

        Make("NpcClass_Thief", c => {
            c.className   = "Thief";
            c.description = "Cutpurse. Light, dark clothing for concealment.";
            c.torso       = new SlotRange(5, 10);
            c.armUpper    = new SlotRange(3, 7);
            c.armLower    = new SlotRange(3, 7);
            c.hands       = new SlotRange(3, 6);
            c.hips        = new SlotRange(4, 8);
            c.legs        = new SlotRange(4, 8);
            c.shoulderAttach        = new SlotRange(0, 2);
            c.allowChestAttachment  = true;
            c.allowBackAttachment   = false;
            c.allowHipsAttachment   = true;
            c.hipsAttach            = new SlotRange(3, 7);
            c.headCoveringStyle     = HeadCoveringStyle.NoFacialHair;
            c.facialHair            = FacialHairChance.Never;
        });

        Make("NpcClass_Assassin", c => {
            c.className   = "Assassin";
            c.description = "Silent killer. Form-fitting dark armour, hooded.";
            c.torso       = new SlotRange(7, 13);
            c.armUpper    = new SlotRange(5, 10);
            c.armLower    = new SlotRange(5, 10);
            c.hands       = new SlotRange(4, 8);
            c.hips        = new SlotRange(6, 10);
            c.legs        = new SlotRange(6, 10);
            c.shoulderAttach        = new SlotRange(0, 3);
            c.elbowAttach           = new SlotRange(0, 4);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 5);
            c.headCoveringStyle     = HeadCoveringStyle.NoHair;
            c.facialHair            = FacialHairChance.Never;
        });

        Make("NpcClass_TownGuard", c => {
            c.className   = "Town Guard";
            c.description = "Village militia. Light armour, basic weapons.";
            c.torso       = new SlotRange(7, 12);
            c.armUpper    = new SlotRange(5, 9);
            c.armLower    = new SlotRange(5, 9);
            c.hands       = new SlotRange(4, 7);
            c.hips        = new SlotRange(6, 10);
            c.legs        = new SlotRange(6, 10);
            c.shoulderAttach        = new SlotRange(2, 7);
            c.elbowAttach           = new SlotRange(0, 4);
            c.kneeAttach            = new SlotRange(0, 4);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 6);
            c.headCoveringStyle     = HeadCoveringStyle.Random;
        });

        // ── Medium Armour ─────────────────────────────────────────────────────

        Make("NpcClass_Soldier", c => {
            c.className   = "Soldier";
            c.description = "Professional fighter. Medium armour, disciplined look.";
            c.torso       = new SlotRange(9, 15);
            c.armUpper    = new SlotRange(7, 12);
            c.armLower    = new SlotRange(7, 12);
            c.hands       = new SlotRange(5, 9);
            c.hips        = new SlotRange(8, 12);
            c.legs        = new SlotRange(8, 12);
            c.shoulderAttach        = new SlotRange(5, 11);
            c.elbowAttach           = new SlotRange(2, 5);
            c.kneeAttach            = new SlotRange(2, 6);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(3, 8);
            c.allowBackAttachment   = false;
            c.headCoveringStyle     = HeadCoveringStyle.Random;
        });

        Make("NpcClass_Bandit", c => {
            c.className   = "Bandit";
            c.description = "Outlaw. Mixed scavenged armour, rough look.";
            c.torso       = new SlotRange(5, 14);
            c.armUpper    = new SlotRange(3, 11);
            c.armLower    = new SlotRange(3, 11);
            c.hands       = new SlotRange(2, 8);
            c.hips        = new SlotRange(4, 11);
            c.legs        = new SlotRange(4, 11);
            c.shoulderAttach        = new SlotRange(0, 10);
            c.shoulderMismatchChance = 40; // bandits often wear mismatched gear
            c.elbowAttach           = new SlotRange(0, 5);
            c.elbowMismatchChance   = 35;
            c.allowChestAttachment  = true;
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 6);
            c.headCoveringStyle     = HeadCoveringStyle.Random;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        Make("NpcClass_Ranger", c => {
            c.className   = "Ranger";
            c.description = "Wilderness scout. Medium leather and hide armour.";
            c.torso       = new SlotRange(7, 13);
            c.armUpper    = new SlotRange(5, 10);
            c.armLower    = new SlotRange(5, 10);
            c.hands       = new SlotRange(4, 8);
            c.hips        = new SlotRange(6, 11);
            c.legs        = new SlotRange(6, 11);
            c.shoulderAttach        = new SlotRange(2, 8);
            c.elbowAttach           = new SlotRange(0, 4);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(2, 7);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 6);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
        });

        Make("NpcClass_CityGuard", c => {
            c.className   = "City Guard";
            c.description = "Urban law enforcement. Mix of medium and light armour, more uniform.";
            c.torso       = new SlotRange(9, 14);
            c.armUpper    = new SlotRange(6, 11);
            c.armLower    = new SlotRange(6, 11);
            c.hands       = new SlotRange(5, 9);
            c.hips        = new SlotRange(7, 11);
            c.legs        = new SlotRange(7, 11);
            c.shoulderAttach        = new SlotRange(6, 12);
            c.shoulderMismatchChance = 5;
            c.elbowAttach           = new SlotRange(2, 6);
            c.kneeAttach            = new SlotRange(2, 6);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(4, 9);
            c.allowBackAttachment   = false;
            c.headCoveringStyle     = HeadCoveringStyle.NoFacialHair;
        });

        Make("NpcClass_Barbarian", c => {
            c.className   = "Barbarian";
            c.description = "Tribal warrior. Minimal armour, fur and hide, very muscular look.";
            c.torso       = new SlotRange(2, 7);
            c.armUpper    = new SlotRange(1, 5);
            c.armLower    = new SlotRange(1, 5);
            c.hands       = new SlotRange(1, 4);
            c.hips        = new SlotRange(2, 6);
            c.legs        = new SlotRange(2, 6);
            c.shoulderAttach        = new SlotRange(0, 6);
            c.shoulderMismatchChance = 50;
            c.elbowAttach           = new SlotRange(0, 4);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(0, 4);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(0, 5);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.facialHair            = FacialHairChance.Always;
        });

        // ── Heavy Armour ──────────────────────────────────────────────────────

        Make("NpcClass_Knight", c => {
            c.className   = "Knight";
            c.description = "Elite mounted warrior. Full plate armour.";
            c.torso       = new SlotRange(12, -1);
            c.armUpper    = new SlotRange(10, -1);
            c.armLower    = new SlotRange(10, -1);
            c.hands       = new SlotRange(8, -1);
            c.hips        = new SlotRange(10, -1);
            c.legs        = new SlotRange(10, -1);
            c.shoulderAttach        = new SlotRange(10, -1);
            c.shoulderMismatchChance = 5;
            c.elbowAttach           = new SlotRange(4, -1);
            c.kneeAttach            = new SlotRange(5, -1);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(5, -1);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(5, -1);
            c.headCoveringStyle     = HeadCoveringStyle.NoHair;
            c.facialHair            = FacialHairChance.Never;
        });

        Make("NpcClass_Paladin", c => {
            c.className   = "Paladin";
            c.description = "Holy warrior. Heavy armour with religious iconography.";
            c.torso       = new SlotRange(12, -1);
            c.armUpper    = new SlotRange(10, -1);
            c.armLower    = new SlotRange(10, -1);
            c.hands       = new SlotRange(8, -1);
            c.hips        = new SlotRange(10, -1);
            c.legs        = new SlotRange(10, -1);
            c.shoulderAttach        = new SlotRange(11, -1);
            c.shoulderMismatchChance = 0;
            c.elbowAttach           = new SlotRange(4, -1);
            c.kneeAttach            = new SlotRange(5, -1);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(6, -1);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(6, -1);
            c.headCoveringStyle     = HeadCoveringStyle.NoHair;
            c.facialHair            = FacialHairChance.Never;
        });

        // ── Royalty / Nobility ────────────────────────────────────────────────

        Make("NpcClass_King", c => {
            c.className   = "King";
            c.description = "Sovereign ruler. Ornate robes or armour with crown.";
            c.gender      = GenderPreference.Male;
            c.torso       = new SlotRange(10, -1);
            c.armUpper    = new SlotRange(8, -1);
            c.armLower    = new SlotRange(8, -1);
            c.hands       = new SlotRange(6, -1);
            c.hips        = new SlotRange(9, -1);
            c.legs        = new SlotRange(9, -1);
            c.shoulderAttach        = new SlotRange(9, -1);
            c.shoulderMismatchChance = 0;
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(6, -1);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(8, -1);
            c.headCoveringStyle     = HeadCoveringStyle.NoFacialHair;
            c.facialHair            = FacialHairChance.Sometimes;
            c.textureSet            = 1;
            c.colourVariant         = ColourVariant.Random;
        });

        Make("NpcClass_Queen", c => {
            c.className   = "Queen";
            c.description = "Sovereign ruler. Ornate robes with crown.";
            c.gender      = GenderPreference.Female;
            c.torso       = new SlotRange(10, -1);
            c.armUpper    = new SlotRange(8, -1);
            c.armLower    = new SlotRange(8, -1);
            c.hands       = new SlotRange(6, -1);
            c.hips        = new SlotRange(9, -1);
            c.legs        = new SlotRange(9, -1);
            c.shoulderAttach        = new SlotRange(9, -1);
            c.shoulderMismatchChance = 0;
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(6, -1);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(8, -1);
            c.headCoveringStyle     = HeadCoveringStyle.NoFacialHair;
            c.textureSet            = 1;
            c.colourVariant         = ColourVariant.Random;
        });

        Make("NpcClass_Noble", c => {
            c.className   = "Noble";
            c.description = "Wealthy lord or lady. Fine clothing, no armour.";
            c.torso       = new SlotRange(9, -1);
            c.armUpper    = new SlotRange(7, -1);
            c.armLower    = new SlotRange(7, -1);
            c.hands       = new SlotRange(5, -1);
            c.hips        = new SlotRange(8, -1);
            c.legs        = new SlotRange(8, -1);
            c.shoulderAttach        = new SlotRange(7, -1);
            c.allowChestAttachment  = true;
            c.chestAttach           = new SlotRange(5, -1);
            c.allowBackAttachment   = true;
            c.backAttach            = new SlotRange(6, -1);
            c.headCoveringStyle     = HeadCoveringStyle.BaseHair;
            c.facialHair            = FacialHairChance.Sometimes;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("NPC Class Generator",
            $"Done.\n\nCreated: {created}\nSkipped (already exist): {skipped}", "OK");
    }
}
