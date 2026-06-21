using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build Greyspire Dressing  (Phase G3)
//
// Lights + props for the Greyspire blockout. Lives in its OWN parent
// (Greyspire_Dressing) so re-running the G1 blockout builder never wipes it and
// vice-versa. Re-runnable: destroys+rebuilds only Greyspire_Dressing. Manual
// doors/addons in Greyspire_Blockout_Addons are untouched.
//
// Layout mirrors GreyspireBlockoutBuilder (5m grid, wall yaws N=180/S=0/W=90/E=270,
// door openings via the per-wall index arrays). Torch yaws may need a flip once
// seen in-scene — tweak TorchYawOffset and re-run.
public class GreyspireDressingBuilder
{
    const float T = 5f;
    const float WallH = 4f;
    const float TorchYawOffset = 0f;     // add 180 here if torches face into the wall

    const string DG = "Assets/Synty/PolygonDungeon/Prefabs";

    // ── Lights ──────────────────────────────────────────────────────────────────
    const string Torch   = DG + "/Props/SM_Prop_Torch_Ornate_01.prefab";
    const string Brazier = DG + "/Props/SM_Prop_Brazier_01.prefab";

    // ── Props ───────────────────────────────────────────────────────────────────
    const string Bed1 = DG + "/Props/SM_Prop_Bed_01.prefab";
    const string Bed2 = DG + "/Props/SM_Prop_Bed_02.prefab";
    const string Table = DG + "/Props/SM_Prop_Table_01.prefab";
    const string StoneTable = DG + "/Props/SM_Prop_StoneTable_01.prefab";
    const string StoneChair = DG + "/Props/SM_Prop_StoneChair_01.prefab";
    const string Bench = DG + "/Props/SM_Prop_Bench_01.prefab";
    const string WeaponRack = DG + "/Props/SM_Prop_WeaponRack_01.prefab";
    const string Cage = DG + "/Props/SM_Prop_Toture_Cage_01.prefab";
    const string Stocks = DG + "/Props/SM_Prop_Toture_Stocks_01.prefab";
    const string Minecart = DG + "/Props/SM_Prop_Minecart_01.prefab";
    const string Candle = DG + "/Props/SM_Prop_Candle_01.prefab";
    const string Rug = DG + "/Props/SM_Prop_Rug_03.prefab";

    static readonly string[] Stools = { DG + "/Props/SM_Prop_Stool_01.prefab", DG + "/Props/SM_Prop_Stool_02.prefab", DG + "/Props/SM_Prop_Stool_03.prefab" };
    static readonly string[] Barrels = { DG + "/Props/SM_Prop_Barrel_01.prefab", DG + "/Props/SM_Prop_Barrel_02.prefab", DG + "/Props/SM_Prop_Barrel_03.prefab" };
    static readonly string[] WoodCrates = { DG + "/Props/SM_Prop_Crate_Wood_01.prefab", DG + "/Props/SM_Prop_Crate_Wood_02.prefab", DG + "/Props/SM_Prop_Crate_Wood_03.prefab", DG + "/Props/SM_Prop_Crate_Wood_04.prefab", DG + "/Props/SM_Prop_Crate_Wood_05.prefab" };
    static readonly string[] MetalCrates = { DG + "/Props/SM_Prop_Crate_Metal_01.prefab", DG + "/Props/SM_Prop_Crate_Metal_02.prefab", DG + "/Props/SM_Prop_Crate_Metal_03.prefab" };
    static readonly string[] Bookcases = { DG + "/Props/SM_Prop_Bookcase_01.prefab", DG + "/Props/SM_Prop_Bookcase_02.prefab", DG + "/Props/SM_Prop_Bookcase_03.prefab" };
    static readonly string[] Chests = { DG + "/Props/SM_Prop_Chest_01.prefab", DG + "/Props/SM_Prop_Chest_02.prefab", DG + "/Props/SM_Prop_Chest_03.prefab", DG + "/Props/SM_Prop_Chest_04.prefab" };
    static readonly string[] Banners = { DG + "/Props/SM_Prop_Wall_Banner_01.prefab", DG + "/Props/SM_Prop_Wall_Banner_02.prefab", DG + "/Props/SM_Prop_Wall_Banner_03.prefab", DG + "/Props/SM_Prop_Wall_Banner_05.prefab" };
    static readonly string[] Chains = { DG + "/Props/SM_Prop_Chain_01.prefab", DG + "/Props/SM_Prop_Chain_03.prefab", DG + "/Props/SM_Prop_Chain_05.prefab" };
    static readonly string[] Rubble = { DG + "/Environments/Rocks/SM_Env_Brick_Rubble_02.prefab", DG + "/Environments/Rocks/SM_Env_Brick_Rubble_05.prefab", DG + "/Environments/Rocks/SM_Env_RockPile_Square_01.prefab" };
    static readonly string[] MineTrack = { DG + "/Environments/Wood/SM_Env_Minetrack_Straight_01.prefab" };

    enum Style { Rough, Stone }
    enum SnapY { Center, Base, Top }

    class Room
    {
        public string Name;
        public float X0, Z0, X1, Z1, FloorY;
        public int Rows = 1;
        public Style Style = Style.Rough;
        public int[] N = E0, S = E0, Ea = E0, W = E0;
        static readonly int[] E0 = new int[0];
        public float CX => (X0 + X1) * 0.5f;
        public float CZ => (Z0 + Z1) * 0.5f;
    }

    // Mirror of the blockout rooms (coords + wall openings) so dressing lines up.
    static readonly List<Room> Rooms = new()
    {
        new Room { Name="Cell Block A", X0=0,  Z0=-20, X1=10, Z1=-5, Rows=1, N=new[]{1} },
        new Room { Name="Common Hall",  X0=0,  Z0=5,   X1=30, Z1=25, Rows=2, S=new[]{1}, W=new[]{2}, N=new[]{1}, Ea=new[]{2} },
        new Room { Name="Cell Block B", X0=5,  Z0=40,  X1=15, Z1=55, Rows=1, S=new[]{0} },
        new Room { Name="Overhear Cell", X0=45, Z0=10, X1=50, Z1=15, Rows=1, N=new[]{0} },
        new Room { Name="Guardroom", X0=45, Z0=20, X1=60, Z1=35, Rows=1, Style=Style.Stone, S=new[]{0}, Ea=new[]{1} },
        new Room { Name="Barracks",  X0=60, Z0=20, X1=70, Z1=35, Rows=1, Style=Style.Stone, W=new[]{1}, Ea=new[]{1} },
        new Room { Name="Storage",   X0=70, Z0=20, X1=80, Z1=30, Rows=1, Style=Style.Stone, W=new[]{1} },
        new Room { Name="Upper Guardroom", X0=90, Z0=10, X1=108, Z1=27, FloorY=10, Rows=1, Style=Style.Stone, W=new[]{1}, N=new[]{1}, Ea=new[]{2} },
        new Room { Name="Warden's Office",  X0=108,Z0=12, X1=124, Z1=27, FloorY=10, Rows=1, Style=Style.Stone, W=new[]{1} },
        new Room { Name="Exit",             X0=90, Z0=27, X1=108, Z1=35, FloorY=10, Rows=1, Style=Style.Stone, S=new[]{1} },
    };

    static Transform _root;

    [MenuItem("Tools/Valdris/Scene/Build Greyspire Dressing")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Greyspire Dressing",
            "Adds torches + props to the blockout (own parent 'Greyspire_Dressing').\n\n" +
            "Re-running only rebuilds dressing; the blockout and manual addons are left alone.\n\nContinue?",
            "Build", "Cancel")) return;

        Scene scene = SceneManager.GetActiveScene();
        var existing = GameObject.Find("Greyspire_Dressing");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        _root = new GameObject("Greyspire_Dressing").transform;
        SceneManager.MoveGameObjectToScene(_root.gameObject, scene);
        Undo.RegisterCreatedObjectUndo(_root.gameObject, "Build Greyspire Dressing");

        var torchParent = Sub("Torches");
        var propParent = Sub("Props");

        foreach (var r in Rooms)
        {
            BuildTorches(r, torchParent);
            BuildProps(r, Sub(r.Name, propParent));
        }

        BuildMine(Sub("Mine", propParent), torchParent);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[GreyspireDressing] Built torches + props. Tweak TorchYawOffset and re-run if torches face the wrong way.");
    }

    // ── Torches (perimeter, skipping door openings) ─────────────────────────────
    static void BuildTorches(Room r, Transform parent)
    {
        Color col; float intensity, range;
        switch (r.Name)
        {
            case "Warden's Office":  col = new Color(1f, 0.82f, 0.50f); intensity = 1100f; range = 16f; break;
            case "Upper Guardroom":  col = new Color(1f, 0.80f, 0.55f); intensity = 950f;  range = 16f; break;
            case "Cell Block A":
            case "Cell Block B":
            case "Overhear Cell":    col = new Color(1f, 0.55f, 0.22f); intensity = 450f;  range = 11f; break;
            default:                 col = new Color(1f, 0.62f, 0.28f); intensity = 800f;  range = r.Rows > 1 ? 15f : 11f; break;
        }

        float torchY = r.FloorY + WallH * (r.Rows > 1 ? 1.0f : 0.62f);
        var tp = Sub(r.Name, parent);

        int nx = Mathf.RoundToInt((r.X1 - r.X0) / T);
        int nz = Mathf.RoundToInt((r.Z1 - r.Z0) / T);
        const float inset = 0.35f;

        // Place on roughly every other tile so torches aren't on every 5m.
        for (int i = 0; i < nx; i++)
        {
            if (i % 2 != 0 && nx > 2) continue;
            float cx = r.X0 + (i + 0.5f) * T;
            if (!Contains(r.S, i)) PlaceTorch(tp, new Vector3(cx, torchY, r.Z0 + inset), 0   + TorchYawOffset, col, intensity, range);
            if (!Contains(r.N, i)) PlaceTorch(tp, new Vector3(cx, torchY, r.Z1 - inset), 180 + TorchYawOffset, col, intensity, range);
        }
        for (int i = 0; i < nz; i++)
        {
            if (i % 2 != 0 && nz > 2) continue;
            float cz = r.Z0 + (i + 0.5f) * T;
            if (!Contains(r.W, i))  PlaceTorch(tp, new Vector3(r.X0 + inset, torchY, cz), 90  + TorchYawOffset, col, intensity, range);
            if (!Contains(r.Ea, i)) PlaceTorch(tp, new Vector3(r.X1 - inset, torchY, cz), 270 + TorchYawOffset, col, intensity, range);
        }
    }

    static void PlaceTorch(Transform parent, Vector3 pos, float yaw, Color col, float intensity, float range)
    {
        var go = new GameObject("Torch");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        PlaceMesh(Torch, go.transform, SnapY.Center);

        var lg = new GameObject("Light");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0, 0.25f, 0.4f);   // a touch in front of the bracket
        var lt = lg.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = col;
        lt.intensity = intensity;
        lt.range = range;
        lt.shadows = LightShadows.None;
        lg.AddComponent<TorchFlicker>();
    }

    // ── Props per room ──────────────────────────────────────────────────────────
    static void BuildProps(Room r, Transform p)
    {
        switch (r.Name)
        {
            case "Cell Block A":
                // Two bunk cells against the side walls + buckets/chains.
                Prop(Bed1, Corner(r, 0.18f, 0.20f), Yaw(r, 90), p);
                Prop(Bed2, Corner(r, 0.82f, 0.20f), Yaw(r, 270), p);
                Prop(Barrels[0], Corner(r, 0.12f, 0.70f), 0, p);
                Wall(Chains[0], r, 0.5f, 1f, 180, 2.6f, p);
                break;

            case "Cell Block B":
                Prop(Bed2, Corner(r, 0.20f, 0.80f), Yaw(r, 90), p);
                Prop(Barrels[1], Corner(r, 0.80f, 0.78f), 0, p);
                Wall(Chains[1], r, 0.5f, 0f, 0, 2.6f, p);
                ScatterRubble(r, p, 2);
                break;

            case "Common Hall":
                // Long mess tables down the middle with benches + scattered barrels/crates.
                Prop(Table, new Vector3(r.CX - 5, r.FloorY, r.CZ), 0, p);
                Prop(Table, new Vector3(r.CX + 5, r.FloorY, r.CZ), 0, p);
                Prop(Bench, new Vector3(r.CX - 5, r.FloorY, r.CZ - 1.6f), 0, p);
                Prop(Bench, new Vector3(r.CX + 5, r.FloorY, r.CZ + 1.6f), 180, p);
                Prop(Brazier, new Vector3(r.CX, r.FloorY, r.CZ), 0, p);
                Prop(Stools[0], new Vector3(r.CX - 5, r.FloorY, r.CZ + 1.6f), 0, p);
                Prop(Stools[1], new Vector3(r.CX + 5, r.FloorY, r.CZ - 1.6f), 0, p);
                Prop(Barrels[0], Corner(r, 0.08f, 0.10f), 0, p);
                Prop(Barrels[2], Corner(r, 0.92f, 0.90f), 0, p);
                Prop(WoodCrates[1], Corner(r, 0.10f, 0.90f), 20, p);
                break;

            case "Overhear Cell":
                // Solitary / interrogation: stocks + a cage, grim.
                Prop(Stocks, new Vector3(r.CX, r.FloorY, r.CZ + 0.6f), 0, p);
                Prop(Cage, Corner(r, 0.78f, 0.25f), 0, p);
                Wall(Chains[2], r, 0.5f, 1f, 180, 2.6f, p);
                break;

            case "Guardroom":
                Prop(Table, new Vector3(r.CX, r.FloorY, r.CZ), 0, p);
                Prop(Stools[0], new Vector3(r.CX - 1.6f, r.FloorY, r.CZ), 0, p);
                Prop(Stools[2], new Vector3(r.CX + 1.6f, r.FloorY, r.CZ), 0, p);
                Prop(WeaponRack, Corner(r, 0.15f, 0.85f), Yaw(r, 0), p);
                Prop(MetalCrates[0], Corner(r, 0.85f, 0.85f), 0, p);
                Banner(r, 0.5f, 1f, 180, p);
                break;

            case "Barracks":
                Prop(Bed1, Corner(r, 0.20f, 0.20f), Yaw(r, 90), p);
                Prop(Bed1, Corner(r, 0.20f, 0.80f), Yaw(r, 90), p);
                Prop(Bed2, Corner(r, 0.80f, 0.20f), Yaw(r, 270), p);
                Prop(WeaponRack, Corner(r, 0.80f, 0.80f), Yaw(r, 270), p);
                Prop(Barrels[1], Corner(r, 0.5f, 0.10f), 0, p);
                break;

            case "Storage":
                // Stacks of crates/barrels + chests (loot lives here later).
                Prop(WoodCrates[0], Corner(r, 0.18f, 0.20f), 12, p);
                Prop(WoodCrates[2], Corner(r, 0.30f, 0.22f), -18, p);
                Prop(WoodCrates[3], Corner(r, 0.20f, 0.78f), 8, p);
                Prop(MetalCrates[1], Corner(r, 0.82f, 0.78f), 0, p);
                Prop(Barrels[0], Corner(r, 0.85f, 0.22f), 0, p);
                Prop(Barrels[2], Corner(r, 0.78f, 0.30f), 0, p);
                Prop(Chests[0], Corner(r, 0.5f, 0.82f), 180, p);
                Prop(Bookcases[0], Corner(r, 0.5f, 0.85f), 180, p);
                break;

            case "Upper Guardroom":
                Prop(Table, new Vector3(r.CX - 3, r.FloorY, r.CZ), 0, p);
                Prop(Stools[1], new Vector3(r.CX - 3, r.FloorY, r.CZ + 1.6f), 0, p);
                Prop(WeaponRack, Corner(r, 0.15f, 0.85f), Yaw(r, 0), p);
                Prop(MetalCrates[2], Corner(r, 0.88f, 0.15f), 0, p);
                Banner(r, 0.5f, 1f, 180, p);
                break;

            case "Warden's Office":
                // Desk facing the door, bookcases on the back wall, rug + candles.
                Prop(Rug, new Vector3(r.CX, r.FloorY + 0.02f, r.CZ), 0, p);
                Prop(StoneTable, new Vector3(r.CX + 1.5f, r.FloorY, r.CZ), 90, p);
                Prop(StoneChair, new Vector3(r.CX + 3f, r.FloorY, r.CZ), 270, p);
                Prop(Candle, new Vector3(r.CX + 1.3f, r.FloorY + 0.95f, r.CZ + 0.6f), 0, p);
                Prop(Bookcases[1], Corner(r, 0.92f, 0.25f), Yaw(r, 270), p);
                Prop(Bookcases[2], Corner(r, 0.92f, 0.75f), Yaw(r, 270), p);
                Banner(r, 0.5f, 1f, 180, p);
                Banner(r, 0.5f, 0f, 0, p);
                break;

            case "Exit":
                // Sparse — rubble hinting the coming cave-in.
                ScatterRubble(r, p, 3);
                Prop(Barrels[2], Corner(r, 0.15f, 0.15f), 0, p);
                break;
        }
    }

    // ── Mine (the west dead-end tunnel off Common Hall, z=17.5) ─────────────────
    static void BuildMine(Transform p, Transform torchParent)
    {
        // Tunnel runs from the Common Hall west wall (x=0) toward x=-20 at z=17.5.
        Prop(MineTrack[0], new Vector3(-4f,  0, 17.5f), 90, p);
        Prop(MineTrack[0], new Vector3(-9f,  0, 17.5f), 90, p);
        Prop(MineTrack[0], new Vector3(-14f, 0, 17.5f), 90, p);
        Prop(Minecart, new Vector3(-8f, 0, 17.5f), 90, p);
        Prop(WoodCrates[2], new Vector3(-3f, 0, 16f), 30, p);
        // Cave-in rubble sealing the dead end (the "accident").
        Prop(Rubble[2], new Vector3(-18f, 0, 17.5f), 0, p);
        Prop(Rubble[0], new Vector3(-17f, 0, 16.5f), 40, p);
        Prop(Rubble[1], new Vector3(-17.5f, 0, 18.5f), 200, p);

        var mineTorch = Sub("Mine", torchParent);
        PlaceTorch(mineTorch, new Vector3(-6f, 2.6f, 19.6f), 180, new Color(1f, 0.6f, 0.25f), 600f, 11f);
        PlaceTorch(mineTorch, new Vector3(-12f, 2.6f, 15.4f), 0, new Color(1f, 0.6f, 0.25f), 600f, 11f);
    }

    // ── Placement helpers ───────────────────────────────────────────────────────

    // Position at a fraction across the room (fx,fz in 0..1), on the floor.
    static Vector3 Corner(Room r, float fx, float fz) =>
        new Vector3(Mathf.Lerp(r.X0, r.X1, fx), r.FloorY, Mathf.Lerp(r.Z0, r.Z1, fz));

    // Yaw helper so per-room rotations read clearly (just returns the angle).
    static float Yaw(Room r, float yaw) => yaw;

    static void ScatterRubble(Room r, Transform p, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float fx = Frac(r, i * 3 + 1), fz = Frac(r, i * 7 + 2);
            Prop(Rubble[Mathf.Abs(i * 5 + 1) % Rubble.Length], Corner(r, 0.2f + fx * 0.6f, 0.2f + fz * 0.6f), fx * 360f, p);
        }
    }

    static float Frac(Room r, int salt) =>
        Mathf.Abs(Mathf.Sin((r.X0 + r.Z0 + salt) * 12.9898f) * 43758.5453f) % 1f;

    // Wall-mounted prop (banner/chain): fraction along the room, which wall, yaw, height.
    static void Wall(string prefab, Room r, float along, float side, float yaw, float height, Transform p)
    {
        float x = side < 0.5f ? Mathf.Lerp(r.X0, r.X1, along) : Mathf.Lerp(r.X0, r.X1, along);
        float z = side < 0.5f ? r.Z0 + 0.15f : r.Z1 - 0.15f;   // side 0 = south wall, 1 = north wall
        Prop(prefab, new Vector3(x, r.FloorY + height, z), yaw, p, SnapY.Center);
    }

    static void Banner(Room r, float along, float side, float yaw, Transform p) =>
        Wall(Banners[Mathf.Abs(Mathf.RoundToInt(r.X0 + r.Z0)) % Banners.Length], r, along, side, yaw, 2.8f, p);

    static void Prop(string prefab, Vector3 pos, float yaw, Transform parent, SnapY snap = SnapY.Base)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (pf == null) { Debug.LogWarning($"[GreyspireDressing] Missing: {prefab}"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        SnapByBounds(go, pos, snap);
    }

    static void PlaceMesh(string prefab, Transform parent, SnapY snap)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (pf == null) { Debug.LogWarning($"[GreyspireDressing] Missing: {prefab}"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        SnapByBounds(go, parent.position, snap, keepXZ: true, parent: parent);
    }

    static void SnapByBounds(GameObject go, Vector3 target, SnapY ys, bool keepXZ = false, Transform parent = null)
    {
        Bounds b = WorldBounds(go);
        float dy = ys == SnapY.Base ? target.y - b.min.y : ys == SnapY.Top ? target.y - b.max.y : target.y - b.center.y;
        if (keepXZ)
        {
            // Only correct vertical offset; keep the mesh centred on its parent.
            Vector3 d = new Vector3(parent.position.x - b.center.x, dy, parent.position.z - b.center.z);
            go.transform.position += d;
        }
        else
        {
            go.transform.position += new Vector3(target.x - b.center.x, dy, target.z - b.center.z);
        }
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static bool Contains(int[] arr, int i) { foreach (int v in arr) if (v == i) return true; return false; }

    static Transform Sub(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : _root);
        return go.transform;
    }
}
