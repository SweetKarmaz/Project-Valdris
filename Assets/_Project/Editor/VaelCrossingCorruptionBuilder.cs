using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Corruption
//
// PHASE 5b of the VaelCrossing world build: the southern corruption mood.
//
// Creates a LOCAL HDRP Volume (box) over the southern band. As the player enters
// it the air darkens, desaturates, tints sickly green-grey, fog thickens, and a
// vignette closes in — faking the oppressive overcast around the crypt without
// touching the global sky. Drizzle + puddles are added on top (later steps).
// Re-runnable.
public class VaelCrossingCorruptionBuilder : EditorWindow
{
    const string TerrainObject = "VaelCrossing Terrain";
    const string VolumeObject  = "VaelCrossing Corruption";
    const string CloudObject   = "VaelCrossing Corruption Clouds";
    const string PuddleObject  = "VaelCrossing Corruption Puddles";
    const string AdventureEnv  = "Assets/Synty/PolygonAdventure/Prefabs/Environments";
    const string ProfileDir    = "Assets/_Project/Data/Volumes";
    const string ProfilePath   = ProfileDir + "/VaelCrossing_Corruption.asset";

    float southEndV     = 0.25f;  // band depth from the south edge (v)
    float blendDistance = 35f;    // smooth fade at the band's north edge
    float saturation    = -45f;   // -100..100
    float postExposure  = -0.9f;  // darken (EV)
    float contrast      = 10f;
    Color tint          = new(0.72f, 0.82f, 0.62f);  // sickly green-grey
    float fogMeanPath   = 110f;   // lower = thicker local fog
    float vignette      = 0.38f;

    float topElevation  = 0.62f;  // ceiling (snow line) as ×height — clear sky above
    bool  doClouds      = true;
    float cloudSpacing  = 30f;
    float cloudScale    = 3f;
    float cloudYJitter  = 15f;
    float cloudAlpha    = 0.85f;   // <1 = semi-transparent
    int   cloudSeed     = 99;

    bool  doRain        = true;
    float rainIntensity = 1f;
    bool  doPuddles     = true;
    float puddleSpacing = 16f;
    Vector2 puddleSize  = new(1.5f, 4f);
    float puddleSlopeMax = 14f;
    int   puddleSeed    = 7;

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Corruption")]
    static void Open() => GetWindow<VaelCrossingCorruptionBuilder>("VaelCrossing Corruption");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Phase 5b — southern corruption volume (local). Re-runnable.", MessageType.Info);
        southEndV     = EditorGUILayout.Slider("Band Depth (v)", southEndV, 0.05f, 0.45f);
        blendDistance = EditorGUILayout.Slider("Edge Blend (m)", blendDistance, 0f, 100f);
        saturation    = EditorGUILayout.Slider("Saturation", saturation, -100f, 0f);
        postExposure  = EditorGUILayout.Slider("Darkness (EV)", postExposure, -3f, 0f);
        contrast      = EditorGUILayout.Slider("Contrast", contrast, -30f, 40f);
        tint          = EditorGUILayout.ColorField("Tint", tint);
        fogMeanPath   = EditorGUILayout.Slider("Fog Thickness", fogMeanPath, 20f, 400f);
        vignette      = EditorGUILayout.Slider("Vignette", vignette, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ceiling & Clouds", EditorStyles.boldLabel);
        topElevation  = EditorGUILayout.Slider("Top / Snow Line", topElevation, 0.3f, 1f);
        doClouds      = EditorGUILayout.Toggle("Cloud Layer", doClouds);
        using (new EditorGUI.DisabledScope(!doClouds))
        {
            cloudSpacing = EditorGUILayout.Slider("Cloud Spacing", cloudSpacing, 10f, 80f);
            cloudScale   = EditorGUILayout.Slider("Cloud Scale", cloudScale, 0.5f, 8f);
            cloudYJitter = EditorGUILayout.Slider("Cloud Height Spread", cloudYJitter, 0f, 60f);
            cloudAlpha   = EditorGUILayout.Slider("Cloud Opacity", cloudAlpha, 0.1f, 1f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rain & Puddles", EditorStyles.boldLabel);
        doRain = EditorGUILayout.Toggle("Rain (screen overlay)", doRain);
        using (new EditorGUI.DisabledScope(!doRain))
            rainIntensity = EditorGUILayout.Slider("Rain Intensity", rainIntensity, 0.1f, 1f);
        doPuddles = EditorGUILayout.Toggle("Puddles", doPuddles);
        using (new EditorGUI.DisabledScope(!doPuddles))
        {
            puddleSpacing  = EditorGUILayout.Slider("Puddle Spacing", puddleSpacing, 5f, 50f);
            puddleSize     = EditorGUILayout.Vector2Field("Puddle Size (min/max)", puddleSize);
            puddleSlopeMax = EditorGUILayout.Slider("Puddle Max Slope", puddleSlopeMax, 2f, 30f);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Build / Rebuild Corruption", GUILayout.Height(34))) Build();
    }

    void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.gameObject.name == TerrainObject) ?? Terrain.activeTerrain;
        if (terrain == null) { EditorUtility.DisplayDialog("Corruption", "No VaelCrossing Terrain found.", "OK"); return; }
        Vector3 size = terrain.terrainData.size, origin = terrain.transform.position;

        // ── Profile ──────────────────────────────────────────────────────────
        if (!System.IO.Directory.Exists(ProfileDir)) System.IO.Directory.CreateDirectory(ProfileDir);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        var ca = profile.components.OfType<ColorAdjustments>().FirstOrDefault() ?? profile.Add<ColorAdjustments>(true);
        ca.saturation.overrideState  = true; ca.saturation.value  = saturation;
        ca.postExposure.overrideState = true; ca.postExposure.value = postExposure;
        ca.contrast.overrideState    = true; ca.contrast.value    = contrast;
        ca.colorFilter.overrideState = true; ca.colorFilter.value = tint;

        var vig = profile.components.OfType<Vignette>().FirstOrDefault() ?? profile.Add<Vignette>(true);
        vig.intensity.overrideState  = true; vig.intensity.value  = vignette;
        vig.smoothness.overrideState = true; vig.smoothness.value = 0.5f;
        vig.color.overrideState      = true; vig.color.value      = new Color(0.05f, 0.07f, 0.05f);

        float snowY = origin.y + topElevation * size.y;   // ceiling at the snow line

        var fog = profile.components.OfType<Fog>().FirstOrDefault() ?? profile.Add<Fog>(true);
        fog.enabled.overrideState = true; fog.enabled.value = true;
        fog.meanFreePath.overrideState = true; fog.meanFreePath.value = fogMeanPath;
        // Keep the murk below the snow line so there's clear sky above it.
        fog.baseHeight.overrideState = true; fog.baseHeight.value = origin.y;
        fog.maximumHeight.overrideState = true; fog.maximumHeight.value = snowY;

        EditorUtility.SetDirty(profile);

        // ── Local volume box (recreate fresh to avoid stale/broken state) ─────
        var existing = GameObject.Find(VolumeObject);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        var go = new GameObject(VolumeObject);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create Corruption Volume");

        float bandZ = southEndV * size.z;
        float botY  = origin.y - 20f;      // a little below ground
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        go.transform.position = new Vector3(origin.x + size.x * 0.5f,
                                            (botY + snowY) * 0.5f,
                                            origin.z + bandZ * 0.5f);
        box.center = Vector3.zero;
        box.size   = new Vector3(size.x, snowY - botY, bandZ);   // top capped at snow line

        var vol = go.AddComponent<Volume>();
        vol.isGlobal      = false;
        vol.blendDistance = blendDistance;
        vol.priority      = 1f;            // beats the global sky volume when inside
        vol.sharedProfile = profile;

        // Rain: trigger the screen-overlay rain while the player is in this volume.
        if (doRain) go.AddComponent<RainZone>().intensity = rainIntensity;

        BuildCloudsInternal(scene, origin, size, snowY, bandZ);
        BuildPuddles(scene, terrain, origin, size, bandZ);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[VaelCrossingCorruption] Local volume + cloud ceiling at snow line " +
                  $"(top {topElevation:P0}) over south band (v<{southEndV}).");
    }

    // Lays a ceiling of Synty cloud meshes at the snow line across the corruption
    // footprint (full width west→east, south edge → as far north as the band).
    void BuildCloudsInternal(Scene scene, Vector3 origin, Vector3 size, float snowY, float bandZ)
    {
        var existing = GameObject.Find(CloudObject);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        if (!doClouds) return;

        var prefabs = new[] { "SM_Env_Cloud_01", "SM_Env_Cloud_02", "SM_Env_Cloud_03",
                              "SM_Env_Cloud_04", "SM_Env_Cloud_05" }
            .Select(FindPrefab).Where(p => p != null).ToList();
        if (prefabs.Count == 0) { Debug.LogWarning("[VaelCrossingCorruption] No cloud prefabs found."); return; }

        var root = new GameObject(CloudObject);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create Corruption Clouds");

        // One shared semi-transparent cloud material so the opacity slider works.
        Material cloudMat = MakeCloudMaterial(prefabs[0]);

        // Cover the band PLUS the blend zone north of it (where the effect fades in).
        float coverZ = bandZ + blendDistance;

        Random.InitState(cloudSeed);
        float sp = Mathf.Max(8f, cloudSpacing);
        for (float z = 0f; z < coverZ; z += sp)
        for (float x = 0f; x < size.x; x += sp)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Count)];
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            inst.transform.position = new Vector3(
                origin.x + x + Random.Range(-sp, sp) * 0.4f,
                snowY + Random.Range(-cloudYJitter, cloudYJitter),
                origin.z + z + Random.Range(-sp, sp) * 0.4f);
            inst.transform.rotation   = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            inst.transform.localScale = Vector3.one * cloudScale * Random.Range(0.8f, 1.4f);

            if (cloudMat != null)
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(cloudMat, r.sharedMaterials.Length).ToArray();
        }
    }

    // Clones the cloud prefab's material as a transparent variant so we can fade it.
    Material MakeCloudMaterial(GameObject prefab)
    {
        var src = prefab.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial;
        if (src == null) return null;
        if (cloudAlpha >= 0.999f) return src;   // fully opaque → keep original

        var mat = new Material(src) { name = "VC_CorruptionCloud" };
        mat.SetFloat("_SurfaceType", 1f);   // Transparent
        mat.SetFloat("_BlendMode", 0f);     // Alpha
        if (mat.HasProperty("_BaseColor"))
        {
            var c = mat.GetColor("_BaseColor");
            c.a = cloudAlpha;
            mat.SetColor("_BaseColor", c);
        }
        HDMaterial.ValidateMaterial(mat);
        return mat;
    }

    // Scatters dark, glossy "wet" blobs on the south ground.
    void BuildPuddles(Scene scene, Terrain terrain, Vector3 origin, Vector3 size, float bandZ)
    {
        var old = GameObject.Find(PuddleObject);
        if (old != null) Undo.DestroyObjectImmediate(old);
        if (!doPuddles || terrain == null) return;

        var root = new GameObject(PuddleObject);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create Puddles");

        var data = terrain.terrainData;
        var mat = MakeWet();
        float waterY = (GameObject.Find("VaelCrossing Water")?.transform.position.y) ?? -9999f;

        // A few organic blob meshes (flat, normal up) for varied puddle shapes.
        var blobs = new Mesh[5];
        for (int i = 0; i < blobs.Length; i++) blobs[i] = MakeBlobMesh(puddleSeed * 31 + i);

        Random.InitState(puddleSeed);
        float sp = Mathf.Max(4f, puddleSpacing);
        for (float z = 0f; z < bandZ; z += sp)
        for (float x = 0f; x < size.x; x += sp)
        {
            if (Random.value > 0.45f) continue;             // sparse
            float px = origin.x + x + Random.Range(-sp, sp) * 0.4f;
            float pz = origin.z + z + Random.Range(-sp, sp) * 0.4f;
            float u = (px - origin.x) / size.x, v = (pz - origin.z) / size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

            float gy = terrain.SampleHeight(new Vector3(px, 0f, pz)) + origin.y;
            if (gy <= waterY + 0.5f) continue;
            if (data.GetSteepness(u, v) > puddleSlopeMax) continue;

            var q = new GameObject("Puddle");
            q.transform.SetParent(root.transform);
            q.AddComponent<MeshFilter>().sharedMesh = blobs[Random.Range(0, blobs.Length)];
            var mr = q.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;

            Vector3 n = data.GetInterpolatedNormal(u, v);
            q.transform.position = new Vector3(px, gy + 0.04f, pz);
            q.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) *
                                   Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float sx = Random.Range(puddleSize.x, puddleSize.y);
            float sz = sx * Random.Range(0.6f, 1.4f);       // non-uniform → more shape variety
            q.transform.localScale = new Vector3(sx, 1f, sz);
        }
    }

    // Flat, irregular polygon (in the XZ plane, normal up) for an organic puddle.
    static Mesh MakeBlobMesh(int seed)
    {
        var rnd = new System.Random(seed);
        int n = rnd.Next(7, 11);
        var verts = new Vector3[n + 1];
        var tris  = new int[n * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float ang = i / (float)n * Mathf.PI * 2f;
            float rad = 0.5f * (0.6f + (float)rnd.NextDouble() * 0.7f);
            verts[i + 1] = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
        }
        for (int i = 0; i < n; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = (i + 1) % n + 1;
            tris[i * 3 + 2] = i + 1;
        }
        var m = new Mesh { name = "VC_PuddleBlob" };
        m.vertices = verts; m.triangles = tris;
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Material MakeWet()
    {
        var sh = Shader.Find("HDRP/Lit");
        if (sh == null) return null;
        var m = new Material(sh) { name = "VC_Puddle" };
        m.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.03f, 1f));
        m.SetFloat("_Smoothness", 0.92f);   // wet sheen
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_DoubleSidedEnable", 1f);   // visible from above regardless of quad facing
        m.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
        HDMaterial.ValidateMaterial(m);
        return m;
    }

    static GameObject FindPrefab(string nameNoExt)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{nameNoExt} t:Prefab", new[] { AdventureEnv }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == nameNoExt)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
