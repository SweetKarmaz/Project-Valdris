using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// Searchable dropdown for any LootItem field in the Inspector.
// Pulls the list from LootRegistry — same asset the builder auto-populates.
// Typing in the search field filters by item name in real time.

class LootItemAdvancedDropdown : AdvancedDropdown
{
    public System.Action<LootItem> OnSelected;
    readonly List<LootItem> _items;

    public LootItemAdvancedDropdown(AdvancedDropdownState state, List<LootItem> items)
        : base(state)
    {
        _items      = items;
        minimumSize = new Vector2(320f, 300f);
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        var root = new AdvancedDropdownItem("Loot Items");
        root.AddChild(new LootDropdownItem(null, "(None)"));

        // Group by type for readability when the list is long.
        var byType = new Dictionary<LootItemType, AdvancedDropdownItem>();
        foreach (LootItemType t in System.Enum.GetValues(typeof(LootItemType)))
        {
            var group = new AdvancedDropdownItem(t.ToString());
            byType[t] = group;
        }

        foreach (var item in _items)
        {
            if (item == null) continue;
            var entry = new LootDropdownItem(item, item.ItemName);
            byType[item.itemType].AddChild(entry);
        }

        foreach (var group in byType.Values)
            if (group.children != null) root.AddChild(group);

        return root;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (item is LootDropdownItem li) OnSelected?.Invoke(li.Item);
    }

    class LootDropdownItem : AdvancedDropdownItem
    {
        public LootItem Item { get; }
        public LootDropdownItem(LootItem item, string label) : base(label) => Item = item;
    }
}

[CustomPropertyDrawer(typeof(LootItem))]
class LootItemPropertyDrawer : PropertyDrawer
{
    static List<LootItem>  _cached;
    static double          _cacheTime;
    const  double          CacheTtl = 5.0;

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);

        var labelRect  = new Rect(pos.x, pos.y, EditorGUIUtility.labelWidth, pos.height);
        var buttonRect = new Rect(pos.x + EditorGUIUtility.labelWidth, pos.y,
                                  pos.width - EditorGUIUtility.labelWidth, pos.height);

        EditorGUI.LabelField(labelRect, label);

        var current   = prop.objectReferenceValue as LootItem;
        string btnTxt = current != null ? current.ItemName : "— select a loot item —";

        if (GUI.Button(buttonRect, btnTxt, EditorStyles.popup))
        {
            var items = GetCached();
            var dd    = new LootItemAdvancedDropdown(new AdvancedDropdownState(), items);
            dd.OnSelected += chosen =>
            {
                prop.objectReferenceValue = chosen;
                prop.serializedObject.ApplyModifiedProperties();
            };
            dd.Show(buttonRect);
        }

        EditorGUI.EndProperty();
    }

    static List<LootItem> GetCached()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_cached != null && now - _cacheTime < CacheTtl) return _cached;

        _cached = new List<LootItem>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab",
            new[] { "Assets/_Project/Prefabs/Loot" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go      = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var loot = go.GetComponent<LootItem>();
            if (loot != null) _cached.Add(loot);
        }

        _cached.Sort((a, b) =>
            string.Compare(a.ItemName, b.ItemName, System.StringComparison.OrdinalIgnoreCase));

        _cacheTime = now;
        return _cached;
    }
}
