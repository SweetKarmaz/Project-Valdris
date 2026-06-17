using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates the arrow ammo LootItem prefabs (one per damage type) from the base
// flying-arrow projectile prefab. Run via Tools > Valdris > Generate Arrow Ammo.
//
// Each arrow: 5 Physical projectile damage; elemental variants add +5 of their
// type as an on-hit rider. A bow's own damage/riders are added on top at hit
// time (see ProjectileBehaviour). Arrows are stackable Projectile items and are
// registered automatically (they live under Loot/), so they save/load by name.
public static class ArrowAmmoGenerator
{
    const string BasePrefab = "Assets/_Project/Prefabs/WeaponProjectiles/Arrow_01.prefab";
    const string OutFolder  = "Assets/_Project/Prefabs/Loot/Weapons/Ammo";

    [MenuItem("Tools/Valdris/Generate Arrow Ammo")]
    public static void Generate()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
        if (basePrefab == null)
        {
            Debug.LogError($"[ArrowAmmo] Base arrow prefab not found at {BasePrefab}.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(OutFolder))
            Directory.CreateDirectory(OutFolder);

        (string name, DamageType type)[] kinds =
        {
            ("Arrow",            DamageType.Physical),
            ("Fire Arrow",       DamageType.Fire),
            ("Ice Arrow",        DamageType.Ice),
            ("Lightning Arrow",  DamageType.Lightning),
            ("Holy Arrow",       DamageType.Holy),
            ("Corruption Arrow", DamageType.Corruption),
        };

        int made = 0;
        foreach (var (display, type) in kinds)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var li = inst.GetComponent<LootItem>() ?? inst.AddComponent<LootItem>();
            li.displayNameOverride = display;
            li.itemType            = LootItemType.Projectile;
            li.projectileType      = ProjectileType.Arrow;
            li.projectileDamage    = 5f;
            li.rarity              = ItemRarity.Common;
            li.isStackable         = true;
            li.maxStack            = 999;
            li.goldValue           = 1;
            li.flavorText          = type == DamageType.Physical
                ? "A simple arrow."
                : $"An arrow tipped with {type.ToString().ToLowerInvariant()}.";
            li.onHitEffects = new List<OnHitEffect>();
            if (type != DamageType.Physical)
                li.onHitEffects.Add(new OnHitEffect { type = type, damage = 5f, procChance = 1f, debuff = null });

            // Ensure a trigger collider exists so the arrow can register hits.
            var col = inst.GetComponentInChildren<Collider>();
            if (col == null)
            {
                var sc = inst.AddComponent<SphereCollider>();
                sc.radius = 0.05f;
                sc.isTrigger = true;
            }
            else col.isTrigger = true;

            string path = $"{OutFolder}/{display.Replace(' ', '_')}.prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            made++;
            Debug.Log($"[ArrowAmmo] Created {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LootRegistryBuilder.RebuildViaMenu();
        Debug.Log($"[ArrowAmmo] Done — {made} arrow ammo prefabs generated and registered.");
    }
}
