using System.Collections.Generic;
using UnityEngine;

// Single ScriptableObject asset that lists every LootItem prefab in the project.
// Assign this asset to GameDatabase.lootRegistry.
//
// The list is maintained automatically by LootRegistryBuilder (Editor) whenever
// prefabs in Assets/_Project/Prefabs/Loot/ are created, changed, or deleted.
// You can also rebuild manually: right-click the asset → Rebuild, or use the
// Tools > Valdris > Rebuild Loot Registry menu item.
[CreateAssetMenu(fileName = "LootRegistry", menuName = "Valdris/Loot Registry")]
public class LootRegistry : ScriptableObject
{
    [SerializeField]
    private List<LootItem> _items = new();

    // Folder-classified bases for randomized loot generation. Populated by
    // LootRegistryBuilder at editor time (the only place the asset path is known).
    [System.Serializable]
    public struct GenBase
    {
        public LootItem    item;
        public GenCategory category;
    }

    [SerializeField]
    private List<GenBase> _genBases = new();

    public IReadOnlyList<LootItem> Items => _items;
    public IReadOnlyList<GenBase>  GenBases => _genBases;

    // ── Lookups ───────────────────────────────────────────────────────────────

    public LootItem FindByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        foreach (var item in _items)
            if (item != null && item.ItemName == itemName) return item;
        return null;
    }

    public List<LootItem> GetByType(LootItemType type)
    {
        var result = new List<LootItem>();
        foreach (var item in _items)
            if (item != null && item.itemType == type) result.Add(item);
        return result;
    }

    public List<LootItem> GetByRarity(ItemRarity rarity)
    {
        var result = new List<LootItem>();
        foreach (var item in _items)
            if (item != null && item.rarity == rarity) result.Add(item);
        return result;
    }

    // Returns all weapons of a specific category — used by NpcController to
    // resolve the preferred attack type from the NPC's carried items.
    public List<LootItem> GetWeaponsByCategory(WeaponCategory category)
    {
        var result = new List<LootItem>();
        foreach (var item in _items)
            if (item != null && item.IsWeapon && item.weaponCategory == category)
                result.Add(item);
        return result;
    }

    // ── Editor population (called by LootRegistryBuilder) ────────────────────

#if UNITY_EDITOR
    [ContextMenu("Rebuild (scan Loot folder)")]
    public void EditorRebuild()
    {
        var found = new List<LootItem>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            "t:Prefab", new[] { "Assets/_Project/Prefabs/Loot" });

        foreach (string guid in guids)
        {
            string path   = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
            if (prefab == null) continue;
            var loot = prefab.GetComponent<LootItem>();
            if (loot != null) found.Add(loot);
        }

        found.Sort((a, b) =>
            string.Compare(a.ItemName, b.ItemName, System.StringComparison.OrdinalIgnoreCase));

        EditorSetItems(found, new List<GenBase>());
    }

    // Called by LootRegistryBuilder — sets the lists and marks the asset dirty.
    public void EditorSetItems(List<LootItem> items, List<GenBase> genBases)
    {
        _items    = new List<LootItem>(items);
        _genBases = genBases != null ? new List<GenBase>(genBases) : new List<GenBase>();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
