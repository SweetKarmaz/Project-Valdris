using System.Collections.Generic;
using UnityEngine;

// Drives the player's visible character model based on equipped armor.
// Listens to InventorySystem's LootItem equip events.
//
// Group names in MeshGroupOverride use canonical keys (e.g. "Torso",
// "HandRight", "Back_Attachment"). The GroupNameMap table below translates
// these to the actual Transform names inside Chr_Npc_Base, inserting the
// correct Male_/Female_ prefix for gendered groups automatically.
//
// Index conventions (matching the Chr_Npc_Base hierarchy):
//   0-indexed groups:  Torso, Arms, Hands, Legs, Hips  (child 0 = variant _00)
//   1-indexed groups:  Back_Attachment, Shoulder_Attachment_*, HeadCoverings_*
//                      (child 0 = variant _01, so stored index = file number - 1)
[DisallowMultipleComponent]
public class PlayerAppearanceComponent : MonoBehaviour
{
    [HideInInspector] public Transform modelRoot;
    [HideInInspector] public bool      isFemale;

    [Header("Hand bones (for weapon/shield attachment)")]
    [Tooltip("Right-hand bone name on the rig (MainHand). Synty: 'Hand_R'.")]
    public string rightHandBoneName = "Hand_R";
    [Tooltip("Left-hand bone name on the rig (OffHand). Synty: 'Hand_L'.")]
    public string leftHandBoneName  = "Hand_L";

    // Per-slot record of which mesh children were activated, for clean reversal.
    readonly Dictionary<EquipSlot, List<AppearanceSlotSave>> _slotMeshes = new();

    // The default (naked-body) active child index per mesh group, captured the
    // first time a group is modified, so unequipping restores it instead of
    // leaving the body part invisible.
    readonly Dictionary<string, int> _groupDefault = new();

    // Instantiated weapon/shield models attached to hand bones, by slot, plus the
    // source item (for re-applying grip offsets live).
    readonly Dictionary<EquipSlot, GameObject> _attached  = new();
    readonly Dictionary<EquipSlot, LootItem>   _heldItems = new();

    // ── Group name lookup ─────────────────────────────────────────────────────
    // Maps canonical key → (male group name, female group name) as they appear
    // in the Chr_Npc_Base Transform hierarchy.

    static readonly Dictionary<string, (string male, string female)> GroupNameMap =
        new Dictionary<string, (string, string)>
    {
        { "Torso",              ("Male_03_Torso",          "Female_03_Torso") },
        { "ArmUpperRight",      ("Male_04_Arm_Upper_Right","Female_04_Arm_Upper_Right") },
        { "ArmUpperLeft",       ("Male_05_Arm_Upper_Left", "Female_05_Arm_Upper_Left") },
        { "ArmLowerRight",      ("Male_06_Arm_Lower_Right","Female_06_Arm_Lower_Right") },
        { "ArmLowerLeft",       ("Male_07_Arm_Lower_Left", "Female_07_Arm_Lower_Left") },
        { "HandRight",          ("Male_08_Hand_Right",     "Female_08_Hand_Right") },
        { "HandLeft",           ("Male_09_Hand_Left",      "Female_09_Hand_Left") },
        { "Hips",               ("Male_10_Hips",           "Female_10_Hips") },
        { "LegRight",           ("Male_11_Leg_Right",      "Female_11_Leg_Right") },
        { "LegLeft",            ("Male_12_Leg_Left",       "Female_12_Leg_Left") },
        // Gender-neutral — both columns identical
        { "HeadCoverings_No_Hair",       ("HeadCoverings_No_Hair",       "HeadCoverings_No_Hair") },
        { "HeadCoverings_No_FacialHair", ("HeadCoverings_No_FacialHair", "HeadCoverings_No_FacialHair") },
        { "HeadCoverings_Base_Hair",     ("HeadCoverings_Base_Hair",     "HeadCoverings_Base_Hair") },
        { "Back_Attachment",             ("Back_Attachment",              "Back_Attachment") },
        { "Shoulder_Attachment_R",       ("Shoulder_Attachment_R",        "Shoulder_Attachment_R") },
        { "Shoulder_Attachment_L",       ("Shoulder_Attachment_L",        "Shoulder_Attachment_L") },
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (modelRoot == null)
        {
            var anim = GetComponentInChildren<Animator>(true);
            if (anim != null) modelRoot = anim.transform.parent ?? anim.transform;
        }
        if (modelRoot != null)
            isFemale = modelRoot.Find("Female_03_Torso") != null;

        // Subscribe to LootItem armor equip events. InventorySystem may not
        // exist yet (DontDestroyOnLoad order), so also handle late connection.
        if (InventorySystem.Instance != null) SubscribeToInventory();
    }

    // Called by InventorySystem if it initialises after this component.
    public void SubscribeToInventory()
    {
        InventorySystem.Instance.OnLootEquipped    += OnLootEquipped;
        InventorySystem.Instance.OnLootUnequipped  += OnLootUnequipped;
        InventorySystem.Instance.OnThrownEquipChanged += OnThrownChanged;

        // Re-apply any already-equipped items so the model reflects saved state.
        foreach (var kvp in InventorySystem.Instance.GetAllEquippedLoot())
            OnLootEquipped(kvp.Key, kvp.Value);
        OnThrownChanged(InventorySystem.Instance.GetEquippedThrown());
    }

    void OnDestroy()
    {
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnLootEquipped    -= OnLootEquipped;
            InventorySystem.Instance.OnLootUnequipped  -= OnLootUnequipped;
            InventorySystem.Instance.OnThrownEquipChanged -= OnThrownChanged;
        }
    }

    // ── LootItem event handlers ───────────────────────────────────────────────

    static bool IsHandSlot(EquipSlot slot) => slot == EquipSlot.MainHand || slot == EquipSlot.OffHand;

    void OnLootEquipped(EquipSlot slot, LootItem item)
    {
        if (item == null) return;
        Debug.Log($"[PlayerAppearance] OnLootEquipped slot={slot} item='{item.ItemName}' " +
                  $"type={item.itemType} handSlot={IsHandSlot(slot)}");

        // Weapons / shields attach a held model to a hand bone.
        if (IsHandSlot(slot)) { AttachEquipVisual(slot, item); return; }

        if (!IsMeshSlot(slot)) return;
        DeactivateSlot(slot);
        if (item.meshOverrides != null && item.meshOverrides.Count > 0)
            ApplyMeshOverrides(slot, item.meshOverrides);
        else
            Debug.Log($"[PlayerAppearance] '{item.ItemName}' has no meshOverrides (slot {slot}).");
    }

    void OnLootUnequipped(EquipSlot slot, LootItem item)
    {
        if (IsHandSlot(slot)) { DetachEquipVisual(slot); return; }
        if (IsMeshSlot(slot)) DeactivateSlot(slot);
    }

    // Throwing weapons live in the thrown slot but show in the right hand.
    void OnThrownChanged(LootItem thrown)
    {
        if (thrown != null) AttachEquipVisual(EquipSlot.MainHand, thrown);
        else if (InventorySystem.Instance?.GetEquippedLoot(EquipSlot.MainHand) == null)
            DetachEquipVisual(EquipSlot.MainHand);
    }

    // ── Weapon / shield attachment ─────────────────────────────────────────────

    void AttachEquipVisual(EquipSlot slot, LootItem item)
    {
        DetachEquipVisual(slot);
        if (item == null || modelRoot == null) return;

        var mount = HeldItemVisual.Attach(modelRoot, item, slot, rightHandBoneName, leftHandBoneName);
        if (mount == null) return;

        _attached[slot]  = mount;
        _heldItems[slot] = item;
    }

    void DetachEquipVisual(EquipSlot slot)
    {
        if (_attached.TryGetValue(slot, out var go))
        {
            if (go != null) Destroy(go);
            _attached.Remove(slot);
        }
        _heldItems.Remove(slot);
    }

    // Re-apply grips each frame so category grip values tune live in play mode.
    void LateUpdate()
    {
        if (_attached.Count == 0) return;
        foreach (var kvp in _attached)
        {
            var mount = kvp.Value;
            if (mount == null || mount.transform.childCount == 0) continue;
            if (_heldItems.TryGetValue(kvp.Key, out var item))
                HeldItemVisual.ApplyGrip(mount.transform.GetChild(0), item, kvp.Key);
        }
    }

    // ── Core mesh logic ───────────────────────────────────────────────────────

    void ApplyMeshOverrides(EquipSlot slot, List<MeshGroupOverride> overrides)
    {
        if (overrides == null || overrides.Count == 0 || modelRoot == null) return;

        var activated = new List<AppearanceSlotSave>();
        foreach (var entry in overrides)
        {
            if (string.IsNullOrEmpty(entry.groupName)) continue;
            string resolved = ResolveGroupName(entry.groupName);
            if (resolved == null)
            {
                Debug.LogWarning($"[PlayerAppearance] Unknown group key '{entry.groupName}'. " +
                                 "Check GroupNameMap in PlayerAppearanceComponent.");
                continue;
            }

            if (entry.index < 0)
            {
                ClearMeshGroup(resolved);
                activated.Add(new AppearanceSlotSave { groupName = resolved, index = -1 });
            }
            else
            {
                var save = SetMeshGroup(resolved, entry.index);
                if (save != null) activated.Add(save);
            }
        }

        if (activated.Count > 0)
            _slotMeshes[slot] = activated;
    }

    void DeactivateSlot(EquipSlot slot)
    {
        if (!_slotMeshes.TryGetValue(slot, out var saves)) return;
        // Restore each touched group to its default (naked-body) variant rather
        // than leaving it empty — otherwise the body part vanishes on unequip.
        foreach (var save in saves)
            RestoreGroupDefault(save.groupName);
        _slotMeshes.Remove(slot);
    }

    // Records the active child index a group had before any armor modified it.
    void EnsureDefaultRecorded(string groupName)
    {
        if (_groupDefault.ContainsKey(groupName)) return;
        var group = FindGroup(groupName);
        int def = -1;
        if (group != null)
            for (int i = 0; i < group.childCount; i++)
                if (group.GetChild(i).gameObject.activeSelf) { def = i; break; }
        _groupDefault[groupName] = def;
    }

    void RestoreGroupDefault(string groupName)
    {
        var group = FindGroup(groupName);
        if (group == null) return;
        int def = _groupDefault.TryGetValue(groupName, out var d) ? d : -1;
        for (int i = 0; i < group.childCount; i++)
            group.GetChild(i).gameObject.SetActive(i == def);
    }

    // Translates a canonical key ("Torso", "Back_Attachment", etc.) to the
    // actual Transform name inside Chr_Npc_Base. Falls back to the raw name
    // so hand-authored overrides using the full name still work.
    string ResolveGroupName(string key)
    {
        if (GroupNameMap.TryGetValue(key, out var pair))
            return isFemale ? pair.female : pair.male;
        // Fallback: treat it as a literal group name.
        if (FindGroup(key) != null) return key;
        return null;
    }

    // Deep search for a named mesh-group node anywhere under the model root.
    // (Transform.Find only checks direct children — Synty group nodes are nested,
    // so a shallow Find silently fails to apply mesh overrides.) Cached; stale
    // entries from a swapped model resolve to null and are re-found.
    readonly Dictionary<string, Transform> _groupCache = new();
    Transform FindGroup(string groupName)
    {
        if (modelRoot == null || string.IsNullOrEmpty(groupName)) return null;
        if (_groupCache.TryGetValue(groupName, out var cached) && cached != null) return cached;
        foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
            if (t.name == groupName) { _groupCache[groupName] = t; return t; }
        return null;
    }

    static bool IsMeshSlot(EquipSlot slot) => slot switch
    {
        EquipSlot.Necklace => false,
        EquipSlot.Ring1    => false,
        EquipSlot.Ring2    => false,
        EquipSlot.Ring3    => false,
        EquipSlot.Ring4    => false,
        _                  => true,
    };

    AppearanceSlotSave SetMeshGroup(string groupName, int childIndex)
    {
        EnsureDefaultRecorded(groupName);
        Transform group = FindGroup(groupName);
        if (group == null) return null;
        int count = group.childCount;
        if (childIndex >= count)
        {
            Debug.LogWarning($"[PlayerAppearance] Index {childIndex} out of range for " +
                             $"'{groupName}' ({count} children).");
            return null;
        }
        for (int i = 0; i < count; i++)
            group.GetChild(i).gameObject.SetActive(i == childIndex);
        return new AppearanceSlotSave { groupName = groupName, index = childIndex };
    }

    void ClearMeshGroup(string groupName)
    {
        EnsureDefaultRecorded(groupName);
        Transform group = FindGroup(groupName);
        if (group == null) return;
        for (int i = 0; i < group.childCount; i++)
            group.GetChild(i).gameObject.SetActive(false);
    }
}
