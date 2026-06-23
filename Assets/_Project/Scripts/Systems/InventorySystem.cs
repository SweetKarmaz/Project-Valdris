using System;
using System.Collections.Generic;
using UnityEngine;

// One slot in an inventory. Stackable items (Projectile, Consumable, or
// LootItem.isStackable) accumulate count in a slot up to LootItem.maxStack;
// everything else occupies its own slot.
[Serializable]
public class InventorySlot
{
    public LootItem item;   // prefab reference — never an instance
    public int      count;

    public InventorySlot(LootItem item, int count = 1)
    {
        this.item  = item;
        this.count = count;
    }

    public bool   CanStack => item != null && item.CanStack;
    public string ItemName => item != null ? item.ItemName : string.Empty;
}

// Serializable snapshot used by SaveSystem and the shared Inventory class.
// A randomized item stores its full ItemRoll (generated = true); a plain prefab
// item stores just its name. JsonUtility never serializes null for a nested
// [Serializable] class, so `generated` is the authoritative discriminator.
[Serializable]
public class InventorySlotSave
{
    public string   itemName;
    public int      count;
    public bool     generated;
    public ItemRoll roll;
}

[Serializable]
public class EquippedLootSave
{
    public EquipSlot slot;
    public string    itemName;
    public bool      generated;
    public ItemRoll  roll;
}

// The player's inventory: a thin singleton wrapper around the shared Inventory
// container, plus the equipped-armor and thrown-weapon slots and gold.
public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    // Player carries up to Inventory.DefaultCapacity (1000) slots.
    readonly Inventory _inventory = new(Inventory.DefaultCapacity);

    // Raised when an Add could not fit everything (inventory full). The argument
    // is the LootItem that overflowed. UI hooks this to show "Inventory full".
    public event Action<LootItem> OnInventoryFull;

    // ── LootItem armor equipment slots ───────────────────────────────────────
    readonly Dictionary<EquipSlot, LootItem> _equippedLoot = new();

    public event Action<EquipSlot, LootItem> OnLootEquipped;
    public event Action<EquipSlot, LootItem> OnLootUnequipped;

    public LootItem GetEquippedLoot(EquipSlot slot) =>
        _equippedLoot.TryGetValue(slot, out var item) ? item : null;

    public IReadOnlyDictionary<EquipSlot, LootItem> GetAllEquippedLoot() => _equippedLoot;

    // Which equipment slot an item goes into, or null if it isn't equippable.
    //   Armor / Accessory → its equipSlot (a shield is Armor with equipSlot OffHand)
    //   Weapon            → MainHand (two-handed / off-hand weapons handled later)
    public static EquipSlot? GetEquipSlot(LootItem item)
    {
        if (item == null) return null;
        return item.itemType switch
        {
            LootItemType.Armor      => item.equipSlot,
            LootItemType.Accessory  => item.equipSlot,
            LootItemType.Weapon     => EquipSlot.MainHand,
            LootItemType.Projectile => IsThrownWeapon(item) ? EquipSlot.MainHand : (EquipSlot?)null,
            _                       => null,
        };
    }

    // Thrown weapons can occupy the Main Hand (then be thrown); arrows/bolts/etc.
    // are ammo for a ranged weapon and aren't hand-equippable.
    public static bool IsThrownWeapon(LootItem item) =>
        item != null && item.itemType == LootItemType.Projectile &&
        (item.projectileType == ProjectileType.ThrowingKnife
         || item.projectileType == ProjectileType.Javelin
         || item.projectileType == ProjectileType.Dart);

    // The four interchangeable ring slots.
    static readonly EquipSlot[] RingSlots =
        { EquipSlot.Ring1, EquipSlot.Ring2, EquipSlot.Ring3, EquipSlot.Ring4 };

    static bool IsRingSlot(EquipSlot slot) =>
        slot == EquipSlot.Ring1 || slot == EquipSlot.Ring2 ||
        slot == EquipSlot.Ring3 || slot == EquipSlot.Ring4;

    // A ring is an Accessory whose base slot is one of the ring slots. Rings can
    // be equipped into ANY free ring slot, not just their authored one.
    public static bool IsRing(LootItem item) =>
        item != null && item.itemType == LootItemType.Accessory && IsRingSlot(item.equipSlot);

    public static bool CanEquipToSlot(LootItem item, EquipSlot slot) =>
        IsRing(item) ? IsRingSlot(slot) : GetEquipSlot(item) == slot;

    // Picks the ring slot to equip into: honour an explicit choice, else the first
    // free ring slot, else fall back to Ring1 (displacing it).
    EquipSlot ResolveRingSlot(EquipSlot? preferred)
    {
        if (preferred.HasValue && IsRingSlot(preferred.Value)) return preferred.Value;
        foreach (var s in RingSlots) if (!_equippedLoot.ContainsKey(s)) return s;
        return EquipSlot.Ring1;
    }

    // Equips an equippable LootItem into its slot. Removes it from inventory,
    // displaces whatever was previously in the slot (returning it to inventory).
    // preferredSlot lets the UI target a specific ring slot (Ring1-4); ignored
    // for non-ring items.
    public bool EquipLootItem(LootItem item, EquipSlot? preferredSlot = null)
    {
        EquipSlot? resolved = GetEquipSlot(item);
        if (resolved == null) return false;
        if (GetCount(item) <= 0)
        {
            Debug.LogWarning($"[Inventory] Cannot equip '{item.ItemName}' — not in inventory.");
            return false;
        }

        // Rings can go into any of the four ring slots.
        EquipSlot slot = IsRing(item) ? ResolveRingSlot(preferredSlot) : resolved.Value;

        // Thrown weapons use the dedicated thrown slot — the stack stays in the
        // inventory and is consumed per throw. Clear any held Main-Hand weapon.
        if (item.itemType == LootItemType.Projectile)
        {
            if (_equippedLoot.TryGetValue(EquipSlot.MainHand, out var heldWeapon))
            {
                _equippedLoot.Remove(EquipSlot.MainHand);
                OnLootUnequipped?.Invoke(EquipSlot.MainHand, heldWeapon);
                AddLootItem(heldWeapon, 1);
            }
            EquipThrown(item);
            return true;
        }

        // Equipping a Main-Hand weapon clears any thrown weapon in that hand.
        if (slot == EquipSlot.MainHand && _equippedThrown != null)
            UnequipThrown();

        // Two-handed rules: a two-hander clears the off-hand; equipping an
        // off-hand item clears a two-hander already in the main hand.
        if (slot == EquipSlot.MainHand && item.isTwoHanded)
            UnequipLootSlot(EquipSlot.OffHand);
        else if (slot == EquipSlot.OffHand)
        {
            var mh = GetEquippedLoot(EquipSlot.MainHand);
            if (mh != null && mh.isTwoHanded) UnequipLootSlot(EquipSlot.MainHand);
        }

        // Remove the item we're equipping first (frees a slot for any displaced
        // occupant to land in).
        RemoveLootItem(item, 1);

        if (_equippedLoot.TryGetValue(slot, out var current))
        {
            _equippedLoot.Remove(slot);
            OnLootUnequipped?.Invoke(slot, current);
            AddLootItem(current, 1);
        }

        _equippedLoot[slot] = item;
        OnLootEquipped?.Invoke(slot, item);
        return true;
    }

    // Unequips the LootItem in the given slot, returning it to inventory.
    public void UnequipLootSlot(EquipSlot slot)
    {
        if (!_equippedLoot.TryGetValue(slot, out var item)) return;
        _equippedLoot.Remove(slot);
        OnLootUnequipped?.Invoke(slot, item);
        AddLootItem(item, 1);
    }

    // ── Equipped-gear stat contribution ───────────────────────────────────────
    // Equipped LootItems feed PlayerStats through the same Flat/Percent channel
    // as buffs and skills. armorValue is treated as a flat Defense bonus.

    public float GearFlat(StatType stat)
    {
        float total = 0f;
        foreach (var item in _equippedLoot.Values)
        {
            if (item == null) continue;
            if (stat == StatType.Defense) total += item.armorValue;
            foreach (var mod in item.statModifiers)
                if (mod.stat == stat && mod.mode == ModifierMode.Flat) total += mod.amount;
        }
        return total;
    }

    public float GearPercent(StatType stat)
    {
        float total = 0f;
        foreach (var item in _equippedLoot.Values)
        {
            if (item == null) continue;
            foreach (var mod in item.statModifiers)
                if (mod.stat == stat && mod.mode == ModifierMode.Percent) total += mod.amount;
        }
        return total;
    }

    // ── Thrown-weapon MainHand slot ───────────────────────────────────────────
    LootItem _equippedThrown;

    public event Action<LootItem> OnThrownEquipChanged;

    public LootItem GetEquippedThrown() => _equippedThrown;

    public void EquipThrown(LootItem item)
    {
        if (item != null && item.itemType != LootItemType.Projectile)
        {
            Debug.LogWarning($"[InventorySystem] Cannot equip non-Projectile item '{item.ItemName}' as thrown weapon.");
            return;
        }
        if (item != null && GetCount(item) <= 0)
        {
            Debug.LogWarning($"[InventorySystem] Cannot equip '{item.ItemName}' — none in inventory.");
            return;
        }
        _equippedThrown = item;
        OnThrownEquipChanged?.Invoke(_equippedThrown);
    }

    public void UnequipThrown()
    {
        _equippedThrown = null;
        OnThrownEquipChanged?.Invoke(null);
    }

    public bool ConsumeThrown()
    {
        if (_equippedThrown == null) return false;
        if (!RemoveLootItem(_equippedThrown, 1)) return false;
        if (GetCount(_equippedThrown) <= 0)
            UnequipThrown();
        return true;
    }

    public bool HasThrownAmmo =>
        _equippedThrown != null && GetCount(_equippedThrown) > 0;

    // ── Gold ──────────────────────────────────────────────────────────────────
    public int Gold { get; private set; }

    public void AddGold(int amount)   { if (amount > 0) Gold += amount; }
    public bool SpendGold(int amount) { if (amount > Gold) return false; Gold -= amount; return true; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Adding / removing ─────────────────────────────────────────────────────

    // Adds up to `count` of `item`. Returns the leftover that did NOT fit
    // (0 = everything fit). Fires OnInventoryFull when something overflows.
    public int AddLootItem(LootItem item, int count = 1)
    {
        if (item == null || count <= 0) return count;

        // Keys are absorbed into the persistent Keyring (one "Keys" token represents
        // them all in the inventory). The ring token itself is added normally.
        if (item.itemType == LootItemType.KeyItem && !item.isKeyRing)
        {
            Keyring.EnsureExists();
            Keyring.Instance.Add(item.ItemName);
            QuestSystem.Instance?.ReportItemAcquired(item.ItemName);   // progress DeliverItem objectives
            return 0;   // fully consumed into the ring
        }

        int leftover = _inventory.Add(item, count);
        if (leftover > 0) OnInventoryFull?.Invoke(item);

        // Progress DeliverItem objectives for each unit actually picked up.
        int added = count - leftover;
        for (int i = 0; i < added; i++) QuestSystem.Instance?.ReportItemAcquired(item.ItemName);

        return leftover;
    }

    // True if the full `count` of `item` could be added right now.
    public bool HasRoomFor(LootItem item, int count = 1) => _inventory.HasRoomFor(item, count);
    public int  RoomFor(LootItem item)                   => _inventory.RoomFor(item);
    public bool IsFull                                    => _inventory.IsFull;

    public bool RemoveLootItem(LootItem item, int count = 1) => _inventory.Remove(item, count);

    public void SortInventory(InventorySortMode mode) => _inventory.Sort(mode);

    // ── Queries ───────────────────────────────────────────────────────────────

    public IReadOnlyList<InventorySlot> GetSlots() => _inventory.Slots;
    public int GetCount(LootItem item)             => _inventory.Count(item);

    // ── Projectile helpers (used by ranged weapon systems) ───────────────────

    public bool HasProjectile(ProjectileType type)
    {
        if (type == ProjectileType.None) return true;
        foreach (var slot in _inventory.Slots)
            if (slot.item != null
                && slot.item.itemType == LootItemType.Projectile
                && slot.item.projectileType == type
                && slot.count > 0)
                return true;
        return false;
    }

    public bool ConsumeProjectile(ProjectileType type)
    {
        if (type == ProjectileType.None) return true;
        foreach (var slot in _inventory.Slots)
        {
            if (slot.item == null
                || slot.item.itemType != LootItemType.Projectile
                || slot.item.projectileType != type) continue;
            return _inventory.Remove(slot.item, 1);
        }
        return false;
    }

    // ── Consumable helpers ────────────────────────────────────────────────────

    public bool UseConsumable(LootItem item, PlayerStats stats)
    {
        if (!RemoveLootItem(item, 1)) return false;
        if (stats == null) return true;

        if (item.restoresHealth)
        {
            float amount = item.healthIsPercent ? stats.MaxHealth * item.healthAmount : item.healthAmount;
            stats.Heal(amount);
        }
        if (item.restoresMana)
        {
            float amount = item.manaIsPercent ? stats.MaxMana * item.manaAmount : item.manaAmount;
            stats.RestoreMana(amount);
        }
        return true;
    }

    // ── Save / load ───────────────────────────────────────────────────────────

    public List<EquippedLootSave> CaptureEquippedLoot()
    {
        var saves = new List<EquippedLootSave>();
        foreach (var kvp in _equippedLoot)
        {
            if (kvp.Value == null) continue;
            if (kvp.Value.IsGenerated)
                saves.Add(new EquippedLootSave { slot = kvp.Key, generated = true, roll = kvp.Value.runtimeRoll });
            else
                saves.Add(new EquippedLootSave { slot = kvp.Key, itemName = kvp.Value.ItemName });
        }
        return saves;
    }

    public void RestoreEquippedLoot(List<EquippedLootSave> saved, LootRegistry registry)
    {
        _equippedLoot.Clear();
        if (saved == null) return;
        foreach (var entry in saved)
        {
            LootItem item = entry.generated
                ? LootItemFactory.Build(entry.roll)
                : registry != null ? registry.FindByName(entry.itemName) : null;
            if (item == null) { Debug.LogWarning($"[Inventory] Saved equipped item not found: {entry.itemName}"); continue; }
            _equippedLoot[entry.slot] = item;
            OnLootEquipped?.Invoke(entry.slot, item);
        }
    }

    // Wipes everything for a fresh New Game (inventory, equipment, thrown, gold).
    public void ClearAll()
    {
        _inventory.Clear();
        _equippedLoot.Clear();
        _equippedThrown = null;
        Gold = 0;
    }

    public List<InventorySlotSave> CaptureState() => _inventory.Capture();

    public void RestoreState(List<InventorySlotSave> saved, GameDatabase database)
    {
        _inventory.Restore(saved, database != null ? database.lootRegistry : null);
    }
}
