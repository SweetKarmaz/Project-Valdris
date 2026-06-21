using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Bake Greyspire NavMesh  (Phase G2)
//
// Bakes one NavMeshSurface over the whole scene, then drops a NavMeshLink across
// every doorway so connectivity through openings is deterministic instead of an
// accident of voxel alignment. The doorway WALL pieces stay in the bake (NPCs
// can't phase through walls); only the link crosses the gap. Door leaves are
// excluded from the bake (they swing at runtime). Re-runnable.
public class GreyspireNavMeshBuilder
{
    const float LinkReach = 1.3f;   // how far each side of the wall the link lands (m)
    const float LinkWidth = 2.0f;   // doorway width the link spans (m)

    [MenuItem("Tools/Valdris/Scene/Bake Greyspire NavMesh")]
    public static void Bake()
    {
        Scene scene = SceneManager.GetActiveScene();

        // 1) Door leaves swing open at runtime → keep them out of the static bake.
        //    (The doorway WALL piece stays, so the wall is still solid to NPCs.)
        int doorsIgnored = 0;
        foreach (var door in Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            var mod = door.GetComponent<NavMeshModifier>();
            if (mod == null) mod = door.gameObject.AddComponent<NavMeshModifier>();
            mod.overrideArea    = false;
            mod.ignoreFromBuild = true;
            doorsIgnored++;
        }

        // 2) One surface for the level.
        var surfGO = GameObject.Find("Greyspire_NavMesh");
        if (surfGO == null)
        {
            surfGO = new GameObject("Greyspire_NavMesh");
            SceneManager.MoveGameObjectToScene(surfGO, scene);
            Undo.RegisterCreatedObjectUndo(surfGO, "Create Greyspire NavMesh");
        }
        var surface = surfGO.GetComponent<NavMeshSurface>();
        if (surface == null) surface = surfGO.AddComponent<NavMeshSurface>();

        surface.agentTypeID       = 0;                                 // Humanoid
        surface.collectObjects    = CollectObjects.All;               // whole scene
        surface.useGeometry       = NavMeshCollectGeometry.RenderMeshes; // blockout has no colliders
        surface.layerMask         = ~0;
        surface.defaultArea       = 0;                                 // Walkable
        surface.overrideVoxelSize = true;
        surface.voxelSize         = 0.10f;                             // finer → cleaner walkable area in rooms

        // 3) Bake and persist the data so it survives scene reloads.
        surface.BuildNavMesh();

        string sceneDir = Path.GetDirectoryName(scene.path);
        string folder = string.IsNullOrEmpty(sceneDir) ? "Assets" : $"{sceneDir}/Greyspire_NavMesh";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(sceneDir, "Greyspire_NavMesh");

        string assetPath = $"{folder}/NavMesh.asset";
        var data = surface.navMeshData;
        if (data == null)
        {
            EditorUtility.DisplayDialog("Bake Greyspire NavMesh",
                "Bake produced no NavMeshData. Make sure the blockout is in the open scene and try again.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(data, assetPath);
        EditorUtility.SetDirty(surface);

        // 4) Bridge every doorway with a NavMeshLink (deterministic connectivity).
        int links = BuildDoorwayLinks(scene);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[GreyspireNavMesh] Baked. Door leaves ignored: {doorsIgnored}, doorway links: {links}. Asset: {assetPath}");
        EditorUtility.DisplayDialog("Bake Greyspire NavMesh",
            $"NavMesh baked and saved.\n\nDoor leaves excluded from bake: {doorsIgnored}\nDoorway links created: {links}\nAsset: {assetPath}\n\n" +
            "Walls stay solid; links carry NPCs through the openings. Turn on the Navigation overlay to see links + walkable area.", "OK");
    }

    // A link per doorway-wall piece, crossing the opening along the wall's normal.
    static int BuildDoorwayLinks(Scene scene)
    {
        var existing = GameObject.Find("Greyspire_NavLinks");
        if (existing != null) Object.DestroyImmediate(existing);
        var root = new GameObject("Greyspire_NavLinks");
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Build Greyspire NavLinks");

        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.Contains("DoorFrame") && !t.name.Contains("Door_Frame")) continue;

            float floorY = t.position.y > 5f ? 10f : 0f;   // ground vs Warden level
            var go = new GameObject("DoorLink");
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(
                new Vector3(t.position.x, floorY + 0.05f, t.position.z),
                Quaternion.Euler(0f, t.eulerAngles.y, 0f));

            var link = go.AddComponent<NavMeshLink>();
            link.agentTypeID   = 0;
            link.area          = 0;
            link.bidirectional = true;
            link.width         = LinkWidth;
            link.startPoint    = new Vector3(0f, 0f, -LinkReach);   // local: through the wall normal
            link.endPoint      = new Vector3(0f, 0f,  LinkReach);
            n++;
        }
        return n;
    }
}
