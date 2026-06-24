using System;
using System.Collections.Generic;

// A generated (randomized) item's data. This is the persistable record: it
// captures everything the generator decided so a unique item can be rebuilt
// after save/load via LootItemFactory. The base prefab (resolved by name) only
// supplies the model, icon, and fixed type/grip data.
//
// All fields are JsonUtility-friendly (no UnityEngine.Object references), so
// generated items round-trip through the save system.
[Serializable]
public class ItemRoll
{
    public string     basePrefabName;   // resolves the base LootItem in LootRegistry
    public ItemRarity rarity;
    public string     displayName;
    public string     flavorText;

    public bool        isWeapon;        // true → weaponDamage applies; false → armorValue
    public bool        isTwoHanded;
    public GenCategory category;        // used to assign WeaponType during build
    public float weaponDamage;
    public float armorValue;
    public int   goldValue = 1;

    public List<StatModifier> statModifiers = new();
    public List<RolledOnHit>  bonusDamage   = new(); // weapons only (elemental riders)
}

// A generated elemental damage rider: bonus typed damage with a guaranteed proc
// and no debuff (debuffs are authored content, not randomly generated).
[Serializable]
public class RolledOnHit
{
    public DamageType type;
    public float      damage;
}
