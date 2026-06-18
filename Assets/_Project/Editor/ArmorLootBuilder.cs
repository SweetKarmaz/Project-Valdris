// Tools → Valdris → Build Armor Loot Prefabs
//
// Creates one loot prefab per Synty Static part in the following subfolders:
//   Assets/_Project/Prefabs/Loot/Armor/{Chest,Hands,Legs,Shoulders,Hips,Head,Back}
//
// Each prefab:
//   • Uses the Synty Static prefab as the root (carries all mesh/material data).
//   • Gets a LootItem component with itemType=Armor, equipSlot, and meshOverrides.
//   • Is safe to re-run — existing prefabs are skipped unless you hold Shift.
//
// Index conventions
//   0-indexed groups (Torso, Hands, Legs, Hips): child 0 = Synty file _00
//   1-indexed groups (Back, Shoulders, Head):     child 0 = Synty file _01
//                                                  so stored index = fileNum - 1
//
// Torso arm-piece looping:
//   ArmUpper uses a pool of 21 variants; ArmLower uses 19.
//   If the torso index N exceeds the arm pool, we wrap with % (modulo).
//   This gives each chest piece a unique but valid arm look without gaps.
//
// After running, open the generated prefabs in the Inspector to tweak names,
// gold values, icons, or add additional meshOverride entries.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class ArmorLootBuilder
{
    const string LootRoot   = "Assets/_Project/Prefabs/Loot/Armor";
    const string SyntyStatic = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Characters_ModularParts_Static";

    // Arm-pool sizes derived from actual Synty content.
    const int ArmUpperPool = 21;   // Male_04_Arm_Upper_Right has children 00–20 (21 variants)
    const int ArmLowerPool = 19;   // Male_06_Arm_Lower_Right has children 00–18 (19 variants)

    [MenuItem("Tools/Valdris/Loot/Build Armor Loot Prefabs")]
    static void Build()
    {
        bool overwrite = Event.current != null && Event.current.shift;
        int created = 0, skipped = 0;

        // ── Chest ──────────────────────────────────────────────────────────────
        // Representative: Chr_Torso_Male_NN_Static
        // Groups: Torso@N, ArmUpperRight@(N%21), ArmUpperLeft@(N%21),
        //         ArmLowerRight@(N%19), ArmLowerLeft@(N%19)
        created += ProcessGroup(
            subfolder:   "Chest",
            equipSlot:   EquipSlot.Chest,
            sourcePattern: @"Chr_Torso_Male_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("Torso",        n),
                Override("ArmUpperRight",n % ArmUpperPool),
                Override("ArmUpperLeft", n % ArmUpperPool),
                Override("ArmLowerRight",n % ArmLowerPool),
                Override("ArmLowerLeft", n % ArmLowerPool),
            },
            overwrite: overwrite, ref skipped);

        // ── Hands ──────────────────────────────────────────────────────────────
        // Representative: Chr_HandRight_Male_NN_Static
        // Groups: HandRight@N, HandLeft@N
        created += ProcessGroup(
            subfolder:   "Hands",
            equipSlot:   EquipSlot.Hands,
            sourcePattern: @"Chr_HandRight_Male_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("HandRight", n),
                Override("HandLeft",  n),
            },
            overwrite: overwrite, ref skipped);

        // ── Legs ───────────────────────────────────────────────────────────────
        // Representative: Chr_LegRight_Male_NN_Static
        // Groups: LegRight@N, LegLeft@N
        created += ProcessGroup(
            subfolder:   "Legs",
            equipSlot:   EquipSlot.Legs,
            sourcePattern: @"Chr_LegRight_Male_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("LegRight", n),
                Override("LegLeft",  n),
            },
            overwrite: overwrite, ref skipped);

        // ── Shoulders ──────────────────────────────────────────────────────────
        // Representative: Chr_ShoulderAttachRight_NN_Static  (1-indexed files)
        // Groups: Shoulder_Attachment_R@(N-1), Shoulder_Attachment_L@(N-1)
        created += ProcessGroup(
            subfolder:   "Shoulders",
            equipSlot:   EquipSlot.Shoulders,
            sourcePattern: @"Chr_ShoulderAttachRight_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("Shoulder_Attachment_R", n - 1),
                Override("Shoulder_Attachment_L", n - 1),
            },
            overwrite: overwrite, ref skipped);

        // ── Hips ───────────────────────────────────────────────────────────────
        // Representative: Chr_Hips_Male_NN_Static (0-indexed)
        created += ProcessGroup(
            subfolder:   "Hips",
            equipSlot:   EquipSlot.Hips,
            sourcePattern: @"Chr_Hips_Male_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("Hips", n),
            },
            overwrite: overwrite, ref skipped);

        // ── Head ───────────────────────────────────────────────────────────────
        // Representative: Chr_HeadCoverings_No_Hair_NN_Static (1-indexed files)
        // Groups: HeadCoverings_No_Hair@(N-1)
        created += ProcessGroup(
            subfolder:   "Head",
            equipSlot:   EquipSlot.Head,
            sourcePattern: @"Chr_HeadCoverings_No_Hair_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("HeadCoverings_No_Hair", n - 1),
            },
            overwrite: overwrite, ref skipped);

        // ── Back ───────────────────────────────────────────────────────────────
        // Representative: Chr_BackAttachment_NN_Static (1-indexed files)
        // Groups: Back_Attachment@(N-1)
        created += ProcessGroup(
            subfolder:   "Back",
            equipSlot:   EquipSlot.Back,
            sourcePattern: @"Chr_BackAttachment_(\d+)_Static\.prefab",
            buildOverrides: n => new[]
            {
                Override("Back_Attachment", n - 1),
            },
            overwrite: overwrite, ref skipped);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ArmorLootBuilder] Done. Created: {created}  Skipped: {skipped}. " +
                   "Hold Shift while clicking to overwrite existing prefabs.");
    }

    // ── Generic per-group processor ───────────────────────────────────────────

    static int ProcessGroup(
        string subfolder,
        EquipSlot equipSlot,
        string sourcePattern,
        Func<int, MeshGroupOverride[]> buildOverrides,
        bool overwrite,
        ref int skipped)
    {
        string destFolder = $"{LootRoot}/{subfolder}";
        EnsureFolder(destFolder);

        var regex   = new Regex(sourcePattern, RegexOptions.IgnoreCase);
        string[]  guids = AssetDatabase.FindAssets("t:Prefab", new[] { SyntyStatic });
        int created = 0;

        foreach (string guid in guids)
        {
            string srcPath = AssetDatabase.GUIDToAssetPath(guid);
            string srcName = Path.GetFileName(srcPath);
            var    match   = regex.Match(srcName);
            if (!match.Success) continue;

            int fileNum  = int.Parse(match.Groups[1].Value);
            string lootName = $"Armor_{subfolder}_{fileNum:D2}";
            string destPath = $"{destFolder}/{lootName}.prefab";

            if (!overwrite && File.Exists(Path.Combine(Application.dataPath,
                    "../" + destPath.Replace("Assets/", ""))))
            {
                skipped++;
                continue;
            }

            // Copy the Synty Static prefab to our Loot folder under its new name.
            AssetDatabase.CopyAsset(srcPath, destPath);
            AssetDatabase.SaveAssets();

            // Load the copy and add LootItem.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[ArmorLootBuilder] Could not load prefab at {destPath}");
                continue;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(destPath))
            {
                var root = scope.prefabContentsRoot;
                var loot = root.GetComponent<LootItem>() ?? root.AddComponent<LootItem>();

                loot.displayNameOverride = $"{subfolder} {fileNum:D2}";
                loot.itemType            = LootItemType.Armor;
                loot.equipSlot           = equipSlot;
                loot.goldValue           = 10;
                loot.armorValue          = 5;
                loot.rarity              = ItemRarity.Common;

                loot.meshOverrides.Clear();
                foreach (var ov in buildOverrides(fileNum))
                    loot.meshOverrides.Add(ov);
            }

            created++;
        }

        return created;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static MeshGroupOverride Override(string groupName, int index) =>
        new MeshGroupOverride { groupName = groupName, index = index };

    static void EnsureFolder(string path)
    {
        string[] parts  = path.Split('/');
        string   current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
