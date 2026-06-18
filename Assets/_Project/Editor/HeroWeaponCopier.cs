using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Valdris > Copy Hero Weapon Prefabs
//
// Copies weapon and shield prefabs from PolygonFantasyHeroCharacters into the
// project's Weapons folder, stripping the "SM_Wep_" prefix and routing each
// prefab to the correct subfolder based on its type.
//
// Naming conflict resolution: if the target name exists, "_duplicate" is
// appended. If that also exists, "_1" is appended to the duplicate suffix,
// then "_2", etc. — matching the convention already in use in the project.
//
// Safe to re-run: resolved destination names that already exist are skipped.
public static class HeroWeaponCopier
{
    const string Source  = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Weapons";
    const string DestRoot = "Assets/_Project/Prefabs/Weapons";

    // Maps the word that follows "SM_Wep_" to a destination subfolder.
    // Evaluated left-to-right; first match wins.
    static readonly (string prefix, string folder)[] FolderMap =
    {
        ("Axe",           "Axes"),
        ("Dagger",        "Daggers"),
        ("ThowingKnife",  "Daggers"),   // note: Synty typo preserved
        ("Joust",         "Spears"),    // lances / jousting weapons
        ("Mace",          "Maces"),
        ("Shield",        "Shields"),
        ("Staff",         "Staffs"),
        ("Sword",         "Swords"),
    };

    [MenuItem("Tools/Valdris/Weapons/Copy Hero Weapon Prefabs")]
    public static void Run()
    {
        // Build a lookup of all existing filenames per subfolder for fast
        // conflict detection (case-insensitive, without extension).
        var existing = new Dictionary<string, HashSet<string>>();
        foreach (var (_, folder) in FolderMap)
        {
            string dir = $"{DestRoot}/{folder}";
            EnsureFolder(dir);
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
                set.Add(Path.GetFileNameWithoutExtension(
                             AssetDatabase.GUIDToAssetPath(g)));
            existing[folder] = set;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Source });
        int copied = 0, skipped = 0;

        foreach (string guid in guids)
        {
            string srcPath  = AssetDatabase.GUIDToAssetPath(guid);
            string srcFile  = Path.GetFileNameWithoutExtension(srcPath); // e.g. SM_Wep_Sword_01

            // Strip "SM_Wep_" prefix.
            string baseName = srcFile.StartsWith("SM_Wep_")
                ? srcFile.Substring("SM_Wep_".Length)   // e.g. Sword_01
                : srcFile;

            // Resolve destination folder.
            string destFolder = null;
            foreach (var (prefix, folder) in FolderMap)
            {
                if (baseName.StartsWith(prefix))
                {
                    destFolder = folder;
                    break;
                }
            }

            if (destFolder == null)
            {
                Debug.LogWarning($"[HeroWeaponCopier] No folder mapping for '{srcFile}' — skipped.");
                skipped++;
                continue;
            }

            // Resolve a unique name within the destination folder.
            var taken    = existing[destFolder];
            string name  = UniqueName(baseName, taken);
            string dest  = $"{DestRoot}/{destFolder}/{name}.prefab";

            if (!AssetDatabase.CopyAsset(srcPath, dest))
            {
                Debug.LogWarning($"[HeroWeaponCopier] Failed to copy {srcPath} → {dest}");
                skipped++;
                continue;
            }

            taken.Add(name); // register so subsequent passes see it
            copied++;
            Debug.Log($"[HeroWeaponCopier] {srcFile} → {destFolder}/{name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Copy Hero Weapon Prefabs",
            $"Done.\n\nCopied:  {copied}\nSkipped: {skipped}",
            "OK");
    }

    // Returns a filename that doesn't exist in `taken`.
    // Strategy: base → base_duplicate → base_duplicate_1 → base_duplicate_2 …
    static string UniqueName(string baseName, HashSet<string> taken)
    {
        if (!taken.Contains(baseName)) return baseName;

        string dup = $"{baseName}_duplicate";
        if (!taken.Contains(dup)) return dup;

        for (int i = 1; i < 100; i++)
        {
            string candidate = $"{dup}_{i}";
            if (!taken.Contains(candidate)) return candidate;
        }

        // Fallback (should never reach here with sane data).
        return $"{baseName}_{System.Guid.NewGuid():N}";
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
