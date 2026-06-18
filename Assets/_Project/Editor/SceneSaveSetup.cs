using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Setup Scene For Save System
//
// One-click setup so a scene participates in the save-tied scene-state system:
//
//   1. Ensures a "SceneManager" GameObject exists carrying a SceneStateManager
//      (the per-scene singleton that saves/restores NPCs, loot, containers).
//   2. Ensures SOME SceneGameManager (base class or a subclass like
//      GreyspireBuilder) exists — it spawns the player and signals the loading
//      overlay. Adds the plain base class if none is present.
//   3. Assigns a stable, unique id to every NpcController / LootContainer /
//      InteractableProp / SaveableProp that is missing one (existing ids are
//      never changed, so it is safe to re-run after adding objects).
//   4. Adds the scene to Build Settings if it is not already there.
//
// Re-run any time you add new NPCs or containers to a scene.
public static class SceneSaveSetup
{
    [MenuItem("Tools/Valdris/Scene/Setup Scene For Save System")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.name) || scene.name == "Untitled" || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Setup Scene For Save System",
                "Save and name the scene first.", "OK");
            return;
        }

        var log = new StringBuilder();

        bool addedStateManager  = EnsureSceneStateManager(scene, out GameObject managerGO);
        bool addedGameManager   = EnsureSceneGameManager(scene, managerGO);
        int  idsAssigned        = AssignMissingIds(scene, log);
        bool addedToBuild       = EnsureInBuildSettings(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        // ── Summary ───────────────────────────────────────────────────────────
        var summary = new StringBuilder();
        summary.AppendLine($"Scene: {scene.name}\n");
        summary.AppendLine(addedStateManager
            ? "• Added SceneStateManager."
            : "• SceneStateManager already present.");
        summary.AppendLine(addedGameManager
            ? "• Added SceneGameManager (base)."
            : "• SceneGameManager already present.");
        summary.AppendLine($"• IDs assigned this run: {idsAssigned}");
        summary.AppendLine(addedToBuild
            ? "• Added scene to Build Settings."
            : "• Scene already in Build Settings.");
        summary.AppendLine("\nRemember to run 'Create Default Scene State' so first-visit\n" +
                           "spawns match the placed NPCs/props, then save the scene.");

        Debug.Log($"[SceneSaveSetup] {scene.name}: stateMgr+{addedStateManager}, " +
                  $"gameMgr+{addedGameManager}, ids={idsAssigned}, build+{addedToBuild}\n{log}");
        EditorUtility.DisplayDialog("Setup Scene For Save System", summary.ToString(), "OK");
    }

    // ── SceneStateManager ───────────────────────────────────────────────────────

    static bool EnsureSceneStateManager(Scene scene, out GameObject managerGO)
    {
        var existing = FindInScene<SceneStateManager>(scene).FirstOrDefault();
        if (existing != null)
        {
            managerGO = existing.gameObject;
            return false;
        }

        // Reuse an existing "SceneManager" GameObject if one is around, else make it.
        managerGO = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SceneManager");
        if (managerGO == null)
        {
            managerGO = new GameObject("SceneManager");
            SceneManager.MoveGameObjectToScene(managerGO, scene);
            Undo.RegisterCreatedObjectUndo(managerGO, "Create SceneManager");
        }

        Undo.AddComponent<SceneStateManager>(managerGO);
        return true;
    }

    // ── SceneGameManager ──────────────────────────────────────────────────────

    static bool EnsureSceneGameManager(Scene scene, GameObject managerGO)
    {
        // Any subclass counts (e.g. GreyspireBuilder), so check the base type.
        if (FindInScene<SceneGameManager>(scene).Any()) return false;

        Undo.AddComponent<SceneGameManager>(managerGO);
        return true;
    }

    // ── ID assignment ───────────────────────────────────────────────────────────

    static int AssignMissingIds(Scene scene, StringBuilder log)
    {
        var used = new HashSet<string>();
        int count = 0;

        // Collect ids already in use so generated ones never collide.
        foreach (var npc in FindInScene<NpcController>(scene))       used.Add(npc.saveId);
        foreach (var c in FindInScene<LootContainer>(scene))         used.Add(c.containerId);
        foreach (var p in FindInScene<InteractableProp>(scene))      used.Add(p.propId);
        foreach (var p in FindInScene<SaveableProp>(scene))          used.Add(p.propId);

        foreach (var npc in FindInScene<NpcController>(scene))
            count += TrySetId(npc, "saveId", npc.saveId, used, log);

        foreach (var c in FindInScene<LootContainer>(scene))
            count += TrySetId(c, "containerId", c.containerId, used, log);

        foreach (var p in FindInScene<InteractableProp>(scene))
            count += TrySetId(p, "propId", p.propId, used, log);

        foreach (var p in FindInScene<SaveableProp>(scene))
            count += TrySetId(p, "propId", p.propId, used, log);

        return count;
    }

    static int TrySetId(Component c, string field, string current,
                        HashSet<string> used, StringBuilder log)
    {
        if (!string.IsNullOrEmpty(current)) return 0;

        string id = GenerateId(c.gameObject.name, used);
        used.Add(id);

        Undo.RecordObject(c, "Assign Save Id");
        var so = new SerializedObject(c);
        so.FindProperty(field).stringValue = id;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(c);

        log.AppendLine($"   {c.GetType().Name} '{c.gameObject.name}'.{field} = {id}");
        return 1;
    }

    static string GenerateId(string objectName, HashSet<string> used)
    {
        // Readable slug + short guid, guaranteed unique within the scene.
        string slug = new string(objectName.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        if (string.IsNullOrEmpty(slug)) slug = "obj";

        string id;
        do { id = $"{slug}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}"; }
        while (used.Contains(id));
        return id;
    }

    // ── Build Settings ──────────────────────────────────────────────────────────

    static bool EnsureInBuildSettings(Scene scene)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scene.path))
        {
            // Make sure it's enabled, but report that as "already present".
            return false;
        }

        scenes.Add(new EditorBuildSettingsScene(scene.path, enabled: true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Components in the given scene only (includes inactive objects).
    static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<T>(includeInactive: true))
                yield return c;
    }
}
