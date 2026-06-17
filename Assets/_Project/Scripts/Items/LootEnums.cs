// All enums shared across the loot system.

public enum LootItemType
{
    Weapon,       // Deals damage; has a WeaponCategory
    Armor,        // Worn in an equipment slot; has a defense value
    Accessory,    // Ring, necklace, etc.; mainly stat modifiers
    Consumable,   // Potion, food — used up on consumption; stackable
    Projectile,   // Arrow, bolt, javelin, etc.; stackable; required by ranged weapons
    Material,     // Crafting ingredient, ore, herb
    KeyItem,      // Puzzle / story item; can't be dropped or sold
    QuestItem,    // Given / removed by the quest system
    Misc,         // Junk, decorative, vendor fodder
}

public enum WeaponCategory
{
    Melee,    // Sword, axe, dagger, staff — used in melee range
    Ranged,   // Bow, crossbow, thrown — uses a ProjectileType from inventory
}

// Medieval projectile types. Ranged weapons declare which type they require.
// The inventory system checks for a matching stack before allowing a shot.
public enum ProjectileType
{
    None,           // Not a projectile / no ammo needed (melee)
    Arrow,          // Standard bow ammunition
    Bolt,           // Crossbow ammunition
    Javelin,        // Heavy thrown spear; one-shot per item
    ThrowingKnife,  // Light thrown blade; stackable
    SlingStone,     // Sling / slingshot ammunition; stackable
    Dart,           // Blowpipe or hand-thrown dart; stackable
    FireArrow,      // Arrow with burning head — specialised arrow variant
    PoisonBolt,     // Bolt treated with poison — specialised bolt variant
}

// StatType and StatModifier are defined in BuffData.cs and shared across the whole project.
// LootItem.statModifiers uses those existing types directly.
