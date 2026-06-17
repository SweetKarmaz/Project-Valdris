using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Valdris > Sync Weapon Loot Prefabs
//
// Compares Assets/_Project/Prefabs/Weapons/<sub> against
// Assets/_Project/Prefabs/Loot/Weapons/<sub> and copies any missing prefabs,
// then adds a LootItem component with the correct defaults:
//   Shields  → itemType = Armor,  armorValue = 5
//   All else → itemType = Weapon, weaponCategory = Melee
//
// Skips: Components/, General/, Ranged/ (bows are already in Loot and
//        the remaining ranged items are guns we don't want as loot).
// Safe to re-run: prefabs already present in the Loot folder are skipped.
public static class WeaponLootSyncer
{
    const string WeaponsSrc  = "Assets/_Project/Prefabs/Weapons";
    const string LootDest    = "Assets/_Project/Prefabs/Loot/Weapons";

    // Subfolders we sync — all others are silently ignored.
    static readonly string[] Subfolders = { "Axes", "Daggers", "Maces", "Shields", "Spears", "Staffs", "Swords", "Thrown" };

    [MenuItem("Tools/Valdris/Sync Weapon Loot Prefabs")]
    public static void Run()
    {
        int copied = 0, skipped = 0;

        foreach (string sub in Subfolders)
        {
            bool isShieldFolder = sub == "Shields";
            bool isThrownFolder = sub == "Thrown";

            string srcDir  = $"{WeaponsSrc}/{sub}";
            string destDir = $"{LootDest}/{sub}";
            EnsureFolder(destDir);

            // Index what already exists in the loot folder (by filename, no extension).
            var existing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { destDir }))
                existing.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)));

            // Walk every prefab in the source subfolder.
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { srcDir }))
            {
                string srcPath = AssetDatabase.GUIDToAssetPath(g);
                // Only process prefabs directly in this subfolder, not nested ones.
                if (Path.GetDirectoryName(srcPath).Replace('\\', '/') != srcDir)
                    continue;

                string name     = Path.GetFileNameWithoutExtension(srcPath);
                string destPath = $"{destDir}/{name}.prefab";

                if (existing.Contains(name))
                {
                    skipped++;
                    continue;
                }

                // Copy the prefab.
                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                {
                    Debug.LogWarning($"[WeaponLootSyncer] Failed to copy {srcPath}");
                    skipped++;
                    continue;
                }

                // Load the copy and add / configure the LootItem component.
                GameObject go = PrefabUtility.LoadPrefabContents(destPath);
                try
                {
                    LootItem loot = go.GetComponent<LootItem>() ?? go.AddComponent<LootItem>();
                    loot.displayNameOverride = FormatDisplayName(name);

                    if (isThrownFolder)
                    {
                        loot.itemType       = LootItemType.Projectile;
                        loot.projectileType = ResolveProjectileType(name);
                        loot.isStackable    = true;
                        loot.throwCooldown  = 0.8f;
                    }
                    else if (isShieldFolder)
                    {
                        loot.itemType   = LootItemType.Armor;
                        loot.armorValue = 5f;
                    }
                    else
                    {
                        loot.itemType       = LootItemType.Weapon;
                        loot.weaponCategory = WeaponCategory.Melee;
                    }

                    PrefabUtility.SaveAsPrefabAsset(go, destPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(go);
                }

                existing.Add(name);
                copied++;
                Debug.Log($"[WeaponLootSyncer] {sub}/{name}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Sync Weapon Loot Prefabs",
            $"Done.\n\nCopied:  {copied}\nSkipped: {skipped}",
            "OK");
    }

    static ProjectileType ResolveProjectileType(string name)
    {
        string lower = name.ToLower();
        if (lower.Contains("javelin"))                      return ProjectileType.Javelin;
        if (lower.Contains("dart"))                        return ProjectileType.Dart;
        if (lower.Contains("stone") || lower.Contains("sling")) return ProjectileType.SlingStone;
        // ThrowingKnife, shiv, knife, and anything else in this folder default to ThrowingKnife.
        return ProjectileType.ThrowingKnife;
    }

    // "Sword_Cover_01" → "Sword Cover 01"  (simple space insertion on underscores)
    static string FormatDisplayName(string assetName)
    {
        return assetName.Replace('_', ' ');
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string   built = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{built}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(built, parts[i]);
            built = next;
        }
    }
}
