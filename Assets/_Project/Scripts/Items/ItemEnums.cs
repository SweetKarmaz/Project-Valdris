using System;
using System.Collections.Generic;
using UnityEngine;

// Core item enums and structs shared across the whole project.
// These used to live in the now-removed ItemData.cs / GearData.cs (legacy item
// system). They are kept here because LootItem and the equipment slots depend
// on them.

// Item quality tiers. Used by LootItem.rarity and loot/colour systems.
// Serialized as ints in assets — only append, never reorder.
// None is appended LAST (= 5). On an NPC's lootRarity it means "generate no auto
// loot/weapon — only the items placed by hand". Items are never authored as None.
public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary, None }

// The concrete equipment slots on a humanoid character (player or NPC).
// Mesh-visible slots match the Synty PolygonFantasyHeroCharacters rig.
// Stat-only slots (Necklace, Ring*) have no associated character mesh.
// Serialized as ints in assets and saves — only append, never reorder.
public enum EquipSlot
{
    Head,        // helmet — HeadCoverings mesh group
    Necklace,    // stat-only, no mesh
    Shoulders,   // ShoulderAttachment L+R mesh group
    Chest,       // Torso mesh group
    Hands,       // HandLeft + HandRight mesh group
    Legs,        // LegLeft + LegRight mesh group
    Back,        // BackAttachment mesh group (capes, wings, quivers)
    Hips,        // HipsAttachment mesh group (belts, pouches, skirts)
    MainHand,    // weapon socket (right hand bone)
    OffHand,     // shield / off-hand weapon socket (left hand bone)
    Ring1,       // stat-only, no mesh
    Ring2,       // stat-only, no mesh
    Ring3,       // stat-only, no mesh
    Ring4,       // stat-only, no mesh
}

// One Synty mesh group activation entry for a piece of armor.
// groupName is the canonical key (e.g. "Torso", "HandRight", "Back_Attachment");
// PlayerAppearanceComponent maps it to the actual Male_/Female_ hierarchy name.
// index -1 means "clear this group" (show nothing — e.g. bare arms).
[Serializable]
public struct MeshGroupOverride
{
    [Tooltip("Synty mesh group key, e.g. 'Torso', 'HandRight', 'Back_Attachment'.")]
    public string groupName;

    [Tooltip("Child index to activate within the group. -1 clears the group (shows nothing).")]
    public int    index;
}
