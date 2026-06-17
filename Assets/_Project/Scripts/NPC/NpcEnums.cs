public enum NpcFaction
{
    None,
    Aldenmoor,
    Veranthos,
    DraskConfederacy,
    Solvane,
    AshfeldRemnants
}

// Mirrors NpcFaction with an additional Everyone option for reaction triggers.
public enum FactionFilter
{
    None,
    Aldenmoor,
    Veranthos,
    DraskConfederacy,
    Solvane,
    AshfeldRemnants,
    Everyone
}

public enum NpcBehaviorType
{
    Stationary,
    Wander,
    Patrol,
    Follow,
    Flee
}

public enum WitnessedKillReaction
{
    None,
    Flee,
    Attack
}

public enum AttackType
{
    Melee,
    Ranged,
    Thrown,  // uses a Projectile LootItem stacked in inventory; consumed one-per-throw
    Spells
}

// The archetype class of an NPC. Used for AI behaviour, dialogue context,
// and — on randomized prefabs — automatically driving NpcAppearanceComponent.
// QuestGiver is reserved for hand-crafted named characters; do not use on
// procedurally randomized NPCs.
public enum NpcClass
{
    // ── Civilian ──────────────────────────────────────────────────────────────
    Peasant,
    Farmer,
    Beggar,
    Merchant,
    Innkeeper,
    Scholar,
    Bard,
    Blacksmith,

    // ── Spellcasters ──────────────────────────────────────────────────────────
    Mage,
    Priest,
    Cultist,
    Shaman,

    // ── Light Armour ──────────────────────────────────────────────────────────
    Archer,
    Thief,
    Assassin,
    TownGuard,

    // ── Medium Armour ─────────────────────────────────────────────────────────
    Soldier,
    Bandit,
    Ranger,
    CityGuard,

    // ── Heavy Armour ──────────────────────────────────────────────────────────
    Knight,
    Paladin,

    // ── Royalty / Nobility ────────────────────────────────────────────────────
    King,
    Queen,
    Noble,

    // ── Tribal ────────────────────────────────────────────────────────────────
    Barbarian,

    // ── Special — reserved for hand-crafted named characters ─────────────────
    QuestGiver,
}
