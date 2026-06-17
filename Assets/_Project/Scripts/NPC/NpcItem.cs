using System;
using UnityEngine;

// One entry in an NPC's carried item list (NpcController.items, per-instance).
// Assign a LootItem prefab from Assets/_Project/Prefabs/Loot/.
// The item name is derived from the prefab; dropChance is rolled on death.
[Serializable]
public class NpcItem
{
    [Tooltip("Drag a prefab from Assets/_Project/Prefabs/Loot/ here.")]
    public LootItem lootItem;

    [Min(1)]
    public int quantity = 1;

    [Range(0f, 1f)]
    [Tooltip("Probability this item drops when the NPC is looted (1 = always).")]
    public float dropChance = 1f;

    // Canonical name — used by loot windows, save state, and quest checks.
    public string ItemName => lootItem != null ? lootItem.ItemName : string.Empty;
}
