using System;
using System.Collections.Generic;
using UnityEngine;

// Shared inventory container used by the player, NPC corpses, loot containers,
// dropped bags, and (later) merchants. Plain serializable class so it can be a
// field on MonoBehaviours/ScriptableObjects and edited in the Inspector, while
// also being driven at runtime.
//
// Capacity is measured in SLOTS, not items:
//   • A non-stackable item takes one slot each (→ 1000 cloaks fills 1000 slots).
//   • A stackable item fills a slot up to its LootItem.maxStack, then spills
//     into a new slot (→ 1000 stacks of 999 arrows = the cap).
//
// Add() returns the count that did NOT fit, so callers can implement
// "leave it in the container" or "spawn an overflow bag" behaviour.
[Serializable]
public class Inventory
{
    public const int DefaultCapacity = 1000;

    [Tooltip("Maximum number of slots. Non-stackables take one slot each; " +
             "stackables fill to maxStack before opening a new slot.")]
    public int capacity = DefaultCapacity;

    [SerializeField]
    List<InventorySlot> _slots = new();

    public Inventory() { }
    public Inventory(int capacity) { this.capacity = capacity; }

    // ── Queries ───────────────────────────────────────────────────────────────

    public IReadOnlyList<InventorySlot> Slots => _slots;
    public int SlotCount => _slots.Count;
    public bool IsFull   => _slots.Count >= capacity;

    public int Count(LootItem item)
    {
        if (item == null) return 0;
        int total = 0;
        foreach (var s in _slots) if (s.item == item) total += s.count;
        return total;
    }

    public bool Contains(LootItem item) => Count(item) > 0;

    // How many of `item` could actually be added right now, given remaining
    // stack space in existing slots plus free slots × maxStack.
    public int RoomFor(LootItem item)
    {
        if (item == null) return 0;
        int room = 0;
        int max  = MaxStack(item);

        if (item.CanStack)
            foreach (var s in _slots)
                if (s.item == item) room += Mathf.Max(0, max - s.count);

        room += Mathf.Max(0, capacity - _slots.Count) * max;
        return room;
    }

    public bool HasRoomFor(LootItem item, int count = 1) => RoomFor(item) >= count;

    // ── Mutation ──────────────────────────────────────────────────────────────

    // Adds up to `count` of `item`. Returns the leftover that did NOT fit
    // (0 = everything was added). Fires OnChanged if anything changed.
    public int Add(LootItem item, int count = 1)
    {
        if (item == null || count <= 0) return count;
        int remaining = count;
        int max       = MaxStack(item);

        // Top up existing stacks first.
        if (item.CanStack)
        {
            foreach (var s in _slots)
            {
                if (remaining <= 0) break;
                if (s.item != item) continue;
                int space = max - s.count;
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                s.count   += add;
                remaining -= add;
            }
        }

        // Open new slots while there's room.
        while (remaining > 0 && _slots.Count < capacity)
        {
            int add = item.CanStack ? Mathf.Min(max, remaining) : 1;
            _slots.Add(new InventorySlot(item, add));
            remaining -= add;
        }

        if (remaining != count) OnChanged?.Invoke();
        return remaining;
    }

    // Removes up to `count` of `item`. Returns true only if the full amount
    // was removed. Fires OnChanged if anything changed.
    public bool Remove(LootItem item, int count = 1)
    {
        if (item == null || count <= 0) return false;
        int remaining = count;
        for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (_slots[i].item != item) continue;
            int take = Mathf.Min(_slots[i].count, remaining);
            _slots[i].count -= take;
            remaining        -= take;
            if (_slots[i].count <= 0) _slots.RemoveAt(i);
        }
        if (remaining != count) OnChanged?.Invoke();
        return remaining == 0;
    }

    public void Clear()
    {
        if (_slots.Count == 0) return;
        _slots.Clear();
        OnChanged?.Invoke();
    }

    // Raised whenever the contents change (add/remove/clear/restore).
    public event Action OnChanged;

    // ── Save / restore ────────────────────────────────────────────────────────

    public List<InventorySlotSave> Capture()
    {
        var saves = new List<InventorySlotSave>(_slots.Count);
        foreach (var s in _slots)
            if (s.item != null)
                saves.Add(new InventorySlotSave { itemName = s.item.ItemName, count = s.count });
        return saves;
    }

    public void Restore(List<InventorySlotSave> saved, LootRegistry registry)
    {
        _slots.Clear();
        if (saved != null && registry != null)
            foreach (var entry in saved)
            {
                var item = registry.FindByName(entry.itemName);
                if (item != null) _slots.Add(new InventorySlot(item, entry.count));
                else Debug.LogWarning($"[Inventory] Saved item not found in LootRegistry: {entry.itemName}");
            }
        OnChanged?.Invoke();
    }

    // Designer convenience: resolves any Inspector-assigned LootItem prefab
    // entries (used by NPC/container definitions) — already live references,
    // so this is a no-op placeholder kept for symmetry with Capture/Restore.

    static int MaxStack(LootItem item) =>
        item.CanStack ? Mathf.Max(1, item.maxStack) : 1;
}
