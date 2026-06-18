using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Bridge
//
// PHASE 3b of the VaelCrossing world build: the dock/bridge across the river.
//
// Tiles a Synty dock-plank piece (SM_Prop_Dock_03) end-to-end across the river
// at the crossing, then drops two rows of support poles from the deck down to
// the riverbed. Heights come from the existing water surface + terrain. Fully
// re-runnable with fine-tune offsets.
public class VaelCrossingBridgeBuilder : EditorWindow
{
    const string TerrainObject = "VaelCrossing Terrain";
    const string WaterObject   = "VaelCrossing Water";
    const string BridgeObject  = "VaelCrossing Bridge";

    GameObject deck, pole;

    float crossZFrac    = 0.5f;   // where along the river (S→N) the bridge sits
    float bankMargin    = 8f;     // metres of deck onto each bank
    float deckClearance = 1.5f;   // deck height above the water surface
    float yOffset       = 0f;     // manual deck height fine-tune
    float segScale      = 1f;     // deck piece scale

    bool  addPoles        = true;
    float poleEvery       = 6f;   // spacing of pole pairs along the span (m)
    float poleEdgeInset   = 1f;   // keep poles this far from the very ends (m)
    float poleSideInset   = 0.5f; // poles this far inside the deck edges (m)
    float poleThickness   = 1f;
    bool  poleStretchToBed= true; // scale poles down to reach the riverbed

    float manualWater   = 0f;     // >0 overrides detected water Y

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Bridge")]
    static void Open() => GetWindow<VaelCrossingBridgeBuilder>("VaelCrossing Bridge");

    void OnEnable()
    {
        if (deck == null) deck = FindPrefab("SM_Prop_Dock_03");
        if (pole == null) pole = FindPrefab("SM_Prop_Dock_Pole_01");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Phase 3b — dock bridge. Tiles Dock_03 planks across the river and adds " +
            "support poles to the bed. Re-runnable; tweak offsets then hand-nudge.", MessageType.Info);

        deck = (GameObject)EditorGUILayout.ObjectField("Deck Piece", deck, typeof(GameObject), false);
        pole = (GameObject)EditorGUILayout.ObjectField("Pole Piece", pole, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        crossZFrac    = EditorGUILayout.Slider("Crossing (S→N)", crossZFrac, 0.1f, 0.9f);
        bankMargin    = EditorGUILayout.Slider("Bank Margin (m)", bankMargin, 0f, 40f);
        deckClearance = EditorGUILayout.Slider("Deck Clearance (m)", deckClearance, -2f, 10f);
        yOffset       = EditorGUILayout.Slider("Y Offset (m)", yOffset, -10f, 10f);
        segScale      = EditorGUILayout.Slider("Deck Scale", segScale, 0.25f, 4f);
        manualWater   = EditorGUILayout.FloatField("Manual Water Y (0=auto)", manualWater);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Poles", EditorStyles.boldLabel);
        addPoles         = EditorGUILayout.Toggle("Add Poles", addPoles);
        poleEvery        = EditorGUILayout.Slider("Spacing (m)", poleEvery, 2f, 20f);
        poleEdgeInset    = EditorGUILayout.Slider("End Inset (m)", poleEdgeInset, 0f, 10f);
        poleSideInset    = EditorGUILayout.Slider("Side Inset (m)", poleSideInset, -2f, 4f);
        poleThickness    = EditorGUILayout.Slider("Thickness", poleThickness, 0.25f, 3f);
        poleStretchToBed = EditorGUILayout.Toggle("Stretch To Bed", poleStretchToBed);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(deck == null))
            if (GUILayout.Button("Build / Rebuild Bridge", GUILayout.Height(34)))
                Build();
    }

    void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject) ?? Terrain.activeTerrain;
        if (terrain == null) { EditorUtility.DisplayDialog("Build Bridge", "No VaelCrossing Terrain found.", "OK"); return; }

        var data = terrain.terrainData;
        Vector3 size = data.size, origin = terrain.transform.position;

        float waterY = manualWater;
        if (waterY <= 0f)
        {
            var water = GameObject.Find(WaterObject);
            waterY = water != null ? water.transform.position.y : origin.y + size.y * 0.04f;
        }
        float deckY = waterY + deckClearance + yOffset;

        // Wet span + bed level along the crossing row.
        int res = data.heightmapResolution;
        float[,] h = data.GetHeights(0, 0, res, res);
        int zi = Mathf.Clamp(Mathf.RoundToInt(crossZFrac * (res - 1)), 0, res - 1);
        float waterNorm = (waterY - origin.y) / size.y;
        int xMin = -1, xMax = -1; float bedNorm = 1f;
        for (int x = 0; x < res; x++)
            if (h[zi, x] < waterNorm)
            {
                if (xMin < 0) xMin = x;
                xMax = x;
                bedNorm = Mathf.Min(bedNorm, h[zi, x]);
            }
        if (xMin < 0) { EditorUtility.DisplayDialog("Build Bridge",
            "No water at this crossing row. Build water first or move the crossing.", "OK"); return; }

        float mPerCell = size.x / (res - 1);
        float crossX = origin.x + (xMin + xMax) * 0.5f * mPerCell;
        float crossZ = origin.z + crossZFrac * size.z;
        float bedY   = origin.y + bedNorm * size.y;
        float span   = (xMax - xMin) * mPerCell + bankMargin * 2f;

        Bounds db = MeasureBounds(deck, segScale, out bool lengthIsZ);
        float segLen    = lengthIsZ ? db.size.z : db.size.x;   // along the crossing
        float deckWidth = lengthIsZ ? db.size.x : db.size.z;   // across the path
        if (segLen < 0.01f) { Debug.LogWarning("[VaelCrossingBridge] Deck piece has no size."); return; }
        float yaw = lengthIsZ ? 90f : 0f;

        int n = Mathf.Max(1, Mathf.CeilToInt(span / segLen));
        float coverage = n * segLen;
        float startX = crossX - coverage * 0.5f + segLen * 0.5f;

        var old = GameObject.Find(BridgeObject);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(BridgeObject);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create VaelCrossing Bridge");

        // Deck.
        for (int i = 0; i < n; i++)
            PlaceCentered(deck, root.transform, new Vector3(startX + i * segLen, deckY, crossZ), yaw, segScale);

        // Poles: two rows under the deck edges, from end to end.
        if (addPoles && pole != null)
        {
            float halfW = deckWidth * 0.5f - poleSideInset;
            float left  = crossX - coverage * 0.5f + poleEdgeInset;
            float right = crossX + coverage * 0.5f - poleEdgeInset;
            int count = Mathf.Max(2, Mathf.FloorToInt((right - left) / poleEvery) + 1);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);
                float x = Mathf.Lerp(left, right, t);
                PlacePole(pole, root.transform, x, crossZ + halfW, deckY, bedY);
                PlacePole(pole, root.transform, x, crossZ - halfW, deckY, bedY);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[VaelCrossingBridge] {n} deck piece(s) across ~{(xMax - xMin) * mPerCell:0} m of water " +
                  $"at Y={deckY:0.0}; poles to bed Y={bedY:0.0}. Nudge offsets to seat it.");
    }

    // Snaps the piece so its bounds centre lands at `pos`, with yaw.
    static void PlaceCentered(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.transform.localScale = Vector3.one * scale;
        inst.transform.rotation   = Quaternion.Euler(0f, yaw, 0f);
        Bounds wb = WorldBounds(inst);
        inst.transform.position += pos - wb.center;
        Undo.RegisterCreatedObjectUndo(inst, "Place Deck");
    }

    // Places a pole with its top at deckTopY and (optionally) stretched to the bed.
    void PlacePole(GameObject prefab, Transform parent, float x, float z, float deckTopY, float bedY)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.transform.rotation   = Quaternion.identity;
        inst.transform.localScale = Vector3.one * poleThickness;
        Bounds b = WorldBounds(inst);

        if (poleStretchToBed && b.size.y > 0.01f)
        {
            float want = (deckTopY - bedY) + 1f;            // embed ~1 m into the bed
            float sy = Mathf.Max(0.05f, want / b.size.y);
            inst.transform.localScale = new Vector3(poleThickness, poleThickness * sy, poleThickness);
            b = WorldBounds(inst);
        }

        Vector3 c = b.center;
        inst.transform.position += new Vector3(x - c.x, deckTopY - b.max.y, z - c.z);
        Undo.RegisterCreatedObjectUndo(inst, "Place Pole");
    }

    static Bounds MeasureBounds(GameObject prefab, float scale, out bool lengthIsZ)
    {
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        tmp.transform.position = Vector3.zero;
        tmp.transform.rotation = Quaternion.identity;
        tmp.transform.localScale = Vector3.one * scale;
        Bounds b = WorldBounds(tmp);
        lengthIsZ = b.size.z >= b.size.x;
        DestroyImmediate(tmp);
        return b;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static GameObject FindPrefab(string nameNoExt)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{nameNoExt} t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == nameNoExt)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
