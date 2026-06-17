using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

// Tools > Valdris > Create Default Scene State
//
// Scans ONLY the currently active scene for PrisonerNPC and SaveableProp
// components and writes a SceneDefaultState ScriptableObject to:
//
//   Assets/_Project/Data/Scenes/<SceneName>/DefaultSceneState.asset
//
// Run this once after the initial scene build and whenever the NPC/prop layout
// changes in a way that should become the new "pristine" starting state.
//
// This asset is READ-ONLY at runtime. SceneStateManager only uses it on a
// scene's very first visit in a playthrough. After that, the per-playthrough
// JSON in Application.persistentDataPath/Scenes/<scene>_scene.json is used.
public static class SceneDefaultStateBuilder
{
    [MenuItem("Tools/Valdris/Create Default Scene State")]
    public static void Build()
    {
        var activeScene = SceneManager.GetActiveScene();
        string sceneName = activeScene.name;

        if (string.IsNullOrEmpty(sceneName) || sceneName == "Untitled")
        {
            EditorUtility.DisplayDialog("Error",
                "Save and name the scene before creating a default state.", "OK");
            return;
        }

        // ── Scan active scene only ────────────────────────────────────────────

        var npcStates  = new List<SavedNPCState>();
        var propStates = new List<SavedPropState>();

        // Only scan root GameObjects that belong to the active scene.
        foreach (var root in activeScene.GetRootGameObjects())
        {
            foreach (var npc in root.GetComponentsInChildren<PrisonerNPC>(true))
            {
                if (string.IsNullOrEmpty(npc.SaveId))
                {
                    Debug.LogWarning(
                        $"[SceneDefaultStateBuilder] {npc.name} has no SaveId — skipped. " +
                        "Run Build Greyspire Scene first so spawned NPCs are registered.");
                    continue;
                }

                npcStates.Add(new SavedNPCState
                {
                    id            = npc.SaveId,
                    prefabName    = npc.PrefabName,
                    position      = npc.transform.position,
                    yRotation     = npc.transform.eulerAngles.y,
                    isAlive       = true,
                    currentHealth = npc.maxHealth,
                    maxHealth     = npc.maxHealth,
                });
            }

            foreach (var prop in root.GetComponentsInChildren<SaveableProp>(true))
            {
                if (string.IsNullOrEmpty(prop.propId))
                {
                    Debug.LogWarning(
                        $"[SceneDefaultStateBuilder] SaveableProp on '{prop.name}' has no propId — skipped.");
                    continue;
                }

                propStates.Add(new SavedPropState
                {
                    id            = prop.propId,
                    isActive      = prop.gameObject.activeSelf,
                    position      = prop.transform.position,
                    rotationEuler = prop.transform.eulerAngles,
                    localScale    = prop.transform.localScale,
                });
            }
        }

        // ── Write asset ───────────────────────────────────────────────────────

        string dir       = $"Assets/_Project/Data/Scenes/{sceneName}";
        string assetPath = $"{dir}/DefaultSceneState.asset";

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var asset = AssetDatabase.LoadAssetAtPath<SceneDefaultState>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SceneDefaultState>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        asset.npcs  = npcStates;
        asset.props = propStates;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Wire into scene SceneStateManager if unassigned ───────────────────

        var sm = Object.FindAnyObjectByType<SceneStateManager>();
        if (sm != null && sm.defaultState == null)
        {
            sm.defaultState = asset;
            EditorUtility.SetDirty(sm.gameObject);
            Debug.Log("[SceneDefaultStateBuilder] Assigned default state to SceneStateManager.");
        }

        string absolutePath = Path.GetFullPath(assetPath);
        Debug.Log($"[SceneDefaultStateBuilder] Saved to {assetPath} — {npcStates.Count} NPC(s), {propStates.Count} prop(s).");
        EditorUtility.DisplayDialog("Default Scene State Created",
            $"Scene: {sceneName}\n\n" +
            $"NPCs recorded : {npcStates.Count}\n" +
            $"Props recorded: {propStates.Count}\n\n" +
            $"Asset location:\n{assetPath}\n\n" +
            $"This asset is the pristine starting point for this scene. " +
            $"It is never modified at runtime — each playthrough uses a separate " +
            $"JSON file in Application.persistentDataPath/Scenes/.",
            "OK");
    }
}
