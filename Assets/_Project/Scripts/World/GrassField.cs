using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Runtime grass field for HDRP (which can't render terrain-detail grass).
//
// Scatters grass clumps in a radius around the camera and draws them with GPU
// instancing (Graphics.RenderMeshInstanced) — no GameObjects, nothing saved in
// the scene. Density is zone-weighted (heavy meadow, sparse town/south, etc.),
// avoids water and steep slopes, and each clump sways for a wind effect.
//
// Set up / configured by the VaelCrossing grass builder; tune the fields here.
[ExecuteAlways]
public class GrassField : MonoBehaviour
{
    [Header("Source")]
    public Terrain terrain;
    public Mesh[] meshes;            // grass clump meshes (variety)
    public Material[] materials;     // parallel to meshes; must allow GPU instancing
    public string waterObjectName = "VaelCrossing Water";

    [Header("Field")]
    public float radius      = 70f;   // draw grass within this distance of the camera
    public float cellSize    = 2f;    // base spacing
    public int   perCellMax  = 3;     // max clumps per cell (meadow)
    public int   maxInstances= 9000;
    public float slopeMax    = 30f;
    public float waterMargin = 0.5f;
    public Vector2 scaleRange = new(0.8f, 1.6f);

    [Header("Zones (u=W→E, v=S→N)")]
    public float mountainBandU = 0.36f, riverWestU = 0.82f;
    public float northStartV = 0.60f, southEndV = 0.25f, forestSplitU = 0.60f;
    public float townUMin = 0.38f, townUMax = 0.82f, townVMin = 0.37f, townVMax = 0.63f;

    [Header("Density (0..1)")]
    public float dMeadow = 0.95f, dValley = 0.4f, dForestFloor = 0.55f, dSouth = 0.35f, townFactor = 0.25f;

    [Header("Wind")]
    public float windStrength = 9f;    // sway degrees
    public float windSpeed    = 1.6f;
    public float windScale    = 0.06f; // spatial variation
    public Vector2 windDir    = new(1f, 0.35f);

    struct Inst { public Vector3 pos; public float yaw, scale, phase; public int mesh; }
    readonly List<Inst> _inst = new();
    Vector3 _lastCenter = new(1e9f, 0, 0);
    readonly Matrix4x4[] _batch = new Matrix4x4[1023];
    float _waterY = -9999f;

    void OnEnable()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        var w = GameObject.Find(waterObjectName);
        if (w != null) _waterY = w.transform.position.y;
        _lastCenter = new Vector3(1e9f, 0, 0);
    }

    void Update()
    {
        if (terrain == null || meshes == null || meshes.Length == 0) return;

        Vector3 center = CameraPos();
        if ((center - _lastCenter).sqrMagnitude > cellSize * cellSize)
        {
            _lastCenter = center;
            Rebuild(center);
        }
        Render();

#if UNITY_EDITOR
        if (!Application.isPlaying) SceneView.RepaintAll();   // animate sway in edit mode
#endif
    }

    Vector3 CameraPos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null) return sv.camera.transform.position;
        }
#endif
        return Camera.main != null ? Camera.main.transform.position : transform.position;
    }

    void Rebuild(Vector3 center)
    {
        _inst.Clear();
        var data = terrain.terrainData;
        Vector3 size = data.size, origin = terrain.transform.position;
        float r = radius, r2 = r * r;
        int cells = Mathf.CeilToInt(2f * r / cellSize);
        float x0 = center.x - r, z0 = center.z - r;

        for (int cz = 0; cz < cells; cz++)
        for (int cx = 0; cx < cells; cx++)
        {
            float bx = x0 + cx * cellSize, bz = z0 + cz * cellSize;
            for (int k = 0; k < perCellMax; k++)
            {
                float px = bx + Hash(bx, bz, k) * cellSize;
                float pz = bz + Hash(bx, bz, k + 17) * cellSize;
                float ddx = px - center.x, ddz = pz - center.z;
                if (ddx * ddx + ddz * ddz > r2) continue;

                float u = (px - origin.x) / size.x, v = (pz - origin.z) / size.z;
                if (u < mountainBandU || u > riverWestU || v < 0f || v > 1f) continue;

                float dens = ZoneDensity(u, v);
                if (k >= Mathf.RoundToInt(dens * perCellMax)) continue;   // thin out by density

                float gy = terrain.SampleHeight(new Vector3(px, 0f, pz)) + origin.y;
                if (gy <= _waterY + waterMargin) continue;
                if (data.GetSteepness(u, v) > slopeMax) continue;

                _inst.Add(new Inst
                {
                    pos   = new Vector3(px, gy, pz),
                    yaw   = Hash(bx, bz, k + 91) * 360f,
                    scale = Mathf.Lerp(scaleRange.x, scaleRange.y, Hash(bx, bz, k + 33)),
                    phase = Hash(bx, bz, k + 7) * 6.2832f,
                    mesh  = Mathf.Min(meshes.Length - 1, Mathf.FloorToInt(Hash(bx, bz, k + 5) * meshes.Length)),
                });
                if (_inst.Count >= maxInstances) return;
            }
        }
    }

    float ZoneDensity(float u, float v)
    {
        float d;
        if (v < southEndV)        d = dSouth;
        else if (v > northStartV)  d = (u > forestSplitU) ? dMeadow : dForestFloor;
        else                       d = dValley;
        if (u >= townUMin && u <= townUMax && v >= townVMin && v <= townVMax) d *= townFactor;
        return d;
    }

    void Render()
    {
        float t = Application.isPlaying ? Time.time :
#if UNITY_EDITOR
            (float)EditorApplication.timeSinceStartup;
#else
            Time.time;
#endif
        Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(windDir.x, 0f, windDir.y).normalized);
        if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;

        for (int mi = 0; mi < meshes.Length; mi++)
        {
            if (meshes[mi] == null) continue;
            Material mat = materials != null && materials.Length > 0
                ? materials[Mathf.Min(mi, materials.Length - 1)] : null;
            if (mat == null) continue;

            var rp = new RenderParams(mat);
            int n = 0;
            for (int i = 0; i < _inst.Count; i++)
            {
                if (_inst[i].mesh != mi) continue;
                var it = _inst[i];
                float sway = Mathf.Sin(t * windSpeed + it.phase + (it.pos.x + it.pos.z) * windScale) * windStrength;
                var rot = Quaternion.AngleAxis(sway, axis) * Quaternion.Euler(0f, it.yaw, 0f);
                _batch[n++] = Matrix4x4.TRS(it.pos, rot, Vector3.one * it.scale);
                if (n == _batch.Length) { Graphics.RenderMeshInstanced(rp, meshes[mi], 0, _batch, n); n = 0; }
            }
            if (n > 0) Graphics.RenderMeshInstanced(rp, meshes[mi], 0, _batch, n);
        }
    }

    static float Hash(float x, float z, int k)
    {
        float v = Mathf.Sin(x * 12.9898f + z * 78.233f + k * 37.719f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }
}
