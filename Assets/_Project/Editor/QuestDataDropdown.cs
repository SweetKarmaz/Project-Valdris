using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// ── Searchable dropdown ───────────────────────────────────────────────────────

class QuestDataDropdown : AdvancedDropdown
{
    public System.Action<QuestData> OnSelected;

    readonly List<QuestData> _quests;

    public QuestDataDropdown(AdvancedDropdownState state, List<QuestData> quests)
        : base(state)
    {
        _quests          = quests;
        minimumSize      = new Vector2(300f, 280f);
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        var root = new AdvancedDropdownItem("Quests");

        root.AddChild(new QuestDropdownItem(null, "(None)"));

        foreach (var q in _quests)
            if (q != null)
                root.AddChild(new QuestDropdownItem(q, q.name));

        return root;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (item is QuestDropdownItem qi)
            OnSelected?.Invoke(qi.Quest);
    }

    class QuestDropdownItem : AdvancedDropdownItem
    {
        public QuestData Quest { get; }
        public QuestDropdownItem(QuestData quest, string label) : base(label)
            => Quest = quest;
    }
}

// ── Property drawer ───────────────────────────────────────────────────────────

[CustomPropertyDrawer(typeof(QuestData))]
class QuestDataPropertyDrawer : PropertyDrawer
{
    static List<QuestData> _cachedQuests;
    static double          _cacheTime;
    const  double          CacheTtlSeconds = 5.0;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Label on the left.
        Rect labelRect  = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth,
                                   position.y,
                                   position.width - EditorGUIUtility.labelWidth,
                                   position.height);

        EditorGUI.LabelField(labelRect, label);

        var current = property.objectReferenceValue as QuestData;
        string btnLabel = current != null ? current.name : "— select a quest —";

        if (GUI.Button(buttonRect, btnLabel, EditorStyles.popup))
        {
            var quests = GetCachedQuests();
            var state  = new AdvancedDropdownState();
            var dd     = new QuestDataDropdown(state, quests);
            dd.OnSelected += chosen =>
            {
                property.objectReferenceValue = chosen;
                property.serializedObject.ApplyModifiedProperties();
            };
            dd.Show(buttonRect);
        }

        EditorGUI.EndProperty();
    }

    static List<QuestData> GetCachedQuests()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_cachedQuests != null && now - _cacheTime < CacheTtlSeconds)
            return _cachedQuests;

        _cachedQuests = new List<QuestData>();
        string[] guids = AssetDatabase.FindAssets("t:QuestData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var    q    = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (q != null) _cachedQuests.Add(q);
        }
        _cachedQuests.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        _cacheTime = now;
        return _cachedQuests;
    }
}
