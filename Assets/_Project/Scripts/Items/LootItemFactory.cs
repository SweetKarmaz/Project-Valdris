using System.Collections.Generic;
using UnityEngine;

// Builds a live, runtime LootItem from an ItemRoll. The clone is a real
// instance of the base prefab (so its model/icon/grip data are intact for the
// held-visual and inventory UI), kept disabled under a persistent holder, with
// its stats/name/flavor overwritten by the roll.
//
// Because the clone IS a LootItem, all existing UI / equip / stat-reading code
// works on it unchanged. Persistence is handled by storing the ItemRoll
// (LootItem.runtimeRoll) in the save instead of the item name.
public static class LootItemFactory
{
    static Transform _holder;

    static Transform Holder()
    {
        if (_holder == null)
        {
            var go = new GameObject("~GeneratedLoot");
            // Kept INACTIVE: clones parented here are inactive-in-hierarchy, so
            // their models never render in the world and any Light/HDAdditionalLightData
            // they carry never registers with HDRP (which otherwise crashes on the
            // register/unregister churn when the next frame renders).
            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            _holder = go.transform;
        }
        return _holder;
    }

    public static LootItem Build(ItemRoll roll)
    {
        if (roll == null) return null;

        var registry = SaveSystem.Instance?.database?.lootRegistry;
        var basePrefab = registry != null ? registry.FindByName(roll.basePrefabName) : null;
        if (basePrefab == null)
        {
            Debug.LogWarning($"[LootItemFactory] Base prefab not found for generated item: " +
                             $"'{roll.basePrefabName}' ({roll.displayName}).");
            return null;
        }

        // Parent is inactive, so the clone is inactive-in-hierarchy automatically.
        var clone = Object.Instantiate(basePrefab.gameObject, Holder());
        clone.name = roll.displayName;

        var li = clone.GetComponent<LootItem>();
        li.displayNameOverride = roll.displayName;
        li.flavorText          = roll.flavorText;
        li.rarity              = roll.rarity;
        li.goldValue           = roll.goldValue;
        li.isStackable         = false;

        if (roll.isWeapon)
        {
            li.weaponDamage = roll.weaponDamage;
            li.isTwoHanded  = roll.isTwoHanded;
        }
        else
        {
            li.armorValue = roll.armorValue;
        }

        // Fresh copies so edits never leak back to the base prefab's serialized data.
        li.statModifiers = new List<StatModifier>();
        foreach (var m in roll.statModifiers)
            li.statModifiers.Add(new StatModifier { stat = m.stat, mode = m.mode, amount = m.amount });

        li.onHitEffects = new List<OnHitEffect>();
        foreach (var b in roll.bonusDamage)
            li.onHitEffects.Add(new OnHitEffect { type = b.type, damage = b.damage, procChance = 1f, debuff = null });

        li.runtimeRoll = roll;
        return li;
    }
}
