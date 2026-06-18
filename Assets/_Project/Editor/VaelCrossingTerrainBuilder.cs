using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Terrain
//
// PHASE 1 of the VaelCrossing world build: the landform only.
//
// Sculpts a single 1000 m Unity Terrain into the active scene to match the
// VaelCrossing design — west mountain range, a descending pass from the prison
// door to the valley floor, a mostly-flat central valley, north/south border
// hills that grow east->west, and a wide winding river channel down the east edge.
//
// It is fully re-runnable: it reuses the existing terrain + TerrainData asset and
// just rewrites the heights, so you can tweak the parameters below and rebuild
// without piling up terrains. Texturing, water, props, and sky are LATER phases —
// a single grass layer is applied here only so the shape is visible (not pink).
public class VaelCrossingTerrainBuilder : EditorWindow
{
    const string SceneName     = "Act1_Part2_VaelCrossing";
    const string TerrainObject = "VaelCrossing Terrain";
    const string DataFolder    = "Assets/_Project/Scenes/Act1/Act1_Part2_VaelCrossing";
    const string DataAsset     = DataFolder + "/VaelCrossing_TerrainData.asset";

    // ── World ──────────────────────────────────────────────────────────────────
    int   terrainSize   = 1000;   // metres (X & Z)
    int   terrainHeight = 300;    // metres (max vertical)
    int   heightmapRes  = 1025;   // 1024 quads ≈ 0.98 m
    int   seed          = 12345;

    // ── Valley floor ───────────────────────────────────────────────────────────
    float floorHeight    = 0.06f; // ×height (≈18 m)
    float floorNoiseAmp  = 0.015f;
    float floorNoiseFreq = 3f;

    // ── West mountains ───────────────────────────────────────────────────────────
    float mountainWidth  = 0.34f; // fraction of map width (west band; wider = gentler pass)
    float mountainPeak   = 0.85f; // ×height (≈255 m)
    float mountainRough  = 0.45f; // ridge roughness 0..1
    float mountainFreq   = 4.5f;

    // ── Prison pass (notch through the west mountains) ───────────────────────────
    float passCenter        = 0.5f;   // N–S position (v)
    float passHalfWidth     = 0.07f;  // ×map (≈70 m corridor half-width)
    float doorHeight        = 0.45f;  // ×height at the west door (≈135 m)
    float passCurveAmp      = 0.05f;  // lateral curve of the corridor as it descends
    float passCurveFreq     = 1.2f;   // curve cycles across the mountain band
    float passBackWallHeight= 0.65f;  // ×height of the wall behind the entrance
    float passBackWallWidth = 0.03f;  // ×map width of that back wall (steep = small)

    // ── North/South border hills ─────────────────────────────────────────────────
    float hillDepth      = 0.18f; // fraction of map depth near each N/S edge
    float hillPeak       = 0.32f; // ×height (uniform — no ramp toward the mountains)
    float hillFreq       = 5f;
    float hillRough      = 0.35f;

    // ── East river (wide, winding) ───────────────────────────────────────────────
    float riverCenter      = 0.90f;  // base E–W position (u); pushed east for town room
    float riverHalfWidth   = 0.033f; // ×map (≈33 m → ~66 m wide)
    float riverBank        = 0.035f; // bank transition width
    float riverMeanderAmp  = 0.04f;  // ×map lateral wander (at the ends)
    float riverMeanderFreq = 2.5f;   // meander cycles N–S
    float riverMeanderSouthScale = 1.6f; // south-half meander vs north (asymmetry)
    float riverStraightFrac= 0.35f;  // middle fraction kept straight (dock/bridge spot)
    float riverBedHeight   = 0.018f; // ×height (below floor; lower = deeper)

    // ── East bank (beyond the river) ─────────────────────────────────────────────
    float eastHillAmp  = 0.05f;  // ×height of gentle hills east of the river
    float eastHillFreq = 6f;

    Vector2 _scroll;

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Terrain")]
    static void Open() => GetWindow<VaelCrossingTerrainBuilder>("VaelCrossing Terrain");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Phase 1 — landform only. Re-runnable: tweak and rebuild freely. " +
            "Run with the Act1_Part2_VaelCrossing scene open.", MessageType.Info);

        if (GUILayout.Button("Build / Rebuild Terrain", GUILayout.Height(32)))
            Build();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("World", EditorStyles.boldLabel);
        terrainSize   = EditorGUILayout.IntField("Size (m)", terrainSize);
        terrainHeight = EditorGUILayout.IntField("Max Height (m)", terrainHeight);
        heightmapRes  = EditorGUILayout.IntPopup("Heightmap Res", heightmapRes,
            new[] { "513", "1025", "2049" }, new[] { 513, 1025, 2049 });
        seed          = EditorGUILayout.IntField("Seed", seed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Valley Floor", EditorStyles.boldLabel);
        floorHeight    = EditorGUILayout.Slider("Base Height", floorHeight, 0f, 0.3f);
        floorNoiseAmp  = EditorGUILayout.Slider("Noise Amount", floorNoiseAmp, 0f, 0.1f);
        floorNoiseFreq = EditorGUILayout.Slider("Noise Freq", floorNoiseFreq, 0.5f, 12f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("West Mountains", EditorStyles.boldLabel);
        mountainWidth = EditorGUILayout.Slider("Band Width", mountainWidth, 0.1f, 0.45f);
        mountainPeak  = EditorGUILayout.Slider("Peak Height", mountainPeak, 0.3f, 1f);
        mountainRough = EditorGUILayout.Slider("Roughness", mountainRough, 0f, 1f);
        mountainFreq  = EditorGUILayout.Slider("Ridge Freq", mountainFreq, 1f, 10f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prison Pass", EditorStyles.boldLabel);
        passCenter        = EditorGUILayout.Slider("N–S Position", passCenter, 0.2f, 0.8f);
        passHalfWidth     = EditorGUILayout.Slider("Half Width", passHalfWidth, 0.03f, 0.15f);
        doorHeight        = EditorGUILayout.Slider("Door Height", doorHeight, 0.1f, 0.7f);
        passCurveAmp      = EditorGUILayout.Slider("Curve Amount", passCurveAmp, 0f, 0.15f);
        passCurveFreq     = EditorGUILayout.Slider("Curve Freq", passCurveFreq, 0.3f, 3f);
        passBackWallHeight= EditorGUILayout.Slider("Back Wall Height", passBackWallHeight, 0.1f, 1f);
        passBackWallWidth = EditorGUILayout.Slider("Back Wall Width", passBackWallWidth, 0.005f, 0.1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("North/South Hills", EditorStyles.boldLabel);
        hillDepth = EditorGUILayout.Slider("Band Depth", hillDepth, 0.05f, 0.3f);
        hillPeak  = EditorGUILayout.Slider("Peak Height", hillPeak, 0.1f, 0.8f);
        hillFreq  = EditorGUILayout.Slider("Freq", hillFreq, 1f, 10f);
        hillRough = EditorGUILayout.Slider("Roughness", hillRough, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("East River", EditorStyles.boldLabel);
        riverCenter       = EditorGUILayout.Slider("E–W Position", riverCenter, 0.7f, 0.97f);
        riverHalfWidth    = EditorGUILayout.Slider("Half Width", riverHalfWidth, 0.02f, 0.1f);
        riverBank         = EditorGUILayout.Slider("Bank Width", riverBank, 0.01f, 0.08f);
        riverMeanderAmp   = EditorGUILayout.Slider("Meander Amt", riverMeanderAmp, 0f, 0.08f);
        riverMeanderFreq  = EditorGUILayout.Slider("Meander Freq", riverMeanderFreq, 0.5f, 6f);
        riverMeanderSouthScale = EditorGUILayout.Slider("South Meander ×", riverMeanderSouthScale, 0.2f, 2.5f);
        riverStraightFrac = EditorGUILayout.Slider("Straight Middle", riverStraightFrac, 0f, 0.8f);
        riverBedHeight    = EditorGUILayout.Slider("Bed Height", riverBedHeight, 0f, 0.06f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("East Bank", EditorStyles.boldLabel);
        eastHillAmp  = EditorGUILayout.Slider("Hill Amount", eastHillAmp, 0f, 0.2f);
        eastHillFreq = EditorGUILayout.Slider("Hill Freq", eastHillFreq, 1f, 12f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build / Rebuild Terrain", GUILayout.Height(36)))
            Build();

        EditorGUILayout.EndScrollView();
    }

    // ── Build ────────────────────────────────────────────────────────────────────

    void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName &&
            !EditorUtility.DisplayDialog("Wrong scene?",
                $"Active scene is '{scene.name}', not {SceneName}.\nBuild here anyway?",
                "Build here", "Cancel"))
            return;

        // Reuse or create the TerrainData asset so rebuilds don't pile up.
        if (!Directory.Exists(DataFolder)) Directory.CreateDirectory(DataFolder);
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataAsset);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, DataAsset);
        }

        data.heightmapResolution = heightmapRes;
        data.size = new Vector3(terrainSize, terrainHeight, terrainSize);
        data.SetHeights(0, 0, ComputeHeights(heightmapRes));

        // Reuse or create the Terrain GameObject in the scene.
        var existing = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject);
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null)
        {
            go = Terrain.CreateTerrainGameObject(data);
            go.name = TerrainObject;
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create VaelCrossing Terrain");
        }
        var terrain = go.GetComponent<Terrain>();
        terrain.terrainData = data;
        go.GetComponent<TerrainCollider>().terrainData = data;
        go.transform.position = Vector3.zero;

        ApplyBaseLayer(terrain);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[VaelCrossingTerrain] Built {terrainSize}×{terrainSize} m terrain " +
                  $"({heightmapRes} heightmap) into '{scene.name}'.");
    }

    // A single grass layer so the landform is visible. Real multi-layer painting is Phase 2.
    static void ApplyBaseLayer(Terrain terrain)
    {
        if (terrain.terrainData.terrainLayers != null &&
            terrain.terrainData.terrainLayers.Length > 0) return;

        string guid = AssetDatabase.FindAssets("layer_Grass t:TerrainLayer")
            .FirstOrDefault(g => Path.GetFileName(AssetDatabase.GUIDToAssetPath(g))
                                     .StartsWith("layer_Grass") &&
                                 !Path.GetFileName(AssetDatabase.GUIDToAssetPath(g))
                                     .StartsWith("layer_Grass_02"));
        if (guid == null) { Debug.LogWarning("[VaelCrossingTerrain] No grass TerrainLayer found."); return; }

        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
        if (layer != null) terrain.terrainData.terrainLayers = new[] { layer };
    }

    // ── Heightfield ──────────────────────────────────────────────────────────────

    float[,] ComputeHeights(int res)
    {
        var h = new float[res, res];
        float s = seed * 0.013f;

        for (int z = 0; z < res; z++)
        {
            float v = z / (float)(res - 1);            // 0 south → 1 north
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1);        // 0 west → 1 east

                // Valley floor with gentle undulation.
                float height = floorHeight +
                    (Fbm(u * floorNoiseFreq + s, v * floorNoiseFreq + s, 4) - 0.5f) * floorNoiseAmp;

                // West mountains (rise above the floor).
                if (u < mountainWidth)
                {
                    float t = Smooth01(1f - u / mountainWidth);
                    float ridge = Ridged(u * mountainFreq + s, v * mountainFreq + s, 4);
                    float mtn = floorHeight + mountainPeak * t * (1f - mountainRough + mountainRough * ridge);
                    height = Mathf.Max(height, mtn);
                }

                // North & south border hills (grow west toward the mountains).
                float edge = Mathf.Min(v, 1f - v);                 // distance to nearest N/S edge
                if (edge < hillDepth)
                {
                    float t = Smooth01(1f - edge / hillDepth);
                    // Keep the N/S border hills west of the river — don't let them cross it.
                    float westClip = Smooth01((riverCenter - riverHalfWidth - u) / 0.08f);
                    float bump = Ridged(u * hillFreq - s, v * hillFreq - s, 3);
                    float hill = floorHeight + hillPeak * t * westClip * (1f - hillRough + hillRough * bump);
                    height = Mathf.Max(height, hill);
                }

                // Prison pass — carve a curving, descending corridor through the mountains.
                float passCenterAtU = passCenter +
                    Mathf.Sin(u * passCurveFreq * Mathf.PI * 2f) * passCurveAmp;
                float dPass = Mathf.Abs(v - passCenterAtU);
                if (dPass < passHalfWidth && u < mountainWidth * 1.15f)
                {
                    float blend = Smooth01(1f - dPass / passHalfWidth);
                    float passH;
                    if (u < passBackWallWidth)
                    {
                        // Wall rising to the far west, so there's mountain behind the entrance.
                        float wt = Smooth01(1f - u / passBackWallWidth);
                        passH = Mathf.Lerp(doorHeight, passBackWallHeight, wt);
                    }
                    else
                    {
                        float ramp = Mathf.Clamp01((u - passBackWallWidth) /
                                                   (mountainWidth - passBackWallWidth));
                        passH = Mathf.Lerp(doorHeight, floorHeight, Smooth01(ramp));
                    }
                    height = Mathf.Lerp(height, Mathf.Min(height, passH), blend);
                }

                // East bank — gentle hills beyond the river for texture.
                if (u > riverCenter)
                {
                    float t = Smooth01((u - riverCenter) / Mathf.Max(0.001f, 1f - riverCenter));
                    float bump = Ridged(u * eastHillFreq + s, v * eastHillFreq + s, 3);
                    height = Mathf.Max(height, floorHeight + eastHillAmp * t * bump);
                }

                // East river — straight through the middle, curving toward the ends
                // (north and south curve differently).
                float centerDist = Mathf.Abs(v - 0.5f) * 2f;   // 0 at middle, 1 at N/S ends
                float meanderEnv = Smooth01((centerDist - riverStraightFrac) /
                                            Mathf.Max(0.001f, 1f - riverStraightFrac));
                float ampSide = riverMeanderAmp * (v < 0.5f ? riverMeanderSouthScale : 1f);
                float meander = Mathf.Sin(v * riverMeanderFreq * Mathf.PI * 2f) * ampSide * meanderEnv;
                float dRiver  = Mathf.Abs(u - (riverCenter + meander));
                if (dRiver < riverHalfWidth + riverBank)
                {
                    float carve = 1f - Smooth01((dRiver - riverHalfWidth) / riverBank);
                    height = Mathf.Lerp(height, Mathf.Min(height, riverBedHeight), Mathf.Clamp01(carve));
                }

                h[z, x] = Mathf.Clamp01(height);
            }
        }
        return h;
    }

    // ── Noise helpers ────────────────────────────────────────────────────────────

    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum  += amp * Mathf.PerlinNoise(x * freq, y * freq);
            norm += amp; freq *= 2f; amp *= 0.5f;
        }
        return sum / norm; // ~0..1
    }

    static float Ridged(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * freq, y * freq) - 1f);
            sum  += amp * n;
            norm += amp; freq *= 2f; amp *= 0.5f;
        }
        return sum / norm; // ~0..1, ridged
    }
}
