using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Foliage
//
// PHASE 4a of the VaelCrossing world build: scattered props (instanced GameObjects).
//
// Scatters Synty PolygonNature trees / dead trees / bushes / rocks / flower
// clusters by zone, sampling terrain height + slope so nothing lands in the
// river or on cliffs. Grass/flower GROUND COVER is Phase 4b (terrain details).
//
// Re-runnable: rebuilds everything under "VaelCrossing Foliage". Deterministic
// per seed. Tune the per-group density sliders; raise the cap with care.
public class VaelCrossingFoliageBuilder : EditorWindow
{
    const string TerrainObject = "VaelCrossing Terrain";
    const string WaterObject   = "VaelCrossing Water";
    const string FoliageRoot   = "VaelCrossing Foliage";
    const string NaturePrefabs = "Assets/Synty/PolygonNature/Prefabs";

    int   seed         = 1234;
    int   maxInstances = 6000;     // global safety cap
    float waterMargin  = 1.0f;     // keep foliage this far above the water line

    // Zone boundaries (u = west→east, v = south→north), matching the build/paint.
    float mountainBandU = 0.36f;   // foliage starts east of the mountains
    float riverWestU    = 0.82f;   // …and stops west of the river
    float northStartV   = 0.60f;   // north region
    float southEndV     = 0.25f;   // south corrupted region
    float forestSplitU  = 0.60f;   // north: forest west of this, meadow east

    // Per-group density (×; 1 = tuned default) and enables.
    bool  doForest = true;  float dForest = 1f;
    float forestEdge = 0.18f;   // ragged tree-line depth (0 = hard rectangle)
    bool  doBrush  = true;  float dBrush  = 1f;
    bool  doDead   = true;  float dDead   = 1f;
    bool  doRocks  = true;  float dRocks  = 1f;
    float rockMaxElev = 0.58f;  // rocks stop below this (just under the snow line)
    bool  doFlowers= true;  float dFlowers= 1f;

    Vector2 _scroll;

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Foliage")]
    static void Open() => GetWindow<VaelCrossingFoliageBuilder>("VaelCrossing Foliage");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Phase 4a — scattered props (trees, dead trees, bushes, rocks, flowers). " +
            "Re-runnable. Grass ground cover is Phase 4b.", MessageType.Info);

        if (GUILayout.Button("Build / Rebuild Foliage", GUILayout.Height(32))) Build();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        seed         = EditorGUILayout.IntField("Seed", seed);
        maxInstances = EditorGUILayout.IntField("Max Instances", maxInstances);
        waterMargin  = EditorGUILayout.Slider("Water Margin (m)", waterMargin, 0f, 5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Zones (u=W→E, v=S→N)", EditorStyles.boldLabel);
        mountainBandU = EditorGUILayout.Slider("Mountains End (u)", mountainBandU, 0.1f, 0.5f);
        riverWestU    = EditorGUILayout.Slider("River West (u)", riverWestU, 0.6f, 0.95f);
        northStartV   = EditorGUILayout.Slider("North Start (v)", northStartV, 0.4f, 0.85f);
        southEndV     = EditorGUILayout.Slider("South End (v)", southEndV, 0.1f, 0.45f);
        forestSplitU  = EditorGUILayout.Slider("Forest/Meadow Split (u)", forestSplitU, 0.4f, 0.8f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Groups", EditorStyles.boldLabel);
        DensityRow("North Forest (trees)", ref doForest, ref dForest);
        using (new EditorGUI.DisabledScope(!doForest))
            forestEdge = EditorGUILayout.Slider("  Forest Edge Noise", forestEdge, 0f, 0.4f);
        DensityRow("Bushes / Ferns",       ref doBrush,  ref dBrush);
        DensityRow("South Dead Trees",     ref doDead,   ref dDead);
        DensityRow("Mountain Rocks",       ref doRocks,  ref dRocks);
        using (new EditorGUI.DisabledScope(!doRocks))
            rockMaxElev = EditorGUILayout.Slider("  Rock Max Elevation", rockMaxElev, 0.3f, 1f);
        DensityRow("Flower Clusters",      ref doFlowers,ref dFlowers);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build / Rebuild Foliage", GUILayout.Height(32))) Build();
        EditorGUILayout.EndScrollView();
    }

    static void DensityRow(string label, ref bool on, ref float density)
    {
        EditorGUILayout.BeginHorizontal();
        on = EditorGUILayout.ToggleLeft(label, on, GUILayout.Width(170));
        using (new EditorGUI.DisabledScope(!on))
            density = EditorGUILayout.Slider(density, 0.1f, 4f);
        EditorGUILayout.EndHorizontal();
    }

    // ── Build ────────────────────────────────────────────────────────────────────

    void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject) ?? Terrain.activeTerrain;
        if (terrain == null) { EditorUtility.DisplayDialog("Foliage", "No VaelCrossing Terrain found.", "OK"); return; }

        var water = GameObject.Find(WaterObject);
        float waterY = water != null ? water.transform.position.y : -9999f;

        var old = GameObject.Find(FoliageRoot);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(FoliageRoot);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create VaelCrossing Foliage");

        Random.InitState(seed);
        _placed = 0;

        // Living forest: north-west.
        if (doForest)
            Scatter(terrain, root, waterY, "Forest",
                Resolve(new[] { "SM_Tree_" }, new[] { "Dead", "Stump", "Log", "Branch", "Base",
                    "Generic", "Willow", "Round", "SM_Tree_0", "SM_Tree_Large", "Swamp", "PolyPine" }),
                mountainBandU, forestSplitU, northStartV, 0.98f,
                9f / dForest, new Vector2(0.8f, 1.5f), 0f, 30f, false, forestEdge);

        // Bushes / ferns: scattered through forest + north meadow.
        if (doBrush)
            Scatter(terrain, root, waterY, "Brush",
                Resolve(new[] { "SM_Plant_Bush_", "SM_Plant_Fern_" }, new[] { "SM_Plant_Bush_0" }),
                mountainBandU, riverWestU, northStartV, 0.98f,
                10f / dBrush, new Vector2(0.8f, 1.4f), 0f, 33f, false);

        // South dead trees: sparse in the corrupted band.
        if (doDead)
            Scatter(terrain, root, waterY, "DeadTrees",
                Resolve(new[] { "SM_Tree_Dead", "SM_Tree_Pine_Dead", "SM_Tree_Birch_Dead",
                    "SM_Tree_Generic_Dead", "SM_Tree_Swamp" }, null),
                mountainBandU, riverWestU, 0.03f, southEndV,
                18f / dDead, new Vector2(0.7f, 1.3f), 0f, 30f, false);

        // Mountain rocks: on the western slopes.
        if (doRocks)
            Scatter(terrain, root, waterY, "Rocks",
                Resolve(new[] { "SM_Rock_Boulder", "SM_Rock_0" }, new[] { "Cave", "Cluster" }),
                0.02f, mountainBandU, 0.05f, 0.95f,
                24f / dRocks, new Vector2(0.8f, 2f), 18f, 75f, true, 0f, 10f, 0f, rockMaxElev);

        // Flower clusters: light, across the meadows + town.
        if (doFlowers)
            Scatter(terrain, root, waterY, "Flowers",
                Resolve(new[] { "SM_Plant_Flowers_", "SM_Plant_PurpleFlower" }, null),
                mountainBandU, riverWestU, southEndV, 0.95f,
                14f / dFlowers, new Vector2(0.8f, 1.3f), 0f, 25f, false);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[VaelCrossingFoliage] Placed {_placed} props (cap {maxInstances}).");
    }

    int _placed;

    void Scatter(Terrain terrain, GameObject root, float waterY, string groupName,
                 List<GameObject> prefabs, float uMin, float uMax, float vMin, float vMax,
                 float spacing, Vector2 scaleRange, float slopeMin, float slopeMax, bool tiltToNormal,
                 float edgeFeather = 0f, float edgeNoiseFreq = 10f, float elevMin = 0f, float elevMax = 1f)
    {
        if (prefabs.Count == 0) { Debug.LogWarning($"[VaelCrossingFoliage] No prefabs for {groupName}."); return; }
        spacing = Mathf.Max(1.5f, spacing);

        var data = terrain.terrainData;
        Vector3 size = data.size, origin = terrain.transform.position;
        var group = new GameObject(groupName);
        group.transform.SetParent(root.transform);

        float xMin = origin.x + uMin * size.x, xMax = origin.x + uMax * size.x;
        float zMin = origin.z + vMin * size.z, zMax = origin.z + vMax * size.z;

        for (float z = zMin; z < zMax; z += spacing)
        for (float x = xMin; x < xMax; x += spacing)
        {
            if (_placed >= maxInstances) return;

            float px = x + Random.Range(-spacing, spacing) * 0.4f;
            float pz = z + Random.Range(-spacing, spacing) * 0.4f;
            float u = (px - origin.x) / size.x, v = (pz - origin.z) / size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

            // Ragged edges: near the zone border, drop points by noise so the
            // boundary isn't a clean rectangle.
            if (edgeFeather > 0f)
            {
                float fu = Mathf.InverseLerp(uMin, uMax, u);
                float fv = Mathf.InverseLerp(vMin, vMax, v);
                float edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
                float keep = Mathf.Clamp01(edge / edgeFeather);   // 0 at border → 1 inside
                if (Mathf.PerlinNoise(u * edgeNoiseFreq, v * edgeNoiseFreq) > keep) continue;
            }

            float gy = terrain.SampleHeight(new Vector3(px, 0f, pz)) + origin.y;
            if (gy <= waterY + waterMargin) continue;                 // not in/near water

            float elevNorm = (gy - origin.y) / size.y;
            if (elevNorm < elevMin || elevNorm > elevMax) continue;   // elevation band (e.g. below snow)

            float slope = data.GetSteepness(u, v);
            if (slope < slopeMin || slope > slopeMax) continue;

            var prefab = prefabs[Random.Range(0, prefabs.Count)];
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            inst.transform.position = new Vector3(px, gy, pz);

            Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            inst.transform.rotation = tiltToNormal
                ? Quaternion.FromToRotation(Vector3.up, data.GetInterpolatedNormal(u, v)) * yaw
                : yaw;
            inst.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);

            _placed++;
        }
    }

    // ── Prefab resolution ────────────────────────────────────────────────────────

    static readonly Dictionary<string, List<GameObject>> _cache = new();

    static List<GameObject> Resolve(string[] includes, string[] excludes)
    {
        string key = string.Join("|", includes) + "##" + (excludes != null ? string.Join("|", excludes) : "");
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var list = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { NaturePrefabs }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!includes.Any(inc => file.Contains(inc))) continue;
            if (excludes != null && excludes.Any(ex => file.Contains(ex))) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }
        _cache[key] = list;
        return list;
    }
}
