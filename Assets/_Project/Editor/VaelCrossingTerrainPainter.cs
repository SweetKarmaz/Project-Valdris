using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Paint VaelCrossing Terrain
//
// PHASE 2 of the VaelCrossing world build: terrain texturing.
//
// Reads the built terrain's height + slope back from its TerrainData and paints
// the Synty PolygonNature terrain layers by rule:
//   • steep slopes        → Rockwall
//   • high peaks          → Snow
//   • near the water      → Sand (river bed/banks)
//   • the prison pass     → Mud + Pebbles (a worn descending path)
//   • the town centre     → patchy Grass/Mud mix (trodden ground)
//   • the southern band   → grass with extra mud (seeds the corrupted look)
//   • everything else     → Grass
//
// Re-runnable: tweak the sliders and rebuild. It only writes terrainLayers +
// alphamaps, so it never touches the heightfield from Phase 1.
public class VaelCrossingTerrainPainter : EditorWindow
{
    const string SceneName     = "Act1_Part2_VaelCrossing";
    const string TerrainObject = "VaelCrossing Terrain";

    int alphamapRes = 512;

    // ── Slope / elevation (global rules) ─────────────────────────────────────────
    float rockSlopeStart = 30f;  // deg
    float rockSlopeFull  = 46f;
    float rockWeight     = 3f;
    const string RockLayerPath = "Assets/_Project/Data/TerrainLayers/VC_Marble.terrainlayer";
    const string PassDirtPath  = "Assets/_Project/Data/TerrainLayers/VC_Ground067.terrainlayer";
    const string TownDirtPath  = "Assets/_Project/Data/TerrainLayers/VC_Ground103.terrainlayer";
    float snowStart      = 0.62f; // ×height
    float snowFull       = 0.80f;
    float snowWeight     = 2.5f;
    float sandMaxElev    = 0.05f; // ×height — sand fades out above this
    float sandWeight     = 1.6f;

    // ── Prison pass (worn path through the mountains) ────────────────────────────
    float mountainBand = 0.34f;  // western fraction to look for the pass in
    float passSlopeMax = 24f;    // deg — pass floor is the gentle part of the band
    float passElevMax  = 0.55f;  // ×height — ignore high steep mountain
    float passDirt     = 1.2f;
    float passPebbles  = 0.6f;

    // ── Town centre (patchy grass/dirt) ──────────────────────────────────────────
    float townUMin = 0.38f, townUMax = 0.82f;
    float townVMin = 0.37f, townVMax = 0.63f;
    float townEdge = 0.05f;      // soft edge falloff
    float townDirt = 0.7f;       // average dirt mix amount
    float patchFreq = 35f;       // worn-patch noise frequency

    // ── Southern corrupted band ──────────────────────────────────────────────────
    float southBandV = 0.25f;    // v below this gets the treatment
    float southMud   = 0.4f;

    Vector2 _scroll;

    [MenuItem("Tools/Valdris/Scene/Paint VaelCrossing Terrain")]
    static void Open() => GetWindow<VaelCrossingTerrainPainter>("VaelCrossing Paint");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Phase 2 — texturing. Reads height/slope from the built terrain and " +
            "paints Synty layers. Re-runnable; never changes the landform.", MessageType.Info);

        if (GUILayout.Button("Paint / Repaint Terrain", GUILayout.Height(32)))
            Paint();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        alphamapRes = EditorGUILayout.IntPopup("Alphamap Res", alphamapRes,
            new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rock (slope)", EditorStyles.boldLabel);
        rockSlopeStart = EditorGUILayout.Slider("Start (deg)", rockSlopeStart, 10f, 60f);
        rockSlopeFull  = EditorGUILayout.Slider("Full (deg)", rockSlopeFull, 15f, 80f);
        rockWeight     = EditorGUILayout.Slider("Strength", rockWeight, 0.5f, 6f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Snow (elevation)", EditorStyles.boldLabel);
        snowStart  = EditorGUILayout.Slider("Start", snowStart, 0.3f, 1f);
        snowFull   = EditorGUILayout.Slider("Full", snowFull, 0.4f, 1f);
        snowWeight = EditorGUILayout.Slider("Strength", snowWeight, 0f, 6f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sand (near water)", EditorStyles.boldLabel);
        sandMaxElev = EditorGUILayout.Slider("Max Elevation", sandMaxElev, 0.01f, 0.15f);
        sandWeight  = EditorGUILayout.Slider("Strength", sandWeight, 0f, 5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prison Pass", EditorStyles.boldLabel);
        mountainBand = EditorGUILayout.Slider("West Band", mountainBand, 0.1f, 0.5f);
        passSlopeMax = EditorGUILayout.Slider("Max Slope (deg)", passSlopeMax, 5f, 40f);
        passElevMax  = EditorGUILayout.Slider("Max Elevation", passElevMax, 0.1f, 0.9f);
        passDirt     = EditorGUILayout.Slider("Dirt", passDirt, 0f, 3f);
        passPebbles  = EditorGUILayout.Slider("Pebbles", passPebbles, 0f, 3f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Town Centre (grass/dirt mix)", EditorStyles.boldLabel);
        townUMin = EditorGUILayout.Slider("U Min (W→E)", townUMin, 0f, 1f);
        townUMax = EditorGUILayout.Slider("U Max (W→E)", townUMax, 0f, 1f);
        townVMin = EditorGUILayout.Slider("V Min (S→N)", townVMin, 0f, 1f);
        townVMax = EditorGUILayout.Slider("V Max (S→N)", townVMax, 0f, 1f);
        townEdge = EditorGUILayout.Slider("Edge Falloff", townEdge, 0.01f, 0.2f);
        townDirt = EditorGUILayout.Slider("Dirt Amount", townDirt, 0f, 2f);
        patchFreq= EditorGUILayout.Slider("Patch Freq", patchFreq, 5f, 80f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Southern Band", EditorStyles.boldLabel);
        southBandV = EditorGUILayout.Slider("South Depth", southBandV, 0f, 0.45f);
        southMud   = EditorGUILayout.Slider("Extra Mud", southMud, 0f, 1.5f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Paint / Repaint Terrain", GUILayout.Height(36)))
            Paint();

        EditorGUILayout.EndScrollView();
    }

    // ── Paint ────────────────────────────────────────────────────────────────────

    void Paint()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject)
            ?? Terrain.activeTerrain;
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("Paint VaelCrossing Terrain",
                "No terrain found. Run Phase 1 (Build VaelCrossing Terrain) first.", "OK");
            return;
        }
        var data = terrain.terrainData;

        // Resolve the terrain layers (fixed channel order). Pass and town each get
        // their own dirt channel so they can use different textures.
        var rockLayer  = AssetDatabase.LoadAssetAtPath<TerrainLayer>(RockLayerPath) ?? FindLayer("layer_Rockwall");
        var passDirtTL = AssetDatabase.LoadAssetAtPath<TerrainLayer>(PassDirtPath) ?? FindLayer("layer_Mud");
        var townDirtTL = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TownDirtPath) ?? FindLayer("layer_Mud");
        var layers = new[]
        {
            FindLayer("layer_Grass", "layer_Grass_02"),   // 0 grass
            passDirtTL,                                     // 1 pass dirt
            townDirtTL,                                     // 2 town dirt
            FindLayer("layer_Pebbles", "layer_Pebbles_Sand"), // 3 pebbles
            rockLayer,                                       // 4 rock
            FindLayer("layer_Sand", "Darker", "Desert", "HigherDetail"), // 5 sand
            FindLayer("layer_Snow"),                        // 6 snow
        };
        if (layers.Any(l => l == null))
        {
            Debug.LogWarning("[VaelCrossingPaint] Missing one or more terrain layers; " +
                             "those channels will stay empty.");
        }
        const int G = 0, PD = 1, TD = 2, P = 3, R = 4, S = 5, W = 6;
        int n = layers.Length;
        data.terrainLayers = layers;

        data.alphamapResolution = alphamapRes;
        int A = alphamapRes;
        int hRes = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, hRes, hRes);
        var map = new float[A, A, n];

        for (int ay = 0; ay < A; ay++)
        {
            float v = ay / (float)(A - 1);             // 0 south → 1 north
            for (int ax = 0; ax < A; ax++)
            {
                float u = ax / (float)(A - 1);         // 0 west → 1 east

                int hx = Mathf.RoundToInt(u * (hRes - 1));
                int hy = Mathf.RoundToInt(v * (hRes - 1));
                float elev  = heights[hy, hx];
                float slope = data.GetSteepness(u, v);

                float wGrass = 1f, wPassDirt = 0f, wTownDirt = 0f, wPeb = 0f, wRock = 0f, wSand = 0f, wSnow = 0f;

                // Town centre — patchy grass/dirt (trodden ground).
                float town = Rect01(u, townUMin, townUMax, townEdge) *
                             Rect01(v, townVMin, townVMax, townEdge);
                if (town > 0f)
                {
                    float patch = Mathf.PerlinNoise(u * patchFreq, v * patchFreq); // 0..1
                    wTownDirt += town * townDirt * Mathf.Lerp(0.25f, 1.5f, patch);
                }

                // Southern corrupted band — grass with extra mud (uses town dirt).
                float south = Smooth01((southBandV - v) / Mathf.Max(0.001f, southBandV));
                wTownDirt += south * southMud;

                // Prison pass — worn dirt + pebbles on the gentle floor of the west band.
                if (u < mountainBand && elev < passElevMax)
                {
                    float gentle = Smooth01((passSlopeMax - slope) / passSlopeMax);
                    wPassDirt += gentle * passDirt;
                    wPeb      += gentle * passPebbles;
                }

                // Sand near the water (low elevation), not on cliffs.
                float sand = Smooth01((sandMaxElev - elev) / Mathf.Max(0.001f, sandMaxElev));
                wSand += sand * sandWeight;

                // Rock on steep slopes (dominant).
                float rock = Smooth01((slope - rockSlopeStart) / Mathf.Max(0.1f, rockSlopeFull - rockSlopeStart));
                wRock += rock * rockWeight;

                // Snow on high elevation (dominant).
                float snow = Smooth01((elev - snowStart) / Mathf.Max(0.001f, snowFull - snowStart));
                wSnow += snow * snowWeight;

                // Rock/snow/sand suppress the soft ground beneath them.
                float hard = Mathf.Clamp01(rock + snow);
                wGrass    *= (1f - hard);
                wPassDirt *= (1f - hard);
                wTownDirt *= (1f - hard);
                wPeb      *= (1f - hard);

                Write(map, ay, ax, G,  wGrass);
                Write(map, ay, ax, PD, wPassDirt);
                Write(map, ay, ax, TD, wTownDirt);
                Write(map, ay, ax, P,  wPeb);
                Write(map, ay, ax, R,  wRock);
                Write(map, ay, ax, S,  wSand);
                Write(map, ay, ax, W,  wSnow);

                // Normalize so the channel weights sum to 1.
                float sum = 0f;
                for (int l = 0; l < n; l++) sum += map[ay, ax, l];
                if (sum <= 1e-4f) { map[ay, ax, G] = 1f; sum = 1f; }
                for (int l = 0; l < n; l++) map[ay, ax, l] /= sum;
            }
        }

        Undo.RegisterCompleteObjectUndo(data, "Paint VaelCrossing Terrain");
        data.SetAlphamaps(0, 0, map);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[VaelCrossingPaint] Painted {A}×{A} alphamap with {n} layers.");
    }

    static void Write(float[,,] map, int y, int x, int layer, float w)
    {
        if (layer < map.GetLength(2)) map[y, x, layer] = Mathf.Max(0f, w);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    // 1 inside [min,max], smoothly falling to 0 across `edge` on each side.
    static float Rect01(float t, float min, float max, float edge)
    {
        float lo = Smooth01((t - min) / Mathf.Max(0.001f, edge));
        float hi = Smooth01((max - t) / Mathf.Max(0.001f, edge));
        return Mathf.Clamp01(Mathf.Min(lo, hi));
    }

    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    // Finds a PolygonNature TerrainLayer whose filename contains `contains`
    // and none of `excludes`.
    static TerrainLayer FindLayer(string contains, params string[] excludes)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileName(path);
            if (!file.Contains(contains)) continue;
            if (excludes.Any(e => file.Contains(e))) continue;
            return AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        }
        Debug.LogWarning($"[VaelCrossingPaint] TerrainLayer '{contains}' not found.");
        return null;
    }
}
