using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Water
//
// PHASE 3a of the VaelCrossing world build: the river water.
//
// Creates an HDRP Water Surface (River type) sized to the eastern channel. The
// height is auto-detected from the carved riverbed plus a depth offset, so the
// surface sits in the channel and is occluded by the higher banks elsewhere.
// Re-runnable: recreates the "VaelCrossing Water" object each time.
public class VaelCrossingWaterBuilder : EditorWindow
{
    const string TerrainObject = "VaelCrossing Terrain";
    const string WaterObject   = "VaelCrossing Water";

    float eastFraction = 0.70f;  // cover the river: x from this fraction to the east edge
    float waterDepth   = 5f;     // metres of water above the detected riverbed
    float manualLevel  = 0f;     // >0 overrides the auto height (world Y, metres)

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Water")]
    static void Open() => GetWindow<VaelCrossingWaterBuilder>("VaelCrossing Water");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Phase 3a — river water (HDRP Water System, River type). " +
            "Re-runnable. Needs 'Water' enabled on the HDRP asset (auto-attempted).",
            MessageType.Info);

        eastFraction = EditorGUILayout.Slider("East Coverage", eastFraction, 0.4f, 0.9f);
        waterDepth   = EditorGUILayout.Slider("Water Depth (m)", waterDepth, 0f, 20f);
        manualLevel  = EditorGUILayout.FloatField("Manual Level (0=auto)", manualLevel);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build / Rebuild Water", GUILayout.Height(34)))
            Build();
    }

    void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject) ?? Terrain.activeTerrain;
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("Build Water", "No VaelCrossing Terrain found.", "OK");
            return;
        }
        var data = terrain.terrainData;
        Vector3 size = data.size;
        Vector3 origin = terrain.transform.position;

        // Detect the riverbed: minimum terrain height in the eastern strip.
        int res = data.heightmapResolution;
        float[,] h = data.GetHeights(0, 0, res, res);
        int xStart = Mathf.Clamp(Mathf.RoundToInt(eastFraction * (res - 1)), 0, res - 1);
        float minH = 1f;
        for (int z = 0; z < res; z++)
            for (int x = xStart; x < res; x++)
                if (h[z, x] < minH) minH = h[z, x];
        float bedWorld = origin.y + minH * size.y;
        float waterY = manualLevel > 0f ? manualLevel : bedWorld + waterDepth;

        // Recreate the water object cleanly (drops any old plane-based version).
        var old = GameObject.Find(WaterObject);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var go = new GameObject(WaterObject);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create VaelCrossing Water");

        float coverX = (1f - eastFraction) * size.x;
        float cx = origin.x + (eastFraction + 1f) * 0.5f * size.x;
        float cz = origin.z + size.z * 0.5f;
        go.transform.position   = new Vector3(cx, waterY, cz);
        // For Quad geometry the surface size comes from the transform's X/Z scale.
        go.transform.localScale = new Vector3(coverX, 1f, size.z);

        var ws = go.AddComponent<WaterSurface>();
        ws.surfaceType  = WaterSurfaceType.River;
        ws.geometryType = WaterGeometryType.Quad;

        bool enabled = TryEnableWaterSupport();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[VaelCrossingWater] HDRP River water at Y={waterY:0.0} m " +
                  $"(bed {bedWorld:0.0} + {waterDepth}); covers east {1f - eastFraction:P0}." +
                  (enabled ? "" : " NOTE: enable 'Water' on the HDRP asset if it doesn't show."));
    }

    // Best-effort: turn on Water support in the active HDRP asset so the surface renders.
    static bool TryEnableWaterSupport()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null) return false;
        var so = new SerializedObject(rp);
        var prop = so.FindProperty("m_RenderPipelineSettings.supportWater");
        if (prop == null) return false;
        if (!prop.boolValue)
        {
            prop.boolValue = true;
            so.ApplyModifiedProperties();
            Debug.Log("[VaelCrossingWater] Enabled 'Water' support on the HDRP asset.");
        }
        return true;
    }
}
