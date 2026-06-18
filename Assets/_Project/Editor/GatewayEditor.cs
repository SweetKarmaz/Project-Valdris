using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Gateway))]
public class GatewayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw everything except the two fields we replace with dropdowns.
        DrawPropertiesExcluding(serializedObject, "targetSceneName", "destinationSpawnId", "m_Script");

        // ── Scene dropdown ────────────────────────────────────────────────────
        var sceneNameProp = serializedObject.FindProperty("targetSceneName");

        var buildScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target Scene", EditorStyles.boldLabel);

        if (buildScenes.Count > 0)
        {
            int currentScene = buildScenes.IndexOf(sceneNameProp.stringValue);
            var sceneOptions = new List<string> { "— none —" };
            sceneOptions.AddRange(buildScenes);
            int selectedScene = EditorGUILayout.Popup("From Build Settings", currentScene + 1, sceneOptions.ToArray());
            if (selectedScene != currentScene + 1)
                sceneNameProp.stringValue = selectedScene == 0 ? "" : buildScenes[selectedScene - 1];
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No scenes found in Build Settings (File > Build Settings). Add scenes there first.",
                MessageType.Warning);
        }

        EditorGUILayout.PropertyField(sceneNameProp, new GUIContent("Scene Name (manual)"));

        // ── Spawn Point dropdown ──────────────────────────────────────────────
        var spawnIdProp = serializedObject.FindProperty("destinationSpawnId");

        var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
        var ids = points
            .Where(p => !string.IsNullOrEmpty(p.spawnId))
            .Select(p => p.spawnId)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destination Spawn Point", EditorStyles.boldLabel);

        if (ids.Count > 0)
        {
            int current = ids.IndexOf(spawnIdProp.stringValue);
            var options = new List<string> { "— none —" };
            options.AddRange(ids);
            int selected = EditorGUILayout.Popup("From open scene(s)", current + 1, options.ToArray());
            if (selected != current + 1)
                spawnIdProp.stringValue = selected == 0 ? "" : ids[selected - 1];
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No SpawnPoints found in open scenes. Open the destination scene additively to pick from a list, or type the id below.",
                MessageType.Info);
        }

        EditorGUILayout.PropertyField(spawnIdProp, new GUIContent("Spawn Id (manual)"));

        serializedObject.ApplyModifiedProperties();
    }
}
