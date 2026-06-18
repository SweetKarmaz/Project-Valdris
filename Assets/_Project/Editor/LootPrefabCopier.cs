using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Valdris > Copy Loot Prefabs
//
// Copies source prefabs into Assets/_Project/Prefabs/Loot/ without touching
// the originals (which remain available as scene props).
// Run once; re-running is safe — existing copies are skipped.
public static class LootPrefabCopier
{
    const string LootRoot = "Assets/_Project/Prefabs/Loot";

    // ── Copy rules ─────────────────────────────────────────────────────────────
    // Each entry: (sourceFolder, destSubfolder, optional name filter)
    // A null filter copies everything in that folder.
    // A non-null HashSet copies only prefabs whose filename (without .prefab)
    // is in the set.

    static readonly List<CopyRule> Rules = new()
    {
        // ── Accessories / Materials / KeyItems ─────────────────────────────────
        // Items/Loot — everything except display stands, mannequins, coin piles
        new("Assets/_Project/Prefabs/Items/Loot", "Accessories",
            exclude: new HashSet<string>
            {
                "Jewellery_Display_01","Jewellery_Display_02","Jewellery_Display_03",
                "Necklace_Mannequin_01","Necklace_Mannequin_02","Necklace_Mannequin_03",
                "Necklace_Mannequin_04","Necklace_Mannequin_05","Necklace_Mannequin_06",
                "Necklace_Mannequin_07","Necklace_Mannequin_08",
                "Coins_01","Coins_02","Coins_03","Coins_04",
            }),

        // ── Weapons ────────────────────────────────────────────────────────────
        new("Assets/_Project/Prefabs/Weapons/Axes",    "Weapons/Axes"),
        new("Assets/_Project/Prefabs/Weapons/Daggers", "Weapons/Daggers"),
        new("Assets/_Project/Prefabs/Weapons/Maces",   "Weapons/Maces"),
        new("Assets/_Project/Prefabs/Weapons/Spears",  "Weapons/Spears"),
        new("Assets/_Project/Prefabs/Weapons/Staffs",  "Weapons/Staffs"),
        new("Assets/_Project/Prefabs/Weapons/Swords",  "Weapons/Swords"),
        new("Assets/_Project/Prefabs/Weapons/Shields", "Weapons/Shields"),

        // Bows only — skip Elephant_Gun, MusketPistol
        new("Assets/_Project/Prefabs/Weapons/Ranged", "Weapons/Ranged",
            include: new HashSet<string>
            {
                "Bow_01","Bow_02","Bow_Rigged_01","Bow_Rigged_02",
            }),

        // ── Consumables (food) ─────────────────────────────────────────────────
        new("Assets/_Project/Prefabs/Items/Food", "Consumables",
            include: new HashSet<string>
            {
                "Bread_01","Bread_02","Bread_03",
                "Meat_Chicken_Cooked_01","Meat_Steak_Cooked_01","Meat_Turkey_Cooked_01",
                "Pie_01","Pie_02","Pie_03","Pie_04",
                "Fruit_01","Fruit_02","Fruit_03",
                "Cheese_01","Cheese_02","Cheese_03","Cheese_Slice_01",
                "Herb_Bunch_01",
                "Shrooms",
                "HoneyJar_01",
                "Wine_01","Wine_02",
            }),

        // ── KeyItems (scrolls, maps) ───────────────────────────────────────────
        new("Assets/_Project/Prefabs/Items/Books", "KeyItems",
            include: new HashSet<string>
            {
                "Scroll_01","Scroll_02",
                "Map_01","Map_02",
            }),
    };

    // ── Menu entry ─────────────────────────────────────────────────────────────

    [MenuItem("Tools/Valdris/Loot/Copy Loot Prefabs")]
    public static void Run()
    {
        int copied  = 0;
        int skipped = 0;

        foreach (var rule in Rules)
        {
            if (!AssetDatabase.IsValidFolder(rule.Source))
            {
                Debug.LogWarning($"[LootPrefabCopier] Source folder not found: {rule.Source}");
                continue;
            }

            string destFolder = $"{LootRoot}/{rule.Dest}";
            EnsureFolder(destFolder);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { rule.Source });
            foreach (string guid in guids)
            {
                string srcPath  = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(srcPath);

                // Only look one level deep (FindAssets recurses by default).
                // Skip prefabs in subdirectories of the source folder.
                string relativePath = srcPath.Substring(rule.Source.Length).TrimStart('/');
                if (relativePath.Contains('/')) continue;

                if (!rule.ShouldCopy(fileName)) continue;

                string destPath = $"{destFolder}/{Path.GetFileName(srcPath)}";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
                {
                    skipped++;
                    continue;
                }

                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                    Debug.LogWarning($"[LootPrefabCopier] Failed to copy: {srcPath}");
                else
                {
                    copied++;
                    Debug.Log($"[LootPrefabCopier] Copied {srcPath} → {destPath}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Copy Loot Prefabs",
            $"Done.\n\nCopied: {copied}\nSkipped (already exist): {skipped}",
            "OK");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    static void EnsureFolder(string folderPath)
    {
        // Walk each path segment and create missing folders.
        string[] parts = folderPath.Split('/');
        string   built = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{built}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(built, parts[i]);
            built = next;
        }
    }

    // ── Rule definition ────────────────────────────────────────────────────────

    class CopyRule
    {
        public string         Source;
        public string         Dest;
        HashSet<string>       _include; // null = include all
        HashSet<string>       _exclude; // null = exclude none

        public CopyRule(string source, string dest,
                        HashSet<string> include = null,
                        HashSet<string> exclude = null)
        {
            Source   = source;
            Dest     = dest;
            _include = include;
            _exclude = exclude;
        }

        public bool ShouldCopy(string fileName)
        {
            if (_exclude != null && _exclude.Contains(fileName)) return false;
            if (_include != null && !_include.Contains(fileName)) return false;
            return true;
        }
    }
}
