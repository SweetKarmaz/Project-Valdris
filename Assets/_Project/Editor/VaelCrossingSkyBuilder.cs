using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// Tools > Valdris > Scene > Build VaelCrossing Sky
//
// PHASE 5a of the VaelCrossing world build: global sky + sun + base atmosphere.
//
// Creates a directional sun and a global HDRP Volume (Physically Based Sky,
// gentle Fog, Automatic Exposure). Re-runnable. The southern corruption mood is
// a separate local volume (Phase 5b).
public static class VaelCrossingSkyBuilder
{
    const string SunObject   = "VaelCrossing Sun";
    const string SkyObject   = "VaelCrossing Sky";
    const string ProfileDir  = "Assets/_Project/Data/Volumes";
    const string ProfilePath = ProfileDir + "/VaelCrossing_Sky.asset";

    [MenuItem("Tools/Valdris/Scene/Build VaelCrossing Sky")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();

        // ── Sun (recreate fresh to avoid any partial/broken prior state) ────────
        var existingSun = GameObject.Find(SunObject);
        if (existingSun != null) Undo.DestroyObjectImmediate(existingSun);

        var sun = new GameObject(SunObject);
        SceneManager.MoveGameObjectToScene(sun, scene);
        Undo.RegisterCreatedObjectUndo(sun, "Create Sun");

        var light = sun.AddComponent<Light>();
        light.type    = LightType.Directional;
        light.shadows = LightShadows.Soft;

        var hd = sun.GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = sun.AddComponent<HDAdditionalLightData>();
        hd.EnableShadows(true);

        // DayNightSun drives rotation/intensity/colour from the GameClock.
        sun.AddComponent<DayNightSun>();

        // ── Sky volume profile ────────────────────────────────────────────────
        if (!System.IO.Directory.Exists(ProfileDir)) System.IO.Directory.CreateDirectory(ProfileDir);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        var ve = profile.components.OfType<VisualEnvironment>().FirstOrDefault() ?? profile.Add<VisualEnvironment>(true);
        ve.skyType.overrideState = true;
        ve.skyType.value = (int)SkyType.PhysicallyBased;

        if (profile.components.OfType<PhysicallyBasedSky>().FirstOrDefault() == null)
            profile.Add<PhysicallyBasedSky>(true);

        var fog = profile.components.OfType<Fog>().FirstOrDefault() ?? profile.Add<Fog>(true);
        fog.enabled.overrideState = true; fog.enabled.value = true;
        fog.meanFreePath.overrideState = true; fog.meanFreePath.value = 450f;   // subtle distance haze
        fog.baseHeight.overrideState = true; fog.baseHeight.value = 0f;
        fog.maximumHeight.overrideState = true; fog.maximumHeight.value = 300f;

        var exp = profile.components.OfType<Exposure>().FirstOrDefault() ?? profile.Add<Exposure>(true);
        exp.mode.overrideState = true; exp.mode.value = ExposureMode.Automatic;

        EditorUtility.SetDirty(profile);

        // ── Global volume ─────────────────────────────────────────────────────
        var skyGo = GameObject.Find(SkyObject);
        if (skyGo == null)
        {
            skyGo = new GameObject(SkyObject);
            SceneManager.MoveGameObjectToScene(skyGo, scene);
            Undo.RegisterCreatedObjectUndo(skyGo, "Create Sky Volume");
        }
        var vol = skyGo.GetComponent<Volume>() ?? skyGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 0f;
        vol.sharedProfile = profile;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[VaelCrossingSky] Sun + Physically Based Sky + fog + auto exposure created.");
    }
}
