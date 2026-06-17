using UnityEditor;
using UnityEngine;

// Tools > Valdris > Migrate Thrown Weapon Prefabs
//
// One-time migration: moves ThowingKnife_01 out of Daggers/ and into a new
// Thrown/ subfolder in both the Weapons and Loot/Weapons trees.
// Safe to re-run — skips files that are already in the target location.
public static class ThrownWeaponMigrator
{
    [MenuItem("Tools/Valdris/Migrate Thrown Weapon Prefabs")]
    public static void Run()
    {
        bool anyWork = false;
        anyWork |= Move(
            "Assets/_Project/Prefabs/Weapons/Daggers/ThowingKnife_01.prefab",
            "Assets/_Project/Prefabs/Weapons/Thrown",
            "ThowingKnife_01.prefab");
        anyWork |= Move(
            "Assets/_Project/Prefabs/Loot/Weapons/Daggers/ThowingKnife_01.prefab",
            "Assets/_Project/Prefabs/Loot/Weapons/Thrown",
            "ThowingKnife_01.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Migrate Thrown Weapon Prefabs",
            anyWork ? "Done. ThowingKnife_01 moved to Thrown/ in both trees."
                    : "Nothing to move — files are already in Thrown/.",
            "OK");
    }

    static bool Move(string srcPath, string destFolder, string fileName)
    {
        if (!System.IO.File.Exists(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath),
                    srcPath.Replace('/', System.IO.Path.DirectorySeparatorChar))))
        {
            Debug.Log($"[ThrownWeaponMigrator] Already moved or missing: {srcPath}");
            return false;
        }

        EnsureFolder(destFolder);
        string destPath = $"{destFolder}/{fileName}";
        string err = AssetDatabase.MoveAsset(srcPath, destPath);
        if (!string.IsNullOrEmpty(err))
        {
            Debug.LogWarning($"[ThrownWeaponMigrator] Move failed ({srcPath}): {err}");
            return false;
        }
        Debug.Log($"[ThrownWeaponMigrator] Moved {srcPath} → {destPath}");
        return true;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string built = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{built}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(built, parts[i]);
            built = next;
        }
    }
}
