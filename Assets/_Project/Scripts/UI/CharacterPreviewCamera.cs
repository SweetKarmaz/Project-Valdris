using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Renders the live player model into a RenderTexture for the inventory portrait,
// using the standard "paperdoll" technique:
//
//   • The player model lives on the "UICharacter" layer (set by PlayerManager).
//   • The main camera EXCLUDES that layer, so the player never sees their own
//     body in first person — this replaces FirstPersonCamera's renderer-hiding.
//   • This camera renders ONLY the UICharacter layer, framed on the player, with
//     a solid black background.
//
// Because we render the real, in-scene model (lit by the actual scene lights),
// equipment changes show instantly — PlayerAppearanceComponent toggles mesh
// children on this same model, and the portrait camera simply sees the result.
// No clone, no remote stage, no dedicated lights, no visibility syncing.
//
// Setup is automatic: PlayerManager.Awake() calls EnsureExists(); the
// UICharacter layer is created by Editor/UICharacterLayerSetup.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CharacterPreviewCamera : MonoBehaviour
{
    public static CharacterPreviewCamera Instance { get; private set; }

    public const string LayerName = "UICharacter";

    [Header("Framing")]
    [Tooltip("Fallback distance when the model has no renderers to measure.")]
    public float cameraDistance = 3.0f;
    [Tooltip("Fallback aim height when the model has no renderers to measure.")]
    public float heightOffset = 0.95f;
    [Tooltip("Yaw offset in degrees. 0 = straight on (sees the character's front).")]
    public float yawOffset = 0f;
    [Tooltip("Extra space around the character when fitting it to the frame " +
             "(1 = exact fit, 1.15 = 15% margin).")]
    public float framingMargin = 1.15f;

    [Header("Render Texture")]
    public int rtWidth  = 256;
    public int rtHeight = 512;

    [Header("Exposure")]
    [Tooltip("Fixed exposure (EV100) for the portrait. Higher = darker image. " +
             "Tune until the character is well-lit and not blown out.")]
    public float fixedExposure = 7f;

    [Header("Lighting")]
    [Tooltip("Intensity of the dedicated portrait key light.")]
    public float keyLightIntensity = 4000f;
    [Tooltip("Intensity of the softer fill light.")]
    public float fillLightIntensity = 1200f;

    Camera        _cam;
    RenderTexture _rt;
    Transform     _target;   // the live player model root (or player transform)
    Volume        _volume;
    Exposure      _exposure;
    Light         _keyLight;
    Light         _fillLight;

    public Texture PreviewTexture => _rt;

    // ── Factory ───────────────────────────────────────────────────────────────

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("CharacterPreviewCamera");
        DontDestroyOnLoad(go);
        go.AddComponent<Camera>();
        go.AddComponent<HDAdditionalCameraData>();
        go.AddComponent<CharacterPreviewCamera>();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _rt = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32);
        _rt.Create();

        _cam = GetComponent<Camera>();
        _cam.targetTexture = _rt;
        _cam.clearFlags    = CameraClearFlags.SolidColor;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane  = 50f;
        _cam.fieldOfView   = 35f;

        int uiLayer = LayerMask.NameToLayer(LayerName);
        if (uiLayer >= 0)
            _cam.cullingMask = 1 << uiLayer;   // ONLY the character layer
        else
            Debug.LogWarning($"[Portrait] Layer '{LayerName}' not found. " +
                "Open the project once so Editor/UICharacterLayerSetup can create it.");

        // HDRP: solid black background.
        var hdCam = GetComponent<HDAdditionalCameraData>()
                 ?? gameObject.AddComponent<HDAdditionalCameraData>();
        hdCam.clearColorMode     = HDAdditionalCameraData.ClearColorMode.Color;
        hdCam.backgroundColorHDR = Color.black;

        // Use ONLY our own portrait volume — not the scene's post-process volumes,
        // and not the HDRP default (whose auto-exposure blows the character to
        // white and renders a default sky). We put the volume on the UICharacter
        // layer and restrict the camera's volume mask to it.
        if (uiLayer >= 0)
            hdCam.volumeLayerMask = 1 << uiLayer;
        BuildPortraitVolume(uiLayer);
        BuildPortraitLights();

        _cam.enabled = false;                  // only render while inventory is open
    }

    // Two point lights parented to the camera so they always sit in front of the
    // character (the camera reframes onto the player each frame). Point lights
    // stay local — unlike a directional light, they won't bleed across the scene.
    // Enabled only while the inventory is open (see LateUpdate).
    void BuildPortraitLights()
    {
        _keyLight  = MakeLight("PortraitKey",  new Vector3(-0.6f, 0.5f, 0f), keyLightIntensity);
        _fillLight = MakeLight("PortraitFill", new Vector3( 0.8f, 0.0f, 0f), fillLightIntensity);
    }

    Light MakeLight(string goName, Vector3 localPos, float intensity)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        var l     = go.AddComponent<Light>();
        l.type    = LightType.Point;
        l.range   = 12f;
        l.color   = new Color(1f, 0.97f, 0.92f);
        l.intensity = intensity;
        l.enabled = false;
        return l;
    }

    // Builds a dedicated global Volume with fixed exposure (no auto-exposure
    // blow-out) and no sky (so the background stays the camera's black clear).
    void BuildPortraitVolume(int layer)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        _exposure = profile.Add<Exposure>();
        _exposure.mode.Override(ExposureMode.Fixed);
        _exposure.fixedExposure.Override(fixedExposure);

        var sky = profile.Add<VisualEnvironment>();
        sky.skyType.Override(0);               // 0 = None → no sky rendered

        var volGO = new GameObject("PortraitVolume");
        if (layer >= 0) volGO.layer = layer;
        // Parented to the camera (which persists), so no DontDestroyOnLoad needed.
        volGO.transform.SetParent(transform, false);

        _volume          = volGO.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 100f;               // win over any scene volume
        _volume.profile  = profile;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Called by PlayerManager when the player model is created or swapped.
    public void SetTarget(Transform modelRoot)
    {
        _target = modelRoot;
        if (_target != null) Frame();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        bool open = GameUI.IsOpen;
        _cam.enabled = open;
        if (_keyLight  != null) _keyLight.enabled  = open;
        if (_fillLight != null) _fillLight.enabled = open;
        if (!open) return;

        // Allow live tuning from the inspector while playing.
        if (_exposure  != null) _exposure.fixedExposure.value = fixedExposure;
        if (_keyLight  != null) _keyLight.intensity  = keyLightIntensity;
        if (_fillLight != null) _fillLight.intensity = fillLightIntensity;

        if (_target == null) TryAutoWire();
        if (_target != null) Frame();
    }

    void TryAutoWire()
    {
        var player = PlayerManager.Instance?.Player;
        if (player == null) return;
        _target = player.transform.childCount > 0
            ? player.transform.GetChild(0)
            : player.transform;
    }

    // Frames the camera on the model's actual rendered bounds so the WHOLE
    // character always fits, regardless of how a pose/animation translates the
    // body (Mixamo idles can shift the hips down, etc.).
    void Frame()
    {
        Vector3 center;
        float   height;
        if (!TryGetModelBounds(out var b))
        {
            center = _target.position + Vector3.up * heightOffset;
            height = 2f;
        }
        else
        {
            center = b.center;
            height = Mathf.Max(b.size.y, 0.25f);
        }

        // Distance needed to fit `height` vertically within the camera's FOV.
        float halfFov = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float dist    = (height * 0.5f * framingMargin) / Mathf.Tan(halfFov);
        dist = Mathf.Max(dist, 0.5f);

        Vector3 dir = Quaternion.Euler(0f, yawOffset, 0f) * _target.forward;
        Vector3 pos = center + dir * dist;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(center - pos));
    }

    // Combined world bounds of the character BODY only (skinned meshes). Held
    // weapons/shields are plain MeshRenderers, so excluding them keeps the camera
    // framed on the character at a constant distance — weapons may extend out of
    // frame, which is fine.
    bool TryGetModelBounds(out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        foreach (var r in _target.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (r == null || !r.enabled) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }
}
