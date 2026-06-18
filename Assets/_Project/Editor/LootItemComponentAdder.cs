using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Valdris > Add LootItem Components
//
// Scans every prefab under Assets/_Project/Prefabs/Loot/ and adds a LootItem
// component to any that don't already have one.
//
// Default field values are inferred from the prefab's subfolder so each item
// starts with a sensible type — designers just fill in damage, stats, etc.
//
// Subfolder → LootItemType mapping:
//   Accessories/       → Accessory
//   Weapons/Axes/      → Weapon  (Melee)
//   Weapons/Daggers/   → Weapon  (Melee)
//   Weapons/Maces/     → Weapon  (Melee)
//   Weapons/Spears/    → Weapon  (Melee)
//   Weapons/Staffs/    → Weapon  (Melee)
//   Weapons/Swords/    → Weapon  (Melee)
//   Weapons/Shields/   → Armor   (OffHand)
//   Weapons/Ranged/    → Weapon  (Ranged)
//   Consumables/       → Consumable
//   KeyItems/          → KeyItem
//   (anything else)    → Misc
public static class LootItemComponentAdder
{
    const string LootRoot = "Assets/_Project/Prefabs/Loot";

    [MenuItem("Tools/Valdris/Loot/Add LootItem Components")]
    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { LootRoot });

        int added   = 0;
        int skipped = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip prefabs that already have the component.
            var check = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (check == null) continue;
            if (check.GetComponent<LootItem>() != null) { skipped++; continue; }

            var contents = PrefabUtility.LoadPrefabContents(path);

            var item = contents.AddComponent<LootItem>();
            ApplyDefaults(item, path);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            added++;
            Debug.Log($"[LootItemComponentAdder] Added LootItem to {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Add LootItem Components",
            $"Done.\n\nAdded:   {added}\nSkipped (already had component): {skipped}",
            "OK");
    }

    // ── Default values from subfolder path ────────────────────────────────────

    static void ApplyDefaults(LootItem item, string assetPath)
    {
        // Normalise to forward slashes and lower-case for matching.
        string rel = assetPath.Replace('\\', '/').ToLowerInvariant();

        if (rel.Contains("/accessories/"))
        {
            item.itemType = LootItemType.Accessory;
        }
        else if (rel.Contains("/weapons/shields/"))
        {
            // Shields are off-hand defensive items — Armor type makes them
            // show up in the OffHand equipment slot and carry armorValue.
            item.itemType  = LootItemType.Armor;
            item.armorValue = 5f;
        }
        else if (rel.Contains("/weapons/ranged/"))
        {
            item.itemType          = LootItemType.Weapon;
            item.weaponCategory    = WeaponCategory.Ranged;
            item.requiredProjectile = ProjectileType.Arrow;
            item.weaponDamage      = 10f;
            item.weaponRange       = 40f;
            item.reloadTime        = 1.2f;
            item.spread            = 3f;
        }
        else if (rel.Contains("/weapons/axes/"))
        {
            item.itemType       = LootItemType.Weapon;
            item.weaponCategory = WeaponCategory.Melee;
            item.weaponDamage   = 12f;
            item.weaponRange    = 1.6f;
        }
        else if (rel.Contains("/weapons/daggers/"))
        {
            item.itemType            = LootItemType.Weapon;
            item.weaponCategory      = WeaponCategory.Melee;
            item.weaponDamage        = 7f;
            item.weaponRange         = 1.2f;
            item.attackSpeedModifier = 0.2f;  // daggers are faster
        }
        else if (rel.Contains("/weapons/maces/"))
        {
            item.itemType       = LootItemType.Weapon;
            item.weaponCategory = WeaponCategory.Melee;
            item.weaponDamage   = 14f;
            item.weaponRange    = 1.5f;
        }
        else if (rel.Contains("/weapons/spears/"))
        {
            item.itemType       = LootItemType.Weapon;
            item.weaponCategory = WeaponCategory.Melee;
            item.weaponDamage   = 11f;
            item.weaponRange    = 2.5f;  // longer reach
        }
        else if (rel.Contains("/weapons/staffs/"))
        {
            item.itemType       = LootItemType.Weapon;
            item.weaponCategory = WeaponCategory.Melee;
            item.weaponDamage   = 8f;
            item.weaponRange    = 2.0f;
        }
        else if (rel.Contains("/weapons/swords/"))
        {
            item.itemType       = LootItemType.Weapon;
            item.weaponCategory = WeaponCategory.Melee;
            item.weaponDamage   = 10f;
            item.weaponRange    = 1.5f;
        }
        else if (rel.Contains("/consumables/"))
        {
            item.itemType      = LootItemType.Consumable;
            item.restoresHealth = true;
            item.healthAmount   = 20f;
            item.consumedOnUse  = true;
            item.goldValue      = 5;
        }
        else if (rel.Contains("/keyitems/"))
        {
            item.itemType  = LootItemType.KeyItem;
            item.goldValue = 0;  // key items typically can't be sold
        }
        else
        {
            item.itemType = LootItemType.Misc;
        }

        // Common defaults for all items.
        item.rarity           = ItemRarity.Common;
        item.defaultDropChance = 1f;

        // Gold value baseline by type if not already set above.
        if (item.goldValue == 0 && item.itemType != LootItemType.KeyItem)
            item.goldValue = DefaultGoldValue(item.itemType);
    }

    static int DefaultGoldValue(LootItemType type) => type switch
    {
        LootItemType.Weapon      => 20,
        LootItemType.Armor       => 15,
        LootItemType.Accessory   => 25,
        LootItemType.Consumable  => 5,
        LootItemType.Material    => 10,
        LootItemType.Misc        => 1,
        _                        => 1,
    };
}
