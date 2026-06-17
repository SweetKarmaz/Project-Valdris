using UnityEditor;
using UnityEngine;

// Custom inspector for NpcController. Plain per-instance fields are drawn
// normally; archetype fields appear as an "Override <field>" toggle that only
// reveals the value when enabled — otherwise the value comes from the
// NpcDefinition assigned in the Class section.
[CustomEditor(typeof(NpcController))]
public class NpcControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Class & instance ──
        Field("definition");
        Field("displayNameOverride");
        Field("saveId");

        Section("Inventory & Loot");
        Field("lootRarity");
        Field("goldMin");
        Field("goldMax");
        Field("items");

        Section("Story (per-instance)");
        Field("requiredWorldFlags");
        Field("setsWorldFlagsOnDeath");

        Section("Dialogue (per-instance)");
        Field("canTalk");
        Field("greetingLine");

        // ── Overrides ──
        Section("Identity — overrides");
        Override("overrideFaction",     "faction",     "Faction");
        Override("overrideIsEssential", "isEssential", "Is Essential");

        Section("Stats — overrides");
        Override("overrideLevel",     "level",     "Level");
        Override("overrideMaxHealth", "maxHealth", "Max Health");
        Override("overrideMaxMana",   "maxMana",   "Max Mana");
        Override("overrideBaseArmor", "baseArmor", "Base Armor");
        Override("overrideXpReward",  "xpReward",  "XP Reward");

        Section("Movement & Behavior — overrides");
        Override("overrideBehaviorType",   "behaviorType",   "Behavior Type");
        Override("overrideWalkSpeed",      "walkSpeed",      "Walk Speed");
        Override("overrideRunSpeed",       "runSpeed",       "Run Speed");
        Override("overrideAllowedZones",   "allowedZones",   "Allowed Zones");
        Override("overrideIdleSecondsMin", "idleSecondsMin", "Idle Seconds Min");
        Override("overrideIdleSecondsMax", "idleSecondsMax", "Idle Seconds Max");

        Section("Combat — overrides");
        Override("overrideAttackPriority", "attackPriority", "Attack Priority");

        Section("Reactions — overrides");
        Override("overrideWitnessedKillReaction", "witnessedKillReaction", "Witnessed Kill Reaction");

        Section("Spells — overrides");
        Override("overrideKnownSpells",   "knownSpells",   "Known Spells");
        Override("overrideManaRegenRate", "manaRegenRate", "Mana Regen Rate");

        serializedObject.ApplyModifiedProperties();
    }

    void Field(string prop)
    {
        var p = serializedObject.FindProperty(prop);
        if (p != null) EditorGUILayout.PropertyField(p, true);
    }

    void Section(string title)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    // Draws the "Override X" toggle; reveals the value field (indented) only
    // when the toggle is enabled.
    void Override(string toggleProp, string valueProp, string label)
    {
        var toggle = serializedObject.FindProperty(toggleProp);
        var value  = serializedObject.FindProperty(valueProp);
        if (toggle == null || value == null) return;

        toggle.boolValue = EditorGUILayout.ToggleLeft($"Override {label}", toggle.boolValue);
        if (toggle.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(value, new GUIContent(label), true);
            EditorGUI.indentLevel--;
        }
    }
}
