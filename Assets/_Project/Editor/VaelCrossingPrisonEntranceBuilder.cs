using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Prison Entrance
//
// PHASE 6 of the VaelCrossing world build: the Greyspire prison entrance at the
// top of the mountain pass — a former mine set into the rock, with a carved
// "Greyspire Prison" sign, the arrival SpawnPoint (from Greyspire), and an exit
// Gateway back to Greyspire (destination left blank until Greyspire is rebuilt).
// Re-runnable; nudge the position/yaw to seat it against the mountain.
public class VaelCrossingPrisonEntranceBuilder : EditorWindow
{
    const string TerrainObject = "VaelCrossing Terrain";
    const string Root          = "VaelCrossing Prison Entrance";
    const string TunnelPrefab  = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Wall_Tunnel_Entrance_01.prefab";
    const string RockDir        = "Assets/Synty/PolygonNature/Prefabs/Rocks";
    const string GreyspireScene = "Act1_Part1_Greyspire";

    float entranceU = 0.05f;   // near the west end of the pass
    float entranceV = 0.5f;    // mid (the pass corridor)
    float yaw       = 90f;     // opening faces east (down the pass)
    float embedDepth = 1.0f;   // sink into the slope
    float tunnelScale = 1.5f;
    string signText = "GREYSPIRE PRISON";
    float spawnDistance = 8f;  // SpawnPoint this far in front (east) of the door

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Prison Entrance")]
    static void Open() => GetWindow<VaelCrossingPrisonEntranceBuilder>("Prison Entrance");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Phase 6 — Greyspire prison entrance, arrival SpawnPoint, and exit " +
            "Gateway (destination blank until Greyspire is rebuilt). Re-runnable.", MessageType.Info);
        entranceU = EditorGUILayout.Slider("Position W→E (u)", entranceU, 0f, 0.4f);
        entranceV = EditorGUILayout.Slider("Position S→N (v)", entranceV, 0.2f, 0.8f);
        yaw       = EditorGUILayout.Slider("Facing (yaw)", yaw, 0f, 360f);
        embedDepth = EditorGUILayout.Slider("Embed Depth", embedDepth, 0f, 5f);
        tunnelScale = EditorGUILayout.Slider("Entrance Scale", tunnelScale, 0.5f, 4f);
        spawnDistance = EditorGUILayout.Slider("Spawn Distance", spawnDistance, 2f, 25f);
        signText = EditorGUILayout.TextField("Sign Text", signText);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build / Rebuild Entrance", GUILayout.Height(34))) Build();
    }

    void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject) ?? Terrain.activeTerrain;
        if (terrain == null) { EditorUtility.DisplayDialog("Prison Entrance", "No VaelCrossing Terrain found.", "OK"); return; }
        Vector3 size = terrain.terrainData.size, origin = terrain.transform.position;

        var tunnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TunnelPrefab);
        if (tunnelPrefab == null) { EditorUtility.DisplayDialog("Prison Entrance", "Tunnel entrance prefab not found.", "OK"); return; }

        var old = GameObject.Find(Root);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(Root);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create Prison Entrance");

        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;     // "out of the door" = east/down-pass
        Vector3 right = rot * Vector3.right;
        Vector3 ePos = Ground(terrain, origin, size, entranceU, entranceV);

        // ── Tunnel entrance (sunk into the slope) ──────────────────────────────
        var tunnel = (GameObject)PrefabUtility.InstantiatePrefab(tunnelPrefab, root.transform);
        tunnel.transform.localScale = Vector3.one * tunnelScale;
        tunnel.transform.rotation   = rot;
        Bounds tb = WorldBounds(tunnel);
        // Seat its base on the ground, then sink by embedDepth.
        float baseOffset = tunnel.transform.position.y - tb.min.y;
        tunnel.transform.position = ePos + Vector3.up * (baseOffset - embedDepth);
        tb = WorldBounds(tunnel);

        // ── Framing rocks (nestle into the mountain) ───────────────────────────
        var rocks = AssetDatabase.FindAssets("SM_Rock_Cluster_Large t:Prefab", new[] { RockDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null).ToList();
        if (rocks.Count > 0)
        {
            float w = (tb.max - tb.min).magnitude * 0.35f;
            PlaceRock(rocks, root, ePos + right * w  - fwd * 1f, w);
            PlaceRock(rocks, root, ePos - right * w  - fwd * 1f, w);
            PlaceRock(rocks, root, ePos + Vector3.up * (tb.size.y * 0.8f) - fwd * 1.5f, w * 1.3f);
            PlaceRock(rocks, root, ePos + right * w * 1.6f, w);
            PlaceRock(rocks, root, ePos - right * w * 1.6f, w);
        }

        // ── Carved "Greyspire Prison" sign above the door ──────────────────────
        BuildSign(root, ePos + Vector3.up * (tb.size.y + 0.6f) + fwd * 0.2f, rot);

        // ── Arrival SpawnPoint (in front, facing down the pass) ────────────────
        Vector3 spWorld = ePos + fwd * spawnDistance;
        Vector3 spGround = Ground(terrain, origin, size,
            (spWorld.x - origin.x) / size.x, (spWorld.z - origin.z) / size.z);
        var spGo = new GameObject("SpawnPoint - From Greyspire");
        spGo.transform.SetParent(root.transform);
        spGo.transform.SetPositionAndRotation(spGround + Vector3.up * 0.1f, rot);
        var sp = spGo.AddComponent<SpawnPoint>();
        sp.spawnId = "FromGreyspire";
        sp.zoneDisplayName = "Vael Crossing";

        // ── Exit Gateway back into Greyspire (destination left blank) ──────────
        var gwGo = new GameObject("Gateway - To Greyspire");
        gwGo.transform.SetParent(root.transform);
        gwGo.transform.position = ePos + fwd * 1.5f;
        var gw = gwGo.AddComponent<Gateway>();
        gw.trigger = GatewayTrigger.Interact;
        gw.action  = GatewayAction.LoadScene;
        gw.targetSceneName = GreyspireScene;
        gw.destinationSpawnId = "";          // set after Greyspire is rebuilt
        gw.interactionLabel = "Enter Greyspire";
        gw.requireConfirmation = false;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[PrisonEntrance] Built entrance + SpawnPoint 'FromGreyspire' + exit Gateway " +
                  "(destination blank). Nudge Position/Facing to seat it against the mountain.");
    }

    void PlaceRock(System.Collections.Generic.List<GameObject> rocks, GameObject root, Vector3 pos, float scale)
    {
        var r = (GameObject)PrefabUtility.InstantiatePrefab(rocks[Random.Range(0, rocks.Count)], root.transform);
        r.transform.position = pos;
        r.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
        r.transform.localScale = Vector3.one * Mathf.Max(1f, scale * 0.15f) * Random.Range(0.8f, 1.3f);
    }

    void BuildSign(GameObject root, Vector3 pos, Quaternion rot)
    {
        // Stone slab backing.
        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(slab.GetComponent<Collider>());
        slab.name = "Sign Slab";
        slab.transform.SetParent(root.transform);
        slab.transform.position = pos;
        slab.transform.rotation = rot;
        slab.transform.localScale = new Vector3(6f, 1.4f, 0.4f);
        var sh = Shader.Find("HDRP/Lit");
        if (sh != null)
        {
            var m = new Material(sh) { name = "VC_SignStone" };
            m.SetColor("_BaseColor", new Color(0.32f, 0.31f, 0.29f));
            m.SetFloat("_Smoothness", 0.1f);
            slab.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        // Carved text (TMP 3D) on the face.
        var txtGo = new GameObject("Sign Text");
        txtGo.transform.SetParent(root.transform);
        txtGo.transform.position = pos + rot * new Vector3(0f, 0f, -0.22f);
        txtGo.transform.rotation = rot * Quaternion.Euler(0f, 180f, 0f);   // face outward
        var tmp = txtGo.AddComponent<TextMeshPro>();
        tmp.text = signText;
        tmp.fontSize = 6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.08f, 0.07f, 0.06f);     // dark engraved
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null) tmp.font = font;
        tmp.rectTransform.sizeDelta = new Vector2(5.6f, 1.2f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    static Vector3 Ground(Terrain terrain, Vector3 origin, Vector3 size, float u, float v)
    {
        float x = origin.x + Mathf.Clamp01(u) * size.x;
        float z = origin.z + Mathf.Clamp01(v) * size.z;
        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
        return new Vector3(x, y, z);
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
