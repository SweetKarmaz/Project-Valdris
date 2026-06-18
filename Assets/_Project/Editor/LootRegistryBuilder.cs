using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Keeps LootRegistry in sync with all prefabs in Assets/_Project/Prefabs/Loot/.
//
// Runs automatically via AssetPostprocessor whenever a prefab in that folder
// is imported or deleted. Also available via:
//   Tools > Valdris > Rebuild Loot Registry
//   Right-click any LootRegistry asset → Rebuild (scan Loot folder)
[InitializeOnLoad]
public static class LootRegistryBuilder
{
    const string LootFolder    = "Assets/_Project/Prefabs/Loot";
    const string RegistryPath  = "Assets/_Project/ScriptableObjects/LootRegistry.asset";

    // Auto-run once on domain reload (Play / recompile) to catch any changes
    // made while the editor was closed.
    static LootRegistryBuilder() => EditorApplication.delayCall += AutoRebuildIfNeeded;

    [MenuItem("Tools/Valdris/Loot/Rebuild Loot Registry")]
    public static void RebuildViaMenu()
    {
        var registry = GetOrCreateRegistry();
        Rebuild(registry);
        Debug.Log($"[LootRegistry] Rebuilt — {registry.Items.Count} items registered.");
    }

    // Called by LootRegistry.EditorRebuild() (context menu on the asset).
    public static void Rebuild(LootRegistry registry)
    {
        var found    = new List<LootItem>();
        var genBases = new List<LootRegistry.GenBase>();
        string[]  guids  = AssetDatabase.FindAssets("t:Prefab", new[] { LootFolder });

        foreach (string guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var loot = prefab.GetComponent<LootItem>();
            if (loot == null) continue;
            found.Add(loot);

            // Classify by folder for the randomized-loot generator.
            var category = LootClassifier.ClassifyByPath(path, prefab.name);
            if (category != GenCategory.None)
                genBases.Add(new LootRegistry.GenBase { item = loot, category = category });
        }

        // Sort alphabetically so the list is stable across rebuilds.
        found.Sort((a, b) =>
            string.Compare(a.ItemName, b.ItemName, System.StringComparison.OrdinalIgnoreCase));

        registry.EditorSetItems(found, genBases);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LootRegistry] Rebuilt — {found.Count} items, {genBases.Count} generatable bases.");
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    static void AutoRebuildIfNeeded()
    {
        var registry = GetOrCreateRegistry();
        Rebuild(registry);
    }

    static LootRegistry GetOrCreateRegistry()
    {
        var existing = AssetDatabase.LoadAssetAtPath<LootRegistry>(RegistryPath);
        if (existing != null) return existing;

        // Create the registry asset if it doesn't exist yet.
        var newRegistry = ScriptableObject.CreateInstance<LootRegistry>();
        string dir = System.IO.Path.GetDirectoryName(RegistryPath);
        if (!AssetDatabase.IsValidFolder(dir))
            System.IO.Directory.CreateDirectory(dir);

        AssetDatabase.CreateAsset(newRegistry, RegistryPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LootRegistry] Created new registry at {RegistryPath}");
        return newRegistry;
    }

    // Postprocessor watches the Loot folder and auto-rebuilds on changes.
    class LootPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool relevant = false;
            foreach (string path in imported)
                if (path.StartsWith(LootFolder) && path.EndsWith(".prefab"))
                { relevant = true; break; }

            if (!relevant)
                foreach (string path in deleted)
                    if (path.StartsWith(LootFolder) && path.EndsWith(".prefab"))
                    { relevant = true; break; }

            if (!relevant)
                foreach (string path in moved)
                    if (path.StartsWith(LootFolder) && path.EndsWith(".prefab"))
                    { relevant = true; break; }

            if (relevant)
            {
                var registry = GetOrCreateRegistry();
                Rebuild(registry);
            }
        }
    }
}
