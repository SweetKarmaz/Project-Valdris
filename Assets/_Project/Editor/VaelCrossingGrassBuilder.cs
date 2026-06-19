using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Grass
//
// PHASE 4b of the VaelCrossing world build: grass ground cover.
//
// HDRP can't render terrain-detail grass, so this sets up a runtime GrassField
// component (GPU-instanced clumps around the camera — no scene bloat, with wind
// sway and zone-weighted density). This tool just creates/wires the object;
// tune the look on the GrassField component in the Inspector.
public static class VaelCrossingGrassBuilder
{
    const string TerrainObject = "VaelCrossing Terrain";
    const string GrassObject   = "VaelCrossing GrassField";
    const string PlantsDir     = "Assets/Synty/PolygonNature/Prefabs/Plants";

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Grass")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject) ?? Terrain.activeTerrain;
        if (terrain == null) { EditorUtility.DisplayDialog("Grass", "No VaelCrossing Terrain found.", "OK"); return; }

        // Pull mesh + material from the Synty grass prefabs (instancing enabled).
        var meshes = new List<Mesh>();
        var mats   = new List<Material>();
        foreach (var name in new[] { "SM_Plant_Grass_01", "SM_Plant_Grass_02", "SM_Plant_Grass_03",
                                     "SM_Plant_Grass_04", "SM_Plant_Grass_05" })
        {
            var prefab = FindPrefab(name);
            if (prefab == null) continue;
            var mf = prefab.GetComponentInChildren<MeshFilter>(true);
            var mr = prefab.GetComponentInChildren<MeshRenderer>(true);
            if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;

            var mat = mr.sharedMaterial;
            if (!mat.enableInstancing) { mat.enableInstancing = true; EditorUtility.SetDirty(mat); }

            meshes.Add(mf.sharedMesh);
            mats.Add(mat);
            if (meshes.Count >= 3) break;   // a few for variety is plenty
        }
        if (meshes.Count == 0) { EditorUtility.DisplayDialog("Grass", "No grass prefabs found in PolygonNature.", "OK"); return; }

        var go = GameObject.Find(GrassObject);
        if (go == null)
        {
            go = new GameObject(GrassObject);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create VaelCrossing GrassField");
        }
        var field = go.GetComponent<GrassField>() ?? Undo.AddComponent<GrassField>(go);
        field.terrain   = terrain;
        field.meshes    = meshes.ToArray();
        field.materials = mats.ToArray();

        EditorUtility.SetDirty(field);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[VaelCrossingGrass] GrassField ready with {meshes.Count} grass mesh(es). " +
                  "Tune density/wind/zones on the GrassField component.");
    }

    static GameObject FindPrefab(string nameNoExt)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{nameNoExt} t:Prefab", new[] { PlantsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == nameNoExt)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
