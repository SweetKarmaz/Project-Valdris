using System.Collections.Generic;
using UnityEngine;

// Place this component on any prefab in Assets/_Project/Prefabs/Loot/.
// The item name is automatically pulled from the prefab's name; override it
// via displayNameOverride only if you need it to differ (e.g. "Rusty Sword"
// on a prefab named "Sword_Iron_01").
//
// LootRegistryBuilder (Editor) scans the Loot folder and populates
// LootRegistry whenever the folder changes, so this item is available in
// every random loot table and manual item picker without any extra setup.
public class LootItem : MonoBehaviour
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Displayed name. If blank the prefab's asset name is used.")]
    public string displayNameOverride;

    public ItemRarity rarity = ItemRarity.Common;
    public Sprite     icon;

    [TextArea(2, 5)]
    [Tooltip("Flavour or lore text shown in the item detail panel.")]
    public string flavorText;

    [Header("Economy")]
    public int   goldValue   = 1;
    [Tooltip("Can multiple copies stack in one inventory slot? " +
             "Automatically true for Projectile and Consumable types.")]
    public bool  isStackable = false;
    [Range(1, 999)]
    public int   maxStack    = 999;

    // True if this item type always stacks regardless of the isStackable toggle.
    public bool AlwaysStacks =>
        itemType == LootItemType.Projectile || itemType == LootItemType.Consumable;
    public bool CanStack => isStackable || AlwaysStacks;

    [Header("Loot Table")]
    [Range(0f, 1f)]
    [Tooltip("Default drop probability when this item appears in a random loot table.")]
    public float defaultDropChance = 1f;

    // ── Classification ────────────────────────────────────────────────────────

    [Header("Item Type")]
    public LootItemType itemType = LootItemType.Misc;

    [Tooltip("The single 'Keys' token item. Keys loot into the Keyring system; this token represents them in the inventory and is never routed/absorbed.")]
    public bool isKeyRing = false;

    // ── Weapon ────────────────────────────────────────────────────────────────
    // Shown / relevant when itemType == Weapon.
    // NpcController uses WeaponCategory to resolve attack-priority order.

    [Header("Weapon  (itemType = Weapon)")]
    public WeaponCategory weaponCategory = WeaponCategory.Melee;
    [Tooltip("Base damage added to the wielder's attack damage.")]
    public float weaponDamage;
    [Tooltip("Attack range in world units.")]
    public float weaponRange = 1.5f;
    [Tooltip("Modifier to attack speed (e.g. 0.2 = 20% faster, -0.1 = 10% slower).")]
    public float attackSpeedModifier;
    [Tooltip("Ranged weapons only — the projectile type that must be in inventory to fire. " +
             "Set to None for melee weapons.")]
    public ProjectileType requiredProjectile = ProjectileType.None;

    [Tooltip("Seconds the player must wait between shots. " +
             "A short bow redraws in ~0.8s; a crossbow can take 3–5s.")]
    [Min(0.1f)]
    public float reloadTime = 1f;

    [Tooltip("Two-handed: occupies both hands. Equipping clears the off-hand; the " +
             "character will hold it with both hands once two-handed posing is added.")]
    public bool isTwoHanded;

    [Header("Held Visual  (Weapon / Shield — how it sits in the hand)")]
    [Tooltip("Per-item position offset. Leave (0,0,0) to use the category default " +
             "(set on PlayerManager → Weapon Grip).")]
    public Vector3 gripPositionOffset;
    [Tooltip("Per-item rotation (Euler) offset. Leave (0,0,0) to use the category default.")]
    public Vector3 gripRotationOffset;
    [Tooltip("Local scale when held. (0,0,0) is treated as (1,1,1).")]
    public Vector3 gripScale = Vector3.one;
    [Tooltip("Staff/spear: grip the MIDDLE of the weapon (held in both hands at the centre) " +
             "instead of the end.")]
    public bool gripAtCenter;

    [Tooltip("Half-angle of the accuracy cone in degrees. " +
             "0 = laser-perfect; 2–3 = good bow; 8+ = sling or untrained throw.")]
    [Range(0f, 15f)]
    public float spread = 2f;

    // ── Projectile ────────────────────────────────────────────────────────────
    // Shown / relevant when itemType == Projectile.
    // Projectiles are stackable and consumed one-per-shot.

    [Header("Projectile  (itemType = Projectile)")]
    [Tooltip("What type of projectile this is. Must match the firing weapon's requiredProjectile.")]
    public ProjectileType projectileType = ProjectileType.Arrow;

    [Tooltip("Flat damage dealt to the target on hit, before any attacker stat bonuses.")]
    public float projectileDamage = 10f;

    [Tooltip("Extra damage added on top of the firing weapon's base damage.")]
    public float projectileDamageBonus;

    [Tooltip("Units per second the projectile travels after being fired.")]
    public float projectileSpeed = 30f;

    [Tooltip("How much gravity pulls this projectile downward (0 = laser-straight, " +
             "1 = full gravity, 0.3 = light arc for arrows).")]
    [Range(0f, 1f)]
    public float projectileGravityScale = 0.3f;

    [Tooltip("Maximum distance in world units before the projectile despawns without hitting anything.")]
    public float projectileRange = 60f;

    [Tooltip("If true, the projectile passes through the first target and can hit enemies behind it " +
             "(e.g. a javelin that skewers multiple foes).")]
    public bool  projectilePiercing;

    [Tooltip("If true, the projectile sticks into whatever it hits (arrows, bolts, javelins). " +
             "If false it bounces or falls away (sling stones, darts).")]
    public bool  embedOnHit = true;

    [Tooltip("Seconds before an embedded projectile disappears from the world. " +
             "Has no effect when embedOnHit is false.")]
    public float despawnDelay = 8f;

    [Tooltip("Thrown weapons only (ThrowingKnife, Javelin, Dart, etc.) — seconds the character " +
             "must wait between throws. Arrow/bolt ammo does NOT need this; their firing weapon " +
             "carries the reload time instead.")]
    [Min(0.1f)]
    public float throwCooldown = 0.8f;

    [Tooltip("Optional stat modifiers applied to the target on hit (e.g. a poison bolt that " +
             "reduces the target's move speed for a few seconds). Leave empty for plain damage.")]
    public List<StatModifier> onHitModifiers = new();

    // ── Armor ─────────────────────────────────────────────────────────────────

    [Header("Armor  (itemType = Armor)")]
    [Tooltip("Which equipment slot this piece occupies when equipped.")]
    public EquipSlot equipSlot = EquipSlot.Chest;

    [Tooltip("Flat armor points added when equipped.")]
    public float armorValue;

    [Tooltip("Synty mesh groups to activate on the character model when this armor is equipped. " +
             "Use the canonical group key (e.g. 'Torso', 'HandRight', 'Back_Attachment'). " +
             "Multi-piece items like Chest have 5 entries; single-piece items have 1. " +
             "Index -1 clears the group (shows nothing).")]
    public List<MeshGroupOverride> meshOverrides = new();

    // ── On-hit effects ─────────────────────────────────────────────────────────
    // Elemental riders added to a weapon/projectile's hit: bonus typed damage
    // and/or a chance to apply a debuff (e.g. a flaming sword adds fire damage +
    // a Burning DoT). The weapon's main damage is Physical (weaponDamage).

    [Header("On-Hit Effects (elemental riders)")]
    public List<OnHitEffect> onHitEffects = new();

    // ── Stat modifiers ────────────────────────────────────────────────────────
    // Applied when the item is equipped; removed when unequipped.
    // Works for any item type — a ring, a sword, a pair of boots, etc.

    [Header("Stat Modifiers")]
    public List<StatModifier> statModifiers = new();

    // ── Consumable ────────────────────────────────────────────────────────────

    [Header("Consumable  (itemType = Consumable)")]

    [Tooltip("Restore health on use.")]
    public bool  restoresHealth;
    [Tooltip("Flat HP restored, or percentage of MaxHealth if healthIsPercent is true.")]
    public float healthAmount;
    [Tooltip("If true, healthAmount is treated as a fraction of MaxHealth (e.g. 0.5 = 50%).")]
    public bool  healthIsPercent;

    [Tooltip("Restore mana on use.")]
    public bool  restoresMana;
    [Tooltip("Flat mana restored, or percentage of MaxMana if manaIsPercent is true.")]
    public float manaAmount;
    [Tooltip("If true, manaAmount is treated as a fraction of MaxMana.")]
    public bool  manaIsPercent;

    [Tooltip("Remove this item from inventory after it is used (true for potions, etc.).")]
    public bool  consumedOnUse = true;

    // ── Runtime properties ────────────────────────────────────────────────────

    // Set on runtime-generated (randomized) clones built by LootItemFactory.
    // Null on plain prefab assets. When present, the inventory/save system stores
    // this roll instead of the item name so the unique item survives save/load.
    [System.NonSerialized] public ItemRoll runtimeRoll;

    public bool IsGenerated => runtimeRoll != null;

    // The canonical display name: override → prefab asset name.
    public string ItemName =>
        string.IsNullOrWhiteSpace(displayNameOverride) ? gameObject.name : displayNameOverride;

    // Convenience: true for any item that goes in a weapon slot.
    public bool IsWeapon => itemType == LootItemType.Weapon;

    // Convenience for NpcController attack-priority resolution.
    public bool IsMeleeWeapon  => IsWeapon && weaponCategory == WeaponCategory.Melee;
    public bool IsRangedWeapon => IsWeapon && weaponCategory == WeaponCategory.Ranged;
}

// StatModifier and StatType are defined in BuffData.cs — used here directly.
