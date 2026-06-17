using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

// ─────────────────────────────────────────────────────────────────────────────
// Greyspire Scene Builder — multi-level editor-time layout
//
// Menu: Tools > Valdris > Build Greyspire Scene
//
// Layout (Y = floor elevation):
//   y = -8   Mine Chamber  (cave — PolygonDungeon cave pieces + PolygonNature rocks)
//   y = -4   Storage Room  (basement dungeon)
//   y =  0   Ground floor  (rough dungeon: cells, common hall, kitchen, guard post 1)
//   y =  4   Upper Wing    (fine castle: hallway, warden's quarters, guard post 2)
//             Tower Base   (knights tower — connects to upper hallway)
//   y =  8   Tower Entry   (external entrance at cliff face, spiral stair down to base)
// ─────────────────────────────────────────────────────────────────────────────
public static class GreyspireSceneBuilder
{
    // ── Asset roots ───────────────────────────────────────────────────────────
    const string FK  = "Assets/Synty/PolygonFantasyKingdom/Prefabs";
    const string DG  = "Assets/Synty/PolygonDungeon/Prefabs";
    const string KN  = "Assets/Synty/PolygonKnights/Prefabs";
    const string NAT = "Assets/Synty/PolygonNature/Prefabs";

    // ── Floors ────────────────────────────────────────────────────────────────
    const string CastleFloor  = FK + "/Castle/SM_Bld_Castle_Floor_Stone_01.prefab";
    const string DungeonFloor = DG + "/Environments/Floors/SM_Env_Tiles_01.prefab";
    const string CaveFloor    = DG + "/Environments/Rocks/SM_Env_Rock_Flat_Platform_Large_01.prefab";

    // ── Dungeon walls (ground floor / basement) ───────────────────────────────
    static readonly string[] DungeonWalls = {
        DG + "/Environments/Walls/SM_Env_Wall_01.prefab",
        DG + "/Environments/Walls/SM_Env_Wall_02.prefab",
        DG + "/Environments/Walls/SM_Env_Wall_03.prefab",
        DG + "/Environments/Walls/SM_Env_Wall_04.prefab",
        DG + "/Environments/Walls/SM_Env_Wall_05.prefab",
        DG + "/Environments/Walls/SM_Env_Wall_06.prefab",
        // Wall_07 is an arch — excluded from solid wall rotation
    };
    const string DungeonDoor  = DG + "/Environments/Walls/SM_Env_Wall_DoorFrame_01.prefab";
    const string DungeonArch  = DG + "/Environments/Walls/SM_Env_Wall_Archway_01.prefab";
    const string CellBars     = DG + "/Environments/Walls/SM_Env_Wall_Window_Bars_01.prefab";
    const string BarsDoor     = DG + "/Environments/Walls/SM_Env_Door_Bars_01.prefab";

    // ── Castle walls (upper wing) ─────────────────────────────────────────────
    const string CastleWall  = FK + "/Castle/SM_Bld_Castle_Wall_04.prefab";
    const string CastleDoor  = FK + "/Castle/SM_Bld_Castle_Wall_Door_01.prefab";
    const string CastleArrow = FK + "/Castle/SM_Bld_Castle_Wall_Arrowslit_01.prefab";

    // ── Tower walls — reuse castle wall (solid stone, no arrow slits) ─────────
    const string TowerWall   = FK + "/Castle/SM_Bld_Castle_Wall_04.prefab";
    const string TowerDoor   = KN + "/Buildings/SM_Bld_Castle_Door_01.prefab";

    // ── Ceilings ──────────────────────────────────────────────────────────────
    const string CeilFlat     = DG + "/Environments/Walls/SM_Env_Ceiling_Stone_Flat_01.prefab";
    const string CeilCurved   = DG + "/Environments/Walls/SM_Env_Ceiling_Stone_Curved_01.prefab";
    const string CeilBasement = DG + "/Environments/Wood/SM_Env_Basement_Ceiling_01.prefab";
    const string CaveRoof     = DG + "/Environments/Rocks/SM_Env_Cave_Roof_01.prefab";

    // ── Pillars ───────────────────────────────────────────────────────────────
    const string PillarDungeon = DG + "/Environments/Pillars/SM_Env_Pillar_Square_01.prefab";
    const string PillarCastle  = FK + "/Castle/SM_Bld_Castle_Pillar_Stone_01.prefab";

    // ── Stairs ────────────────────────────────────────────────────────────────
    const string StairsLarge  = DG + "/Environments/Floors/SM_Env_Stairs_Large_01.prefab";
    const string StairsSpiral = DG + "/Environments/Floors/SM_Env_SpiralStairs_01.prefab";

    // ── Cave pieces ───────────────────────────────────────────────────────────
    const string Cave01     = DG + "/Environments/Rocks/SM_Env_Cave_01.prefab";
    const string Cave02     = DG + "/Environments/Rocks/SM_Env_Cave_02.prefab";
    const string CaveLarge  = DG + "/Environments/Rocks/SM_Env_Cave_Large_01.prefab";
    const string CaveCurved = DG + "/Environments/Rocks/SM_Env_Cave_Curved_01.prefab";
    const string CaveCorner = DG + "/Environments/Rocks/SM_Env_Cave_Curved_Corner_01.prefab";
    const string CaveRockFl = DG + "/Environments/Rocks/SM_Env_Rock_Flat_Platform_Large_01.prefab";

    // ── Nature rocks (cave scatter) ───────────────────────────────────────────
    const string RockCluster = NAT + "/Rocks/SM_Rock_Cluster_Large_01.prefab";
    const string RockCave    = NAT + "/Rocks/SM_Rock_CaveInterior_01.prefab";

    // ── Props ─────────────────────────────────────────────────────────────────
    const string Torch      = FK + "/Props/SM_Prop_Torch_01.prefab";
    const string PropBed    = DG + "/Props/SM_Prop_Bed_01.prefab";
    const string PropBarrel = DG + "/Props/SM_Prop_Barrel_01.prefab";
    const string PropCrate  = DG + "/Props/SM_Prop_Crate_Wood_01.prefab";
    const string PropTable  = FK + "/Props/Furniture/SM_Prop_Table_Wood_03.prefab";
    const string PropBench  = FK + "/Props/Furniture/SM_Prop_Bench_Seat_02.prefab";
    const string PropDesk   = FK + "/Props/Furniture/SM_Prop_Side_Table_02.prefab";
    const string PropShelf  = FK + "/Props/SM_Prop_Shelf_01.prefab";
    const string PropChest  = FK + "/Props/Furniture/SM_Prop_Chest_03.prefab";
    const string PropStove  = FK + "/Props/Furniture/SM_Prop_Stove_02.prefab";
    const string Battlement = FK + "/Castle/SM_Bld_Castle_Battlements_01.prefab";

    // ─────────────────────────────────────────────────────────────────────────
    // ENUMS
    // ─────────────────────────────────────────────────────────────────────────

    enum RoomType  { Cell, Hall, Kitchen, Guard, Barracks, Warden, Storage, Cave, Tower, Corridor, StairRoom }
    enum WallStyle { Dungeon, Castle, Cave, Tower }

    // ─────────────────────────────────────────────────────────────────────────
    // ROOM DEFINITION
    // ─────────────────────────────────────────────────────────────────────────

    class RoomDef
    {
        public string    Name, ZoneId;
        public RoomType  Type;
        public float     X0, Z0, X1, Z1;
        public float     FloorY     = 0f;
        public float     WallH      = 4f;
        public WallStyle Style      = WallStyle.Dungeon;
        public bool      Pillars    = false;
        public bool      CurvedCeil = false;
        // Tile indices (0 = first tile from lower coord) for passages/bars per side
        public int[] DoorsN = {}, DoorsS = {}, DoorsE = {}, DoorsW = {};
        public int[] BarsN  = {}, BarsS  = {}, BarsE  = {}, BarsW  = {};
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STAIR DEFINITION
    // ─────────────────────────────────────────────────────────────────────────

    struct StairDef
    {
        public float X, Z;         // world-space centre
        public float FromY, ToY;   // elevations to bridge
        public float Rot;          // Y-rotation: direction stairs ascend toward
        public bool  Spiral;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ROOM LIST
    // ─────────────────────────────────────────────────────────────────────────

    // ── Room coordinate notes ─────────────────────────────────────────────────
    // All coordinates are multiples of 5 m to match the Synty module grid.
    // Door tile indices count from the low coordinate (X0 for S/N walls, Z0 for W/E).
    //
    // Ground-floor layout (y = 0):
    //   Z[-25,-5]  Cell blocks A & B
    //   Z[-5, 0]   Narrow 5 m hallways (1-tile wide — W/E walls skipped by hasWE guard)
    //   Z[0,  20]  Common Hall (30 m) + Kitchen (10 m) to its west
    //   Z[20, 45]  Guard Post 1, Barracks
    //   Z[40, 65]  Solitary, Mines Stair
    static readonly List<RoomDef> Rooms = new List<RoomDef>
    {
        // ── Ground floor (y = 0) ─────────────────────────────────────────────

        // Cell Block A — 15 m wide × 20 m deep, 3 × 4 tiles
        // North tile 2 (X[-10,-5]) aligns with Hallway A south opening
        new RoomDef {
            Name="Cell Block A", ZoneId="Cell Block A", Type=RoomType.Cell,
            X0=-20f, Z0=-25f, X1=-5f, Z1=-5f,
            WallH=2.8f,
            DoorsN=new[]{2}
        },
        // Cell Block B — mirror of A
        // North tile 0 (X[5,10]) aligns with Hallway B south opening
        new RoomDef {
            Name="Cell Block B", ZoneId="Cell Block B", Type=RoomType.Cell,
            X0=5f, Z0=-25f, X1=20f, Z1=-5f,
            WallH=2.8f,
            DoorsN=new[]{0}
        },

        // Hallway A — 5 m × 5 m (1 tile); W/E walls skipped automatically (roomW == T)
        new RoomDef {
            Name="Hallway A", ZoneId="Cell Block A", Type=RoomType.Corridor,
            X0=-10f, Z0=-5f, X1=-5f, Z1=0f,
            WallH=2.5f,
            DoorsS=new[]{0}, DoorsN=new[]{0}
        },
        // Hallway B — mirror of A
        new RoomDef {
            Name="Hallway B", ZoneId="Cell Block B", Type=RoomType.Corridor,
            X0=5f, Z0=-5f, X1=10f, Z1=0f,
            WallH=2.5f,
            DoorsS=new[]{0}, DoorsN=new[]{0}
        },

        // Common Hall — 30 m × 20 m, 6 × 4 tiles
        //   South: tile 1 (X[-10,-5]) → Hallway A,  tile 4 (X[5,10]) → Hallway B
        //   West:  tile 1 (Z[5,10])   → Kitchen
        //   North: tile 5 (X[10,15])  → Guard Post 1 south
        new RoomDef {
            Name="Common Hall", ZoneId="Common Hall", Type=RoomType.Hall,
            X0=-15f, Z0=0f, X1=15f, Z1=20f,
            WallH=2.8f,
            DoorsS=new[]{1,4}, DoorsN=new[]{5}, DoorsW=new[]{1}
        },

        // Kitchen — 10 m × 15 m, east wall tile 0 (Z[5,10]) → Common Hall west
        new RoomDef {
            Name="Kitchen", ZoneId="Kitchen", Type=RoomType.Kitchen,
            X0=-25f, Z0=5f, X1=-15f, Z1=20f,
            WallH=2.8f,
            DoorsE=new[]{0}
        },

        // Solitary — 30 m × 25 m
        new RoomDef {
            Name="Solitary", ZoneId="Solitary Confinement", Type=RoomType.Cell,
            X0=-15f, Z0=40f, X1=15f, Z1=65f,
            WallH=2.8f,
            DoorsS=new[]{2,3}
        },
        // Stair room leading down to mines — 15 m × 15 m
        new RoomDef {
            Name="Mines Stair", ZoneId="The Mines", Type=RoomType.StairRoom,
            X0=-25f, Z0=40f, X1=-10f, Z1=55f,
            WallH=2.8f,
            DoorsS=new[]{1}, DoorsN=new[]{0,1,2}
        },
        // Guard Post 1 — 20 m × 25 m, 4 × 5 tiles
        //   South tile 0 (X[10,15]) → Common Hall north
        //   East  tile 2 (Z[30,35]) → Barracks
        //   North tiles 1,2 → stairs up/down
        new RoomDef {
            Name="Guard Post 1", ZoneId="Guard Post 1", Type=RoomType.Guard,
            X0=10f, Z0=20f, X1=30f, Z1=45f,
            WallH=3.0f,
            DoorsS=new[]{0}, DoorsE=new[]{2}, DoorsN=new[]{1,2}
        },
        // Barracks — 25 m × 20 m
        //   West tile 2 (Z[30,35]) → Guard Post 1 east
        new RoomDef {
            Name="Barracks", ZoneId="Barracks", Type=RoomType.Barracks,
            X0=30f, Z0=20f, X1=55f, Z1=40f,
            WallH=2.8f,
            DoorsW=new[]{2}
        },

        // ── Basement (y = -4) ─────────────────────────────────────────────────

        // Storage — 25 m × 25 m
        new RoomDef {
            Name="Storage", ZoneId="Storage", Type=RoomType.Storage,
            X0=5f, Z0=45f, X1=30f, Z1=70f,
            FloorY=-4f, WallH=2.5f,
            DoorsS=new[]{2}
        },

        // ── Mine Chamber (y = -8) — cave style ───────────────────────────────
        // South gaps 3,4,5 align with Mines Stair north opening at X[-25,-10]

        new RoomDef {
            Name="Mine Chamber", ZoneId="The Mines", Type=RoomType.Cave,
            X0=-40f, Z0=55f, X1=-5f, Z1=100f,
            FloorY=-8f, WallH=5f, Style=WallStyle.Cave
        },

        // ── Upper Wing (y = 4) — fine castle stonework ────────────────────────

        // Upper Hallway — 30 m × 20 m, 6 × 4 tiles
        //   South tiles 1,3  → Guard Post 1 north (via stairs)
        //   West  tiles 1,2  → Warden's Quarters east (shared wall X=10)
        //   East  tile  2    → Tower Base west (shared wall X=40)
        //   North tiles 1,3  → Guard Post 2 south (shared wall Z=65)
        new RoomDef {
            Name="Upper Hallway", ZoneId="Upper Wing", Type=RoomType.Corridor,
            X0=10f, Z0=45f, X1=40f, Z1=65f,
            FloorY=4f, WallH=3.2f, Style=WallStyle.Castle,
            DoorsS=new[]{1,3}, DoorsW=new[]{1,2}, DoorsE=new[]{2}, DoorsN=new[]{1,3}
        },
        // Warden's Quarters — 20 m × 35 m
        //   East tiles 1,2 (Z[50,60]) → Upper Hallway west
        new RoomDef {
            Name="Warden's Quarters", ZoneId="Warden's Area", Type=RoomType.Warden,
            X0=-10f, Z0=45f, X1=10f, Z1=80f,
            FloorY=4f, WallH=3.5f, Style=WallStyle.Castle,
            DoorsE=new[]{1,2}
        },
        // Guard Post 2 — 20 m × 15 m, 4 × 3 tiles
        //   South tile 1 (X[15,20]) → Upper Hallway north
        new RoomDef {
            Name="Guard Post 2", ZoneId="Upper Wing", Type=RoomType.Guard,
            X0=10f, Z0=65f, X1=30f, Z1=80f,
            FloorY=4f, WallH=3.0f, Style=WallStyle.Castle,
            DoorsS=new[]{1,3}
        },

        // ── Tower Base (y = 4) — shares west wall with Upper Hallway east ──────

        // Tower Base — 20 m × 20 m
        //   West tile 2 (Z[55,60]) → Upper Hallway east
        new RoomDef {
            Name="Tower Base", ZoneId="Tower", Type=RoomType.Tower,
            X0=40f, Z0=45f, X1=60f, Z1=65f,
            FloorY=4f, WallH=3.5f, Style=WallStyle.Tower,
            DoorsW=new[]{2}
        },

        // ── Tower Entry (y = 8) — same footprint, external cliff entrance ──────

        new RoomDef {
            Name="Tower Entry", ZoneId="Tower", Type=RoomType.Tower,
            X0=40f, Z0=45f, X1=60f, Z1=65f,
            FloorY=8f, WallH=4.0f, Style=WallStyle.Tower,
            DoorsS=new[]{1}
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // STAIR LIST
    // ─────────────────────────────────────────────────────────────────────────

    static readonly List<StairDef> Stairs = new List<StairDef>
    {
        // Guard Post 1 north (Z=45) → Storage basement (y=-4)
        new StairDef { X=15f,  Z=44f, FromY=0f,  ToY=-4f, Rot=0f,   Spiral=false },
        // Guard Post 1 north → Upper Hallway (y=4)
        new StairDef { X=25f,  Z=44f, FromY=0f,  ToY=4f,  Rot=180f, Spiral=false },
        // Mines Stair (Z40-55) → Mine Chamber (y=-8) via two-step descent
        new StairDef { X=-17f, Z=52f, FromY=0f,  ToY=-4f, Rot=0f,   Spiral=false },
        new StairDef { X=-17f, Z=57f, FromY=-4f, ToY=-8f, Rot=0f,   Spiral=false },
        // Tower Base (y=4) → Tower Entry (y=8) — spiral stair inside tower
        new StairDef { X=50f,  Z=55f, FromY=4f,  ToY=8f,  Rot=0f,   Spiral=true  },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // STATE
    // ─────────────────────────────────────────────────────────────────────────

    static Transform _root;
    static readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
    const float T = 5f;  // Synty module grid (metres) — all pieces are 5 m

    // ─────────────────────────────────────────────────────────────────────────
    // ENTRY POINT
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Valdris/Build Greyspire Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Greyspire Scene",
            "Rebuilds the multi-level Greyspire layout using Synty dungeon, castle, " +
            "knights, and cave assets.\n\n" +
            "Existing 'Greyspire_Geometry' will be destroyed and rebuilt.\n\nContinue?",
            "Build", "Cancel")) return;

        _prefabCache.Clear();

        var existing = GameObject.Find("Greyspire_Geometry");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var rootGO = new GameObject("Greyspire_Geometry");
        Undo.RegisterCreatedObjectUndo(rootGO, "Build Greyspire");
        _root = rootGO.transform;

        Transform floorsG  = Sub("Floors");
        Transform ceilsG   = Sub("Ceilings");
        Transform wallsG   = Sub("Walls");
        Transform propsG   = Sub("Props");
        Transform stairsG  = Sub("Stairs");
        Transform zonesG   = Sub("Zones");

        foreach (RoomDef room in Rooms)
        {
            BuildFloor(room, floorsG);
            BuildCeiling(room, ceilsG);

            if (room.Style == WallStyle.Cave)
                BuildCave(room, wallsG, propsG);
            else
                BuildWalls(room, wallsG);

            if (room.Pillars) BuildPillars(room, propsG);
            BuildProps(room, propsG);
            BuildZoneTrigger(room, zonesG);
        }

        foreach (StairDef stair in Stairs)
            BuildStair(stair, stairsG);

        PlaceNavMesh();
        PlaceGameManager();

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[GreyspireSceneBuilder] Build complete.");
        EditorUtility.DisplayDialog("Done",
            $"Built {Rooms.Count} rooms and {Stairs.Count} stairways.\n\n" +
            "Next:\n1. Ctrl+S to save\n2. Select NavMeshSurface and click Bake\n" +
            "3. Fine-tune tile placement in edit mode", "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FLOOR
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildFloor(RoomDef room, Transform parent)
    {
        var fp = Sub(room.Name, parent);

        if (room.Style == WallStyle.Cave)
        {
            for (float x = room.X0; x < room.X1 - 0.01f; x += T)
            for (float z = room.Z0; z < room.Z1 - 0.01f; z += T)
            {
                int rv = (int)(System.Math.Abs(x * 3f + z * 7f)) % 4;
                Place(CaveFloor, new Vector3(x + T * 0.5f, room.FloorY, z + T * 0.5f),
                      Quaternion.Euler(0f, rv * 90f, 0f), fp, $"F_{x}_{z}");
            }
            return;
        }

        string pfab = room.Style == WallStyle.Castle ? CastleFloor : DungeonFloor;
        for (float x = room.X0; x < room.X1 - 0.01f; x += T)
        for (float z = room.Z0; z < room.Z1 - 0.01f; z += T)
            Place(pfab, new Vector3(x + T * 0.5f, room.FloorY, z + T * 0.5f), Quaternion.identity, fp, $"F_{x}_{z}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CEILING
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildCeiling(RoomDef room, Transform parent)
    {
        if (room.Style == WallStyle.Cave) return;  // cave roof placed in BuildCave

        float cy    = room.FloorY + room.WallH;
        // Storage gets a wooden-beam ceiling; everything else uses flat stone.
        // Ceiling pieces face DOWN by default — do NOT rotate 180° or they face up.
        string pfab = room.Type == RoomType.Storage ? CeilBasement : CeilFlat;

        var cp = Sub(room.Name, parent);
        for (float x = room.X0; x < room.X1 - 0.01f; x += T)
        for (float z = room.Z0; z < room.Z1 - 0.01f; z += T)
            Place(pfab, new Vector3(x + T * 0.5f, cy, z + T * 0.5f), Quaternion.identity, cp, $"C_{x}_{z}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WALLS
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildWalls(RoomDef room, Transform parent)
    {
        var wp = Sub(room.Name, parent);

        string solid, door, bars;
        switch (room.Style)
        {
            case WallStyle.Castle:
                solid = CastleWall; door = CastleDoor; bars = CellBars;
                break;
            case WallStyle.Tower:
                solid = TowerWall;  door = TowerDoor;  bars = CellBars;
                break;
            default:
                // Vary dungeon wall variant per room to break uniformity
                int v = System.Math.Abs(room.Name.GetHashCode()) % DungeonWalls.Length;
                solid = DungeonWalls[v]; door = DungeonDoor; bars = CellBars;
                break;
        }

        float y    = room.FloorY;
        float hs   = T * 0.5f;
        float roomW = room.X1 - room.X0;
        float roomD = room.Z1 - room.Z0;

        // S/N walls run along X — skip if room is only 1 tile deep (would overlap W/E)
        // W/E walls run along Z — skip if room is only 1 tile wide (both would collapse to same X)
        bool hasWE = roomW > T + 0.1f;
        bool hasSN = roomD > T + 0.1f;

        // S — pivot at south edge, piece extends north: inset +hs, no run shift
        WallLine(wp, "S", room.X0, room.Z0, room.X1, room.Z0, y, solid, door, bars,
                 room.DoorsS, room.BarsS, false, Quaternion.identity,          +hs,  0f);
        // N — 180° rotation shifts content one tile west: inset +hs (outside), runOffset -1
        WallLine(wp, "N", room.X0, room.Z1, room.X1, room.Z1, y, solid, door, bars,
                 room.DoorsN, room.BarsN, false, Quaternion.Euler(0f,180f,0f), +hs, -1f);
        if (hasWE)
        {
            // W — piece extends east of pivot: inset -hs (outside west boundary)
            WallLine(wp, "W", room.X0, room.Z0, room.X0, room.Z1, y, solid, door, bars,
                     room.DoorsW, room.BarsW, true, Quaternion.Euler(0f, 90f,0f), -hs,  0f);
            // E — -90° rotation shifts content one tile north: inset -hs, runOffset +1
            WallLine(wp, "E", room.X1, room.Z0, room.X1, room.Z1, y, solid, door, bars,
                     room.DoorsE, room.BarsE, true, Quaternion.Euler(0f,-90f,0f), -hs, +1f);
        }
    }

    // alongZ    – wall runs along Z (west/east) vs X (south/north)
    // wallRot   – Y-rotation so textured face points INTO the room
    // inset     – signed offset on the fixed axis (perpendicular to wall run)
    // runOffset – tile-count offset on the running axis; -1 corrects for Synty pieces
    //             whose pivot is at the leading edge, causing a one-tile shift when
    //             the piece is rotated 180° (north) or -90° (east)
    static void WallLine(Transform parent, string side,
        float x0, float z0, float x1, float z1, float baseY,
        string solid, string door, string bars,
        int[] doorTiles, int[] barsTiles, bool alongZ, Quaternion wallRot, float inset,
        float runOffset = 0f)
    {
        float span = alongZ ? (z1 - z0) : (x1 - x0);
        if (System.Math.Abs(span) < 0.01f) return;

        int   count = System.Math.Max(1, Mathf.RoundToInt(Mathf.Abs(span) / T));
        float sign  = span >= 0f ? 1f : -1f;

        var ds = new HashSet<int>(doorTiles ?? new int[0]);
        var bs = new HashSet<int>(barsTiles ?? new int[0]);
        var lp = Sub(side, parent);

        for (int i = 0; i < count; i++)
        {
            float tilePos = (i + 0.5f + runOffset) * T * sign;
            float wx = alongZ ? x0 + inset  : x0 + tilePos;
            float wz = alongZ ? z0 + tilePos : z0 + inset;
            bool isDoor = ds.Contains(i);
            string pfab = isDoor          ? door
                        : bs.Contains(i) ? bars
                        : solid;
            var placed = Place(pfab, new Vector3(wx, baseY, wz), wallRot, lp, $"W{i}");

            if (isDoor && placed != null)
            {
                // NavMeshModifier with ignoreFromBuild=true tells the bake to skip this
                // object entirely — works in both RenderMeshes and PhysicsColliders modes.
                // Applied to every child so no part of the frame hierarchy contributes
                // geometry that would block the NavMesh through the doorway opening.
                foreach (Transform t in placed.GetComponentsInChildren<Transform>(true))
                {
                    var mod = t.gameObject.GetComponent<NavMeshModifier>();
                    if (mod == null) mod = t.gameObject.AddComponent<NavMeshModifier>();
                    mod.ignoreFromBuild = true;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CAVE  (organic walls, cave roof, scattered rocks)
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildCave(RoomDef room, Transform wallParent, Transform propParent)
    {
        float y  = room.FloorY;
        float cy = room.FloorY + room.WallH;
        var   wp = Sub(room.Name, wallParent);

        float hs = T * 0.5f;
        // South/West face correctly inward; North/East need 180° flip
        PlaceCaveWallLine(wp, "S", room.X0, room.Z0, room.X1, room.Z0, y, false, +hs, new int[]{3,4,5}, false);
        PlaceCaveWallLine(wp, "N", room.X0, room.Z1, room.X1, room.Z1, y, false, -hs, new int[0],       true);
        PlaceCaveWallLine(wp, "W", room.X0, room.Z0, room.X0, room.Z1, y, true,  +hs, new int[0],       false);
        PlaceCaveWallLine(wp, "E", room.X1, room.Z0, room.X1, room.Z1, y, true,  -hs, new int[0],       true);

        // Cave roof — randomised rotation to break tiling
        var rp = Sub(room.Name + "_Roof", wallParent);
        for (float x = room.X0; x < room.X1 - 0.01f; x += T)
        for (float z = room.Z0; z < room.Z1 - 0.01f; z += T)
        {
            int rv = (int)(System.Math.Abs(x * 5f + z * 3f)) % 4;
            Place(CaveRoof, new Vector3(x + T * 0.5f, cy, z + T * 0.5f),
                  Quaternion.Euler(180f, rv * 90f, 0f), rp, $"R_{x}_{z}");
        }

        // Small cave rock scatter (no large clusters)
        var   pp         = Sub(room.Name + "_Rocks", propParent);
        float roomW      = room.X1 - room.X0;
        float roomD      = room.Z1 - room.Z0;
        int   rocksTotal = Mathf.Max(4, Mathf.RoundToInt(roomW * roomD / 80f));

        for (int i = 0; i < rocksTotal; i++)
        {
            float rx = room.X0 + 3f + (i * 17f % (roomW - 6f));
            float rz = room.Z0 + 3f + (i * 13f % (roomD - 6f));
            float ry = (i * 73f) % 360f;
            Place(RockCave, new Vector3(rx, y, rz),
                  Quaternion.Euler(0f, ry, 0f), pp, $"Rock_{i}");
        }
    }

    static void PlaceCaveWallLine(Transform parent, string side,
        float x0, float z0, float x1, float z1, float baseY, bool alongZ, float inset, int[] gaps,
        bool flip = false)
    {
        float span = alongZ ? (z1 - z0) : (x1 - x0);
        if (System.Math.Abs(span) < 0.01f) return;

        int   count  = System.Math.Max(1, Mathf.RoundToInt(Mathf.Abs(span) / T));
        float sign   = span >= 0f ? 1f : -1f;
        // flip=true rotates 180° to face inward on N and E cave walls
        var   rot    = alongZ
                         ? Quaternion.Euler(0f, flip ? -90f :  90f, 0f)
                         : Quaternion.Euler(0f, flip ?  180f :   0f, 0f);
        var   gapSet = new HashSet<int>(gaps ?? new int[0]);
        var   lp     = Sub(side, parent);

        for (int i = 0; i < count; i++)
        {
            if (gapSet.Contains(i)) continue;
            float wx = alongZ ? x0 + inset                    : x0 + (i + 0.5f) * T * sign;
            float wz = alongZ ? z0 + (i + 0.5f) * T * sign   : z0 + inset;
            string pfab = (i % 3 == 0) ? CaveLarge : (i % 3 == 1) ? Cave01 : CaveCurved;
            Place(pfab, new Vector3(wx, baseY, wz), rot, lp, $"CW{i}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PILLARS
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildPillars(RoomDef room, Transform parent)
    {
        string pfab = room.Style == WallStyle.Castle ? PillarCastle : PillarDungeon;
        var    pp   = Sub(room.Name + "_Pillars", parent);
        float  y    = room.FloorY;
        for (float x = room.X0 + T * 2f; x < room.X1 - T * 1.5f; x += T * 2f)
        for (float z = room.Z0 + T * 2f; z < room.Z1 - T * 1.5f; z += T * 2f)
            Place(pfab, new Vector3(x, y, z), Quaternion.identity, pp, $"P_{x}_{z}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PROPS  (room-type driven furnishings)
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildProps(RoomDef room, Transform parent)
    {
        var   pp = Sub(room.Name + "_Props", parent);
        float y  = room.FloorY;
        float cx = (room.X0 + room.X1) * 0.5f;
        float cz = (room.Z0 + room.Z1) * 0.5f;

        // Safe interior bounds:
        //   East wall center is INSIDE the room at X1-T/2 — stay west of X1-T
        //   South wall center is INSIDE the room at Z0+T/2 — stay north of Z0+T
        //   West and North walls are OUTSIDE the room — minimal margin needed
        float safeX0 = room.X0 + 2f;
        float safeX1 = room.X1 - T - 1f;
        float safeZ0 = room.Z0 + T + 1f;
        float safeZ1 = room.Z1 - 2f;

        switch (room.Type)
        {
            case RoomType.Cell:
                // Beds along west and east inner walls, spaced every T metres
                for (float z = safeZ0; z < safeZ1; z += T)
                {
                    Place(PropBed, new Vector3(safeX0,       y, z), Quaternion.Euler(0,  90, 0), pp, $"BedW_{z}");
                    Place(PropBed, new Vector3(safeX1 - 0.5f, y, z), Quaternion.Euler(0, -90, 0), pp, $"BedE_{z}");
                }
                break;

            case RoomType.Hall:
                for (float x = safeX0 + T; x < safeX1; x += T * 2f)
                for (float z = safeZ0 + T; z < safeZ1;  z += T * 2f)
                {
                    Place(PropTable, new Vector3(x, y, z),       Quaternion.identity, pp, $"Tbl_{x}_{z}");
                    Place(PropBench, new Vector3(x, y, z - 2f),  Quaternion.identity, pp, $"BnA_{x}_{z}");
                    Place(PropBench, new Vector3(x, y, z + 2f),  Quaternion.identity, pp, $"BnB_{x}_{z}");
                }
                break;

            case RoomType.Kitchen:
                Place(PropStove,  new Vector3(safeX0,       y, cz + 3f), Quaternion.Euler(0, 90, 0), pp, "Stove");
                Place(PropShelf,  new Vector3(safeX0,       y, cz - 3f), Quaternion.Euler(0, 90, 0), pp, "Shelf");
                Place(PropBarrel, new Vector3(cx - 1f,       y, safeZ0),  Quaternion.identity,        pp, "Barrel1");
                Place(PropBarrel, new Vector3(cx + 1f,       y, safeZ0),  Quaternion.identity,        pp, "Barrel2");
                Place(PropTable,  new Vector3(cx,            y, cz),      Quaternion.identity,        pp, "PrepTable");
                break;

            case RoomType.Guard:
                Place(PropDesk,  new Vector3(cx,      y, cz - 2f), Quaternion.identity, pp, "Desk");
                Place(PropChest, new Vector3(safeX0,  y, safeZ0),  Quaternion.identity, pp, "Chest");
                break;

            case RoomType.Barracks:
                for (int i = 0; i < 6; i++)
                {
                    float bx = safeX0 + (i % 3) * T;
                    float bz = safeZ0 + (i / 3) * (T + 2f);
                    Place(PropBed, new Vector3(bx, y, bz), Quaternion.Euler(0, 90, 0), pp, $"Bed{i}");
                }
                break;

            case RoomType.Warden:
                Place(PropDesk,  new Vector3(cx,      y, safeZ1 - T),      Quaternion.identity,        pp, "Desk");
                Place(PropShelf, new Vector3(safeX0,  y, cz),              Quaternion.Euler(0, 90, 0), pp, "Shelf");
                Place(PropChest, new Vector3(cx + 2f, y, safeZ0),          Quaternion.identity,        pp, "Chest");
                Place(PropBed,   new Vector3(safeX0,  y, safeZ0 + T),      Quaternion.Euler(0, 90, 0), pp, "Bed");
                Place(PropTable, new Vector3(cx - 2f, y, safeZ1 - T - 2f), Quaternion.identity,        pp, "Table");
                break;

            case RoomType.Storage:
                for (int i = 0; i < 6; i++)
                {
                    float crx = safeX0 + (i % 3) * (T - 1f);
                    float crz = safeZ1 - (i / 3) * 3f;
                    Place(PropCrate, new Vector3(crx, y, crz), Quaternion.Euler(0, i * 45f, 0), pp, $"Crate{i}");
                }
                for (int i = 0; i < 4; i++)
                    Place(PropBarrel, new Vector3(safeX1, y, safeZ0 + i * 2.5f),
                          Quaternion.identity, pp, $"Barrel{i}");
                break;

            case RoomType.Tower:
                if (room.Name == "Tower Entry")
                {
                    float topY = room.FloorY + room.WallH;
                    for (float x = room.X0; x < room.X1 - 0.01f; x += T)
                    {
                        Place(Battlement, new Vector3(x + T * 0.5f, topY, room.Z0),
                              Quaternion.identity, pp, $"BmS{x}");
                        Place(Battlement, new Vector3(x + T * 0.5f, topY, room.Z1),
                              Quaternion.identity, pp, $"BmN{x}");
                    }
                    for (float z = room.Z0 + T; z < room.Z1 - T + 0.01f; z += T)
                    {
                        Place(Battlement, new Vector3(room.X0, topY, z + T * 0.5f),
                              Quaternion.Euler(0, 90, 0), pp, $"BmW{z}");
                        Place(Battlement, new Vector3(room.X1, topY, z + T * 0.5f),
                              Quaternion.Euler(0, 90, 0), pp, $"BmE{z}");
                    }
                }
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TORCHES
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildTorches(RoomDef room, Transform parent)
    {
        if (room.Style == WallStyle.Cave) return;  // cave uses ambient/point lights from rocks

        Color col;
        float intensity, range;
        switch (room.Type)
        {
            case RoomType.Warden:
                col = new Color(1f, 0.82f, 0.50f); intensity = 1000f; range = 16f; break;
            case RoomType.Tower:
                col = new Color(0.85f, 0.90f, 1.0f); intensity = 600f; range = 18f; break;
            case RoomType.Cell:
                col = new Color(1f, 0.55f, 0.20f); intensity = 450f; range = 11f; break;
            default:
                col = new Color(1f, 0.62f, 0.28f); intensity = 750f;
                range = room.WallH > 3.5f ? 14f : 10f;
                break;
        }

        float torchY  = room.FloorY + room.WallH * 0.65f;
        float w       = room.X1 - room.X0;
        float d       = room.Z1 - room.Z0;
        var   tp      = Sub(room.Name + "_Torches", parent);
        const float spacing = 16f;

        int nx = System.Math.Max(1, Mathf.RoundToInt(w / spacing));
        for (int i = 0; i <= nx; i++)
        {
            float tx = room.X0 + w * i / nx;
            PlaceTorch(tp, new Vector3(tx, torchY, room.Z0 + 0.3f), col, intensity, range, $"TS{i}");
            PlaceTorch(tp, new Vector3(tx, torchY, room.Z1 - 0.3f), col, intensity, range, $"TN{i}");
        }
        int nz = System.Math.Max(1, Mathf.RoundToInt(d / spacing));
        for (int i = 1; i < nz; i++)
        {
            float tz = room.Z0 + d * i / nz;
            PlaceTorch(tp, new Vector3(room.X0 + 0.3f, torchY, tz), col, intensity, range, $"TW{i}");
            PlaceTorch(tp, new Vector3(room.X1 - 0.3f, torchY, tz), col, intensity, range, $"TE{i}");
        }
    }

    static void PlaceTorch(Transform parent, Vector3 pos, Color col, float intensity, float range, string nm)
    {
        var go = new GameObject(nm);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        Place(Torch, pos, Quaternion.identity, go.transform, "Mesh");

        // Light lives on its own child so TorchFlicker's GetComponent<Light>() works
        var lg = new GameObject("Light");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        var lt = lg.AddComponent<Light>();
        lt.type      = LightType.Point;
        lt.color     = col;
        lt.intensity = intensity;
        lt.range     = range;
        lg.AddComponent<TorchFlicker>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STAIRS
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildStair(StairDef s, Transform parent)
    {
        string pfab = s.Spiral ? StairsSpiral : StairsLarge;
        float  midY = (s.FromY + s.ToY) * 0.5f;
        Place(pfab, new Vector3(s.X, midY, s.Z), Quaternion.Euler(0f, s.Rot, 0f),
              parent, $"Stair_{s.X}_{s.Z}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZONE TRIGGERS
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildZoneTrigger(RoomDef room, Transform parent)
    {
        string safe = room.Name.Replace(" ", "").Replace("'", "");
        var    go   = new GameObject("Zone_" + safe);
        go.transform.SetParent(parent, false);
        float cy = room.FloorY + room.WallH * 0.5f;
        go.transform.position = new Vector3((room.X0 + room.X1) * 0.5f, cy, (room.Z0 + room.Z1) * 0.5f);
        var col       = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size      = new Vector3(room.X1 - room.X0, room.WallH, room.Z1 - room.Z0);
        go.AddComponent<Zone>().zoneId = room.ZoneId;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPAWN POINT & NAVMESH
    // ─────────────────────────────────────────────────────────────────────────

    static void PlaceSpawnPoint()
    {
        // Preserve any existing spawn point — the user may have moved it.
        // Only create a new one if it was deleted.
        if (GameObject.Find("PlayerSpawnPoint") != null) return;

        var go = new GameObject("PlayerSpawnPoint");
        go.transform.position = new Vector3(-12f, 1f, -15f);  // default: inside Cell Block A
        Undo.RegisterCreatedObjectUndo(go, "PlayerSpawnPoint");
        Debug.Log("[GreyspireSceneBuilder] Created PlayerSpawnPoint at default position.");
    }

    static void PlaceNavMesh()
    {
        if (GameObject.Find("NavMeshSurface") != null) return;
        var go = new GameObject("NavMeshSurface");
        go.transform.SetParent(_root, false);
        var surface = go.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0;
        Undo.RegisterCreatedObjectUndo(go, "NavMeshSurface");
    }

    static void PlaceGameManager()
    {

        var gmGO = GameObject.Find("GameManager");
        GreyspireBuilder builder;

        if (gmGO != null)
        {
            builder = gmGO.GetComponent<GreyspireBuilder>();
            if (builder == null)
            {
                builder = Undo.AddComponent<GreyspireBuilder>(gmGO);
                Debug.Log("[GreyspireSceneBuilder] Re-added GreyspireBuilder to existing GameManager.");
            }
        }
        else
        {
            gmGO = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(gmGO, "GameManager");
            builder = gmGO.AddComponent<GreyspireBuilder>();
            Debug.Log("[GreyspireSceneBuilder] Created new GameManager.");
        }

        // Default spawn position — inside Cell Block A, just off the south wall.
        // Stored on the component; no separate PlayerSpawnPoint GameObject needed.
        // Only set if still at the default zero value so the user can override it.
        if (builder.defaultSpawnPosition == Vector3.zero)
            builder.defaultSpawnPosition = new Vector3(-12f, 1f, -15f);


        // Ember spell — grant on scene entry (via SceneGameManager.spellsGrantedOnFirstVisit).
        const string emberSpellPath = "Assets/_Project/Data/Spells/Ember.asset";
        var emberSpell = AssetDatabase.LoadAssetAtPath<SpellData>(emberSpellPath);
        if (emberSpell != null)
        {
            if (!builder.spellsGrantedOnFirstVisit.Contains(emberSpell))
                builder.spellsGrantedOnFirstVisit.Add(emberSpell);
        }
        else
        {
            Debug.LogWarning("[GreyspireSceneBuilder] Ember SpellData not found — run Tools > Valdris > Create Ember Spell Data first.");
        }

        // Ensure SceneStateManager is on the GameManager.
        if (gmGO.GetComponent<SceneStateManager>() == null)
        {
            Undo.AddComponent<SceneStateManager>(gmGO);
            Debug.Log("[GreyspireSceneBuilder] Added SceneStateManager to GameManager.");
        }

        // Never touch playerCharacterPrefab — the user sets that manually in the inspector.
        EditorUtility.SetDirty(gmGO);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    static Transform Sub(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent ?? _root, false);
        return go.transform;
    }

    static GameObject Place(string path, Vector3 pos, Quaternion rot, Transform parent, string nm)
    {
        if (!_prefabCache.TryGetValue(path, out var prefab))
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning("[GreyspireSceneBuilder] Missing prefab: " + path);
            _prefabCache[path] = prefab;
        }

        GameObject go;
        if (prefab != null)
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        else
        {
            // Fall back to a placeholder cube so layout is still visible
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
        }

        go.name = nm;
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }
}
