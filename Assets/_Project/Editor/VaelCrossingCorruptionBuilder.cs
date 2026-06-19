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

        BuildCloudsInternal(scene, origin, size, snowY, bandZ);

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
