using System;
using System.Collections.Generic;
using UnityEngine;

// State snapshot for a single NPC. The id should be globally unique across the
// whole game (e.g. "greyspire_prisoner_1") so a character cannot appear in two
// scene saves at once. SceneStateManager tracks NPCs dynamically — call
// UnregisterNPC() when a story beat removes a character permanently.
[Serializable]
public class SavedNPCState
{
    public string id;           // globally unique, e.g. "greyspire_prisoner_1"
    public string prefabName;   // source prefab asset name, for re-instantiation

    public Vector3 position;    // NPC root world position
    public float   yRotation;

    public bool  isAlive;
    public float currentHealth;
    public float maxHealth;

    // When a character dies, TriggerDeath() enables root motion on the model
    // child, which shifts its local transform as the death animation plays.
    // We save that offset here so RestoreFromState() can put the body back
    // in the right pose without replaying the animation.
    public bool    hasModelOffset;           // true = fields below are valid
    public Vector3 modelLocalPosition;
    public Vector3 modelLocalRotationEuler;

    // Loot / inventory
    public bool                    lootRolled;     // true = corpse loot already generated
    public bool                    hasBeenLooted;  // true = corpse emptied
    public int                     remainingGold;
    public List<InventorySlotSave> inventory = new();

    // Auto-generated starting gear (rolled once on first spawn, then persisted so
    // the same weapon is restored on revisit rather than re-randomized).
    public bool     gearGenerated;
    public ItemRoll generatedWeapon;   // null/empty basePrefabName = none
    public int      generatedArrows;   // arrows granted alongside a generated bow

    // Merchant (only when the NPC has a Merchant component)
    public bool                    isMerchant;
    public int                     merchantGold;
    public List<InventorySlotSave> merchantStock = new();

    // Appearance — saved after the first randomization so the look never changes.
    // Each entry is the child index that was activated within its named mesh group.
    public bool                    appearanceLocked;
    public List<AppearanceSlotSave> appearanceSlots = new();
}

// State snapshot for a saveable prop (position, rotation, active state).
// Attach SaveableProp to any GameObject you want the scene state to track.
[Serializable]
public class SavedPropState
{
    public string id;           // set by SaveableProp.propId in the Inspector
    public bool   isActive;
    public Vector3 position;
    public Vector3 rotationEuler;
    public Vector3 localScale;
}

// Loot state for a single interactable container in the scene.
// Saved once loot is generated; updated as the player takes items.
[Serializable]
public class SavedLootState
{
    public string      propId;
    public bool        lootGenerated;   // true = loot was rolled, don't re-roll
    public List<string> itemNames    = new();
    public List<int>    itemQuantities = new();
    public int         remainingGold;
}

// State for a LootContainer (placed chest/sack or a runtime-spawned dropped bag).
// Placed containers are matched back to the scene object by containerId; dropped
// containers are re-instantiated from GameDatabase.droppedBagPrefab.
[Serializable]
public class SavedContainerState
{
    public string containerId;
    public bool   rolled;        // contents already generated
    public bool   isDropped;     // runtime overflow bag (re-instantiated on load)
    public string prefabName;    // informational
    public Vector3 position;
    public float   yRotation;
    public int     gold;
    public List<InventorySlotSave> items = new();
}

// State for a Door InteractableProp: whether it's been unlocked and left open.
[Serializable]
public class SavedDoorState
{
    public string propId;
    public bool   unlocked;
    public bool   open;
}

// One activated mesh slot — the group name and the child index that was enabled.
[Serializable]
public class AppearanceSlotSave
{
    public string groupName;  // e.g. "Male_03_Torso"
    public int    index;      // child index within that group
}

// Full per-scene runtime save written to disk (PersistentDataPath/Scenes/<scene>.json).
// One file per scene; updated whenever the player saves or the scene is exited.
[Serializable]
public class SceneStateSave
{
    // True until the scene has been visited at least once in this playthrough.
    // On first visit the scene spawns from scratch; subsequent visits restore.
    public bool firstVisit = true;

    public List<SavedNPCState>       npcs           = new();
    public List<SavedPropState>      props          = new();
    public List<SavedLootState>      lootContainers = new();
    public List<SavedContainerState> containers     = new();   // new LootContainer system
    public List<SavedDoorState>      doors          = new();
}
