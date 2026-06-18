using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Tools → Valdris → Build Loot Containers
//
// Copies chest / sack / bag / pouch prefabs from the Items library into
// Assets/_Project/Prefabs/LootContainers/, adds a LootContainer component, and
// ensures each has a collider so the aim-raycast interaction can hit it. The
// originals are left untouched (still usable as plain props). Safe to re-run;
// hold Shift when invoking to overwrite existing copies.
public static class LootContainerBuilder
{
    const string DestFolder = "Assets/_Project/Prefabs/LootContainers";

    static readonly string[] SourceFolders =
    {
        "Assets/_Project/Prefabs/Items/Furniture/Storage",
        "Assets/_Project/Prefabs/Items/Containers",
    };

    // Names we treat as lootable containers (chests + sacks/bags/pouches).
    static readonly Regex Match = new(@"^(Chest|Sack|Bag|Bags|Pouch)", RegexOptions.IgnoreCase);

    [MenuItem("Tools/Valdris/Loot/Build Loot Containers")]
    static void Build()
    {
        bool overwrite = Event.current != null && Event.current.shift;

        if (!AssetDatabase.IsValidFolder(DestFolder))
        {
            Directory.CreateDirectory(DestFolder);
            AssetDatabase.Refresh();
        }

        int made = 0, updated = 0, skipped = 0;

        foreach (string folder in SourceFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string srcPath = AssetDatabase.GUIDToAssetPath(guid);
                string name    = Path.GetFileNameWithoutExtension(srcPath);

                if (!Match.IsMatch(name)) continue;
                if (name.ToLowerInvariant().Contains("duplicate")) continue;

                string destPath = $"{DestFolder}/{name}.prefab";
                bool exists = File.Exists(destPath);
                if (exists && !overwrite) { skipped++; SetupExisting(destPath, ref updated); continue; }

                if (exists) AssetDatabase.DeleteAsset(destPath);
                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                {
                    Debug.LogWarning($"[LootContainerBuilder] Failed to copy {srcPath}");
                    continue;
                }
                Setup(destPath);
                made++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LootContainerBuilder] Done. Created {made}, updated {updated}, skipped {skipped} " +
                  $"(in {DestFolder}). Hold Shift to overwrite existing copies.");
    }

    // Adds LootContainer + a collider to an already-copied prefab if missing.
    static void SetupExisting(string destPath, ref int updated)
    {
        var go = PrefabUtility.LoadPrefabContents(destPath);
        bool changed = false;

        if (go.GetComponent<LootContainer>() == null) { go.AddComponent<LootContainer>(); changed = true; }
        if (go.GetComponentInChildren<Collider>() == null) { AddBoundsCollider(go); changed = true; }

        if (changed) { PrefabUtility.SaveAsPrefabAsset(go, destPath); updated++; }
        PrefabUtility.UnloadPrefabContents(go);
    }

    static void Setup(string destPath)
    {
        var go = PrefabUtility.LoadPrefabContents(destPath);

        if (go.GetComponent<LootContainer>() == null) go.AddComponent<LootContainer>();
        if (go.GetComponentInChildren<Collider>() == null) AddBoundsCollider(go);

        PrefabUtility.SaveAsPrefabAsset(go, destPath);
        PrefabUtility.UnloadPrefabContents(go);
    }

    // Adds a BoxCollider on the root sized to the combined mesh bounds so the
    // crosshair raycast can hit the whole model.
    static void AddBoundsCollider(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<MeshRenderer>();
        var box = go.AddComponent<BoxCollider>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // Convert world AABB to the root's local space (root is at origin here).
        box.center = go.transform.InverseTransformPoint(b.center);
        box.size   = b.size;
    }
}
