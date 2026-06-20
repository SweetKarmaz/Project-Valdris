using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build Greyspire Blockout  (Phase G1 spine)
//
// Rooms (floors/walls/ceilings) separated by TUNNEL hallways. No tile stretching
// (walls stack; ceilings sit at a whole number of wall heights). Shared walls
// between adjacent rooms use ONE double-sided piece. Tunnels get floors + door-
// frame end caps. Tunnel-clad stairs rise to the Warden level. Re-runnable.
public class GreyspireBlockoutBuilder
{
    const float T = 5f;
    const float WallSink = 0.15f;
    const float TunnelTighten = 0.22f;   // pull tunnel pieces this much closer
    const string DG = "Assets/Synty/PolygonDungeon/Prefabs/Environments";

    static readonly string[] Walls =
    {
        DG + "/Walls/SM_Env_Wall_01.prefab",
        DG + "/Walls/SM_Env_Wall_01_Alt.prefab",
        DG + "/Walls/SM_Env_Wall_03.prefab",
        DG + "/Walls/SM_Env_Wall_04.prefab",
    };
    const string DoubleWall = DG + "/Walls/SM_Env_Wall_01_DoubleSided.prefab";
    const string DoorFrame  = DG + "/Walls/SM_Env_Wall_DoorFrame_01.prefab";
    static readonly string[] Floors =
    {
        DG + "/Floors/SM_Env_Tiles_01.prefab", DG + "/Floors/SM_Env_Tiles_02.prefab",
        DG + "/Floors/SM_Env_Tiles_03.prefab", DG + "/Floors/SM_Env_Tiles_09.prefab",
    };
    static readonly string[] Ceilings =
    {
        DG + "/Walls/SM_Env_Ceiling_Stone_Flat_01.prefab",
        DG + "/Walls/SM_Env_Ceiling_Stone_Flat_02.prefab",
        DG + "/Walls/SM_Env_Ceiling_Stone_Flat_03.prefab",
    };
    const string Tunnel = DG + "/Walls/SM_Env_Wall_Tunnel_01.prefab";
    const string Stair  = DG + "/Floors/SM_Env_Stairs_01.prefab";

    static string Pick(string[] arr, float x, float z) =>
        arr[Mathf.Abs(Mathf.RoundToInt(x * 7 + z * 13)) % arr.Length];

    enum Style { Rough, Stone }
    enum SnapY { Center, Base, Top }

    class Room
    {
        public string Name;
        public float X0, Z0, X1, Z1, FloorY;
        public int Rows = 1;                 // height in wall-piece units (no stretching)
        public Style Style = Style.Rough;
        public int[] N = E0, S = E0, Ea = E0, W = E0;
        static readonly int[] E0 = new int[0];
    }

    static readonly List<Room> Rooms = new()
    {
        new Room { Name="Cell Block A", X0=0,  Z0=-20, X1=10, Z1=-5, Rows=1, N=new[]{1} },
        new Room { Name="Common Hall",  X0=0,  Z0=5,   X1=30, Z1=25, Rows=2, S=new[]{1}, W=new[]{2}, N=new[]{1}, Ea=new[]{2} },
        new Room { Name="Cell Block B", X0=5,  Z0=40,  X1=15, Z1=55, Rows=1, S=new[]{0} },

        // Overhear cell (1x1x1) — south side of the hallway junction.
        new Room { Name="Overhear Cell", X0=45, Z0=10, X1=50, Z1=15, Rows=1, N=new[]{0} },

        // Guard wing (north side of the junction). Adjacent rooms → shared double-sided walls.
        new Room { Name="Guardroom", X0=45, Z0=20, X1=60, Z1=35, Rows=1, Style=Style.Stone, S=new[]{0}, Ea=new[]{1} },
        new Room { Name="Barracks",  X0=60, Z0=20, X1=70, Z1=35, Rows=1, Style=Style.Stone, W=new[]{1}, Ea=new[]{1} },
        new Room { Name="Storage",   X0=70, Z0=20, X1=80, Z1=30, Rows=1, Style=Style.Stone, W=new[]{1} },

        // Warden level (y=10), reached by the tunnel-clad stairs.
        new Room { Name="Upper Guardroom", X0=90, Z0=10, X1=108, Z1=27, FloorY=10, Rows=1, Style=Style.Stone, W=new[]{1}, N=new[]{1}, Ea=new[]{2} },
        new Room { Name="Warden's Office",  X0=108,Z0=12, X1=124, Z1=27, FloorY=10, Rows=1, Style=Style.Stone, W=new[]{1} },
        new Room { Name="Exit",             X0=90, Z0=27, X1=108, Z1=35, FloorY=10, Rows=1, Style=Style.Stone, S=new[]{1} },
    };

    static Transform _root;
    static float _wallH = 4f, _tunLen = 5f, _stairRise = 2f, _stairRun = 5f;
    static readonly Dictionary<string, WallReq> _wallReqs = new();

    struct WallReq { public Vector3 target; public float yaw; public int rows; public bool ns; public int count; }

    [MenuItem("Tools/Valdris/Scene/Build Greyspire Blockout")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Greyspire Blockout",
            "Rebuilds the G1 spine (rooms + tunnels + stairs, no props).\n\nContinue?", "Build", "Cancel")) return;

        Scene scene = SceneManager.GetActiveScene();
        var existing = GameObject.Find("Greyspire_Blockout");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        _root = new GameObject("Greyspire_Blockout").transform;
        SceneManager.MoveGameObjectToScene(_root.gameObject, scene);
        Undo.RegisterCreatedObjectUndo(_root.gameObject, "Build Greyspire Blockout");

        _wallH = MeasureBounds(Walls[0]).size.y;
        var tb = MeasureBounds(Tunnel); _tunLen = Mathf.Max(tb.size.x, tb.size.z);
        var sb = MeasureBounds(Stair);  _stairRise = Mathf.Max(0.5f, sb.size.y); _stairRun = Mathf.Max(sb.size.x, sb.size.z);
        _wallReqs.Clear();

        // Floors + ceilings now; collect wall segments to dedupe shared walls.
        foreach (var r in Rooms)
        {
            var g = Sub(r.Name);
            BuildFloor(r, g);
            BuildCeiling(r, g);
            CollectWalls(r);
        }
        BuildCollectedWalls(Sub("Walls"));

        // Tunnels + junction.
        var tun = Sub("Tunnels");
        TunnelRun(new Vector3(7.5f, 0, -5f),  Vector3.forward, 2, 0, tun, true);   // Cell A → Common
        TunnelRun(new Vector3(0f,   0, 17.5f),Vector3.left,    4, 0, tun, true);   // Mine dead-end
        TunnelRun(new Vector3(7.5f, 0, 25f),  Vector3.forward, 3, 0, tun, true);   // Cell B → Common
        TunnelRun(new Vector3(30f,  0, 17.5f),Vector3.right,   3, 0, tun, true);   // Common → junction
        BuildJunction(47.5f, 17.5f, 0, tun);                                       // 1x1 doorframe hub on the hallway line
        TunnelRun(new Vector3(50f,  0, 17.5f),Vector3.right,   3, 0, tun, true);   // junction → hallway end
        BuildJunction(67.5f, 17.5f, 0, tun);                                       // hallway-end junction; stairs start here

        var stairs = Sub("Stairs");
        StairRun(new Vector3(70f, 0, 17.5f), Vector3.right, 10f, stairs);          // stairwell up (walls + ceilings)

        var start = new GameObject("PlayerStart (set SceneGameManager spawn here)");
        start.transform.SetParent(_root);
        start.transform.position = new Vector3(5f, 0.1f, -16f);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[GreyspireBlockout] Rebuilt. Send transforms for any tunnel/stair/doorframe tweaks.");
    }

    // ── Floors / ceilings ──────────────────────────────────────────────────────
    static float RowStep => _wallH - WallSink;          // rows overlap by WallSink (no gaps)

    static void BuildFloor(Room r, Transform parent)
    {
        var fp = Sub("Floor", parent);
        for (float x = r.X0; x < r.X1 - 0.01f; x += T)
        for (float z = r.Z0; z < r.Z1 - 0.01f; z += T)
            PlaceSnapped(Pick(Floors, x, z), new Vector3(x + T*0.5f, r.FloorY, z + T*0.5f), Quaternion.identity, fp, SnapY.Top);
    }

    static void BuildCeiling(Room r, Transform parent)
    {
        var cp = Sub("Ceiling", parent);
        float y = r.FloorY - WallSink + r.Rows * RowStep;   // sits on the wall stack
        for (float x = r.X0; x < r.X1 - 0.01f; x += T)
        for (float z = r.Z0; z < r.Z1 - 0.01f; z += T)
            PlaceSnapped(Pick(Ceilings, x, z), new Vector3(x + T*0.5f, y, z + T*0.5f), Quaternion.identity, cp, SnapY.Center);
    }

    // ── Walls (collect + dedupe shared → double-sided) ──────────────────────────
    static void CollectWalls(Room r)
    {
        int nx = Mathf.RoundToInt((r.X1 - r.X0) / T);
        int nz = Mathf.RoundToInt((r.Z1 - r.Z0) / T);
        // Inward yaws after the N/S flip: N=180, S=0, W=90, E=270.
        for (int i = 0; i < nx; i++)
        {
            float cx = r.X0 + (i + 0.5f) * T;
            if (!r.S.Contains(i))  AddWall(new Vector3(cx, r.FloorY, r.Z0), 0,   r.Rows, true);
            if (!r.N.Contains(i))  AddWall(new Vector3(cx, r.FloorY, r.Z1), 180, r.Rows, true);
        }
        for (int i = 0; i < nz; i++)
        {
            float cz = r.Z0 + (i + 0.5f) * T;
            if (!r.W.Contains(i))  AddWall(new Vector3(r.X0, r.FloorY, cz), 90,  r.Rows, false);
            if (!r.Ea.Contains(i)) AddWall(new Vector3(r.X1, r.FloorY, cz), 270, r.Rows, false);
        }
    }

    static void AddWall(Vector3 target, float yaw, int rows, bool ns)
    {
        string key = $"{(ns ? "NS" : "WE")}:{Mathf.RoundToInt(target.x)}:{Mathf.RoundToInt(target.z)}:{Mathf.RoundToInt(target.y)}";
        if (_wallReqs.TryGetValue(key, out var w)) { w.count++; w.rows = Mathf.Max(w.rows, rows); _wallReqs[key] = w; }
        else _wallReqs[key] = new WallReq { target = target, yaw = yaw, rows = rows, ns = ns, count = 1 };
    }

    static void BuildCollectedWalls(Transform parent)
    {
        foreach (var w in _wallReqs.Values)
        {
            bool shared = w.count >= 2;
            float yaw = shared ? (w.ns ? 0f : 90f) : w.yaw;
            for (int k = 0; k < w.rows; k++)
            {
                float y = w.target.y - WallSink + k * RowStep;
                if (shared)
                    PlaceSnapped(DoubleWall, new Vector3(w.target.x, y, w.target.z), Quaternion.Euler(0, yaw, 0), parent, SnapY.Base);
                else
                {
                    int idx = Mathf.Abs(Mathf.RoundToInt(w.target.x * 7 + w.target.z * 13 + yaw + k)) % Walls.Length;
                    PlaceSnapped(Walls[idx], new Vector3(w.target.x, y, w.target.z), Quaternion.Euler(0, yaw, 0), parent, SnapY.Base);
                }
            }
        }
    }

    // ── Tunnels / junction / stairs ─────────────────────────────────────────────
    static void TunnelRun(Vector3 start, Vector3 dir, int count, float yawExtra, Transform parent, bool floor)
    {
        float yaw = DirYaw(dir) + yawExtra;
        float step = _tunLen - TunnelTighten;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = start + dir * (step * i + _tunLen * 0.5f);
            PlaceSnapped(Tunnel, new Vector3(p.x, start.y - WallSink, p.z), Quaternion.Euler(0, yaw, 0), parent, SnapY.Base);
            if (floor) PlaceSnapped(Pick(Floors, p.x, p.z), new Vector3(p.x, start.y, p.z), Quaternion.identity, parent, SnapY.Top);
        }
        // Door-frame caps at both ends (across the tunnel mouth).
        PlaceSnapped(DoorFrame, new Vector3(start.x, start.y - WallSink, start.z) + dir * 0.1f, Quaternion.Euler(0, yaw, 0), parent, SnapY.Base);
        PlaceSnapped(DoorFrame, new Vector3(start.x, start.y - WallSink, start.z) + dir * (step * (count - 1) + _tunLen - 0.1f), Quaternion.Euler(0, yaw, 0), parent, SnapY.Base);
    }

    static void BuildJunction(float cx, float cz, float floorY, Transform parent)
    {
        var g = Sub("Hallway Junction", parent);
        PlaceSnapped(Pick(Floors, cx, cz), new Vector3(cx, floorY, cz), Quaternion.identity, g, SnapY.Top);
        PlaceSnapped(Pick(Ceilings, cx, cz), new Vector3(cx, floorY - WallSink + RowStep, cz), Quaternion.identity, g, SnapY.Center);
        PlaceSnapped(DoorFrame, new Vector3(cx, floorY - WallSink, cz - T*0.5f), Quaternion.Euler(0, 0,   0), g, SnapY.Base);  // S
        PlaceSnapped(DoorFrame, new Vector3(cx, floorY - WallSink, cz + T*0.5f), Quaternion.Euler(0, 180, 0), g, SnapY.Base);  // N
        PlaceSnapped(DoorFrame, new Vector3(cx - T*0.5f, floorY - WallSink, cz), Quaternion.Euler(0, 90,  0), g, SnapY.Base);  // W
        PlaceSnapped(DoorFrame, new Vector3(cx + T*0.5f, floorY - WallSink, cz), Quaternion.Euler(0, 270, 0), g, SnapY.Base);  // E
    }

    static void StairRun(Vector3 basePos, Vector3 dir, float height, Transform parent)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(height / _stairRise));
        float yaw = DirYaw(dir) + 180f;                            // ascend along the run
        Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;  // stairwell sides
        float halfW = T * 0.5f;
        float leftYaw = DirYaw(-perp), rightYaw = DirYaw(perp);
        float pitch = Mathf.Atan2(_stairRise, _stairRun) * Mathf.Rad2Deg;   // ceiling tilt to match the slope
        for (int i = 0; i < steps; i++)
        {
            Vector3 p = basePos + dir * (_stairRun * i) + Vector3.up * (_stairRise * i);
            PlaceSnapped(Stair, p, Quaternion.Euler(0, yaw, 0), parent, SnapY.Base);
            // Two rows of wall each side so the tilted ceiling has wall to meet.
            for (int row = 0; row < 2; row++)
            {
                Vector3 up = Vector3.up * (row * RowStep);
                PlaceSnapped(Walls[0], p + perp * halfW + Vector3.down * WallSink + up, Quaternion.Euler(0, leftYaw, 0), parent, SnapY.Base);
                PlaceSnapped(Walls[0], p - perp * halfW + Vector3.down * WallSink + up, Quaternion.Euler(0, rightYaw, 0), parent, SnapY.Base);
            }
            // Angled ceiling, two rows up, tilted to follow the stairs.
            Quaternion ceilRot = Quaternion.AngleAxis(-pitch, perp);
            PlaceSnapped(Pick(Ceilings, p.x, p.z), p + Vector3.up * (2f * RowStep), ceilRot, parent, SnapY.Center);
        }
    }

    static float DirYaw(Vector3 dir)
    {
        if (dir == Vector3.right) return 90f;
        if (dir == Vector3.left)  return 270f;
        if (dir == Vector3.back)  return 180f;
        return 0f;
    }

    // ── Utilities ────────────────────────────────────────────────────────────────
    static Transform Sub(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : _root);
        return go.transform;
    }

    static void PlaceSnapped(string prefab, Vector3 target, Quaternion rot, Transform parent, SnapY ys)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (p == null) { Debug.LogWarning($"[GreyspireBlockout] Missing: {prefab}"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        go.transform.SetPositionAndRotation(target, rot);
        Bounds b = WorldBounds(go);
        float dy = ys == SnapY.Base ? target.y - b.min.y : ys == SnapY.Top ? target.y - b.max.y : target.y - b.center.y;
        go.transform.position += new Vector3(target.x - b.center.x, dy, target.z - b.center.z);
    }

    static Bounds MeasureBounds(string prefab)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (p == null) return new Bounds(Vector3.zero, Vector3.one * 5f);
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(p);
        var b = WorldBounds(tmp);
        Object.DestroyImmediate(tmp);
        return b;
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
