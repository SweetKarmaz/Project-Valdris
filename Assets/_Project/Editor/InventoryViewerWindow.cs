using UnityEditor;
using UnityEngine;

// Tools → Valdris → Inventory Viewer
//
// Play-mode debug view of the player's live InventorySystem: stacks, gold,
// equipped armor, and the thrown slot — with controls to add/remove items and
// gold for testing. Read-only outside Play mode (the inventory is runtime-only;
// set the *starting* inventory on the PlayerManager component instead).
public class InventoryViewerWindow : EditorWindow
{
    [MenuItem("Tools/Valdris/Debug/Inventory Viewer")]
    static void Open() => GetWindow<InventoryViewerWindow>("Inventory");

    Vector2 _scroll;
    LootItem _addItem;
    int _addCount = 1;
    int _goldDelta = 100;

    void OnInspectorUpdate() => Repaint();   // keep the live view fresh

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode to view the player's runtime inventory.\n\n" +
                "To set the inventory the player STARTS a new game with, use " +
                "PlayerManager → Starting Inventory.", MessageType.Info);
            return;
        }

        var inv = InventorySystem.Instance;
        if (inv == null) { EditorGUILayout.HelpBox("No InventorySystem in the scene yet.", MessageType.Warning); return; }

        // ── Gold ──
        EditorGUILayout.LabelField($"Gold: {inv.Gold}", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _goldDelta = EditorGUILayout.IntField("Amount", _goldDelta);
            if (GUILayout.Button("+ Gold", GUILayout.Width(70))) inv.AddGold(_goldDelta);
            if (GUILayout.Button("- Gold", GUILayout.Width(70))) inv.SpendGold(_goldDelta);
        }

        EditorGUILayout.Space();

        // ── Add item ──
        EditorGUILayout.LabelField("Add Item", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _addItem  = (LootItem)EditorGUILayout.ObjectField(_addItem, typeof(LootItem), false);
            _addCount = Mathf.Max(1, EditorGUILayout.IntField(_addCount, GUILayout.Width(50)));
            using (new EditorGUI.DisabledScope(_addItem == null))
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    int leftover = inv.AddLootItem(_addItem, _addCount);
                    if (leftover > 0) Debug.LogWarning($"[InventoryViewer] {leftover} didn't fit (inventory full).");
                }
        }

        EditorGUILayout.Space();

        // ── Stacks ──
        var slots = inv.GetSlots();
        EditorGUILayout.LabelField($"Inventory  ({slots.Count} slots)", EditorStyles.boldLabel);

        // Defer mutations until after iteration (RemoveLootItem mutates the list).
        LootItem removeItem = null; int removeCount = 0;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            var slot = slots[i];
            if (slot.item == null) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{slot.ItemName}  ×{slot.count}");
                if (GUILayout.Button("-1", GUILayout.Width(36)))  { removeItem = slot.item; removeCount = 1; }
                if (GUILayout.Button("All", GUILayout.Width(40))) { removeItem = slot.item; removeCount = slot.count; }
            }
        }
        EditorGUILayout.EndScrollView();
        if (removeItem != null) inv.RemoveLootItem(removeItem, removeCount);

        EditorGUILayout.Space();

        // ── Equipped ──
        EditorGUILayout.LabelField("Equipped Armor", EditorStyles.boldLabel);
        EquipSlot? unequip = null;
        foreach (var kvp in inv.GetAllEquippedLoot())
        {
            if (kvp.Value == null) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value.ItemName}");
                if (GUILayout.Button("Unequip", GUILayout.Width(80))) unequip = kvp.Key;
            }
        }
        if (unequip.HasValue) inv.UnequipLootSlot(unequip.Value);

        var thrown = inv.GetEquippedThrown();
        EditorGUILayout.LabelField($"Thrown: {(thrown != null ? thrown.ItemName : "—")}");
    }
}
