using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// First-person weapon viewmodel. Creates a dedicated depth-only camera that renders
// the equipped weapon model in camera space on the WeaponVM layer so it never clips
// through world geometry. Drives code-based swing animations for melee, bow, and
// thrown weapons.
//
// Setup (one-time):
//   1. In Edit > Project Settings > Tags and Layers, add a layer named "WeaponVM".
//   2. Set the vmLayer index on this component to match.
//   3. Add this component to the Player root.
//
// Other scripts call TriggerAttack() to start the animation, and read IsInHitWindow
// to know when the sweep hit-test should fire.
public class PlayerViewmodel : MonoBehaviour
{
    public static PlayerViewmodel Instance { get; private set; }

    [Header("Layer")]
    [Tooltip("Index of the 'WeaponVM' layer (Edit > Project Settings > Tags and Layers).")]
    public int vmLayer = 31;

    [Header("Viewmodel Camera")]
    [Tooltip("FOV applied to the VM camera. Slightly lower than the main FOV reduces distortion.")]
    public float vmFov      = 55f;
    public float vmNearClip = 0.04f;
    public float vmFarClip  = 6f;

    [Header("Rest Positions (camera-local)")]
    public Vector3 meleeRestPos = new Vector3( 0.27f, -0.28f, 0.45f);
    public Vector3 meleeRestRot = new Vector3( 10f,   -15f,    0f);
    public Vector3 bowRestPos   = new Vector3(-0.20f, -0.22f, 0.48f);
    public Vector3 bowRestRot   = new Vector3(  0f,    30f,   -5f);
    // Thrown weapon uses the same rest position as melee (right hand).

    [Header("Swing Timing (seconds)")]
    public float windupTime    = 0.12f;
    public float swingTime     = 0.26f;
    public float twoHandMult   = 1.55f;  // applied to windup + swing for 2H weapons
    public float returnTime    = 0.28f;
    public float bowDrawTime   = 0.28f;  // bow raise-to-aim duration
    public float bowRecoilTime = 0.10f;  // snap back on release

    // ── Enums ─────────────────────────────────────────────────────────────────

    enum VMMode  { Empty, MeleeOneHand, MeleeTwoHand, Bow, Thrown }
    enum VMPhase { Idle, Windup, Active, Return }

    // ── Runtime ───────────────────────────────────────────────────────────────

    Camera     _vmCam;
    Transform  _vmRoot;
    GameObject _weaponGO;

    VMMode  _mode  = VMMode.Empty;
    VMPhase _phase = VMPhase.Idle;
    float   _timer;

    LootItem _tracked;

    // Keyframes populated when each swing starts.
    Vector3    _kfRestPos, _kfWindupPos, _kfSwingPos;
    Quaternion _kfRestRot, _kfWindupRot, _kfSwingRot;

    // Set true during the active portion of the swing where hits can register.
    public bool IsInHitWindow { get; private set; }

    // ── Timing ────────────────────────────────────────────────────────────────

    float WindupDuration  => _mode == VMMode.MeleeTwoHand ? windupTime * twoHandMult : windupTime;
    float ActiveDuration
    {
        get
        {
            if (_mode == VMMode.Bow) return bowDrawTime;
            return _mode == VMMode.MeleeTwoHand ? swingTime * twoHandMult : swingTime;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake() => Instance = this;

    void Start()
    {
        BuildVMCamera();
        RefreshWeapon();
    }

    void Update()
    {
        TrackEquipChanges();
        TickAnimation();
    }

    // ── VM Camera ─────────────────────────────────────────────────────────────

    void BuildVMCamera()
    {
        var fp = FirstPersonCamera.Active;
        if (fp == null) { Invoke(nameof(BuildVMCamera), 0.15f); return; }

        Camera mainCam = fp.GetComponent<Camera>();
        if (mainCam == null) return;

        // Strip WeaponVM from the main camera so it never renders the viewmodel model.
        mainCam.cullingMask &= ~(1 << vmLayer);

        var camGO = new GameObject("_WeaponViewmodelCamera");
        camGO.transform.SetParent(fp.transform, false);
        camGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        _vmCam = camGO.AddComponent<Camera>();
        _vmCam.clearFlags    = CameraClearFlags.Depth;
        _vmCam.cullingMask   = 1 << vmLayer;
        _vmCam.depth         = mainCam.depth + 1;
        _vmCam.nearClipPlane = vmNearClip;
        _vmCam.farClipPlane  = vmFarClip;
        _vmCam.fieldOfView   = vmFov;

        // HDRP: disable volumes and, critically, suppress color clearing.
        // Without ClearColorMode.None, HDRP renders a sky/background over the main
        // camera's output even when Camera.clearFlags = Depth, blacking out the world.
        var hd = camGO.GetComponent<HDAdditionalCameraData>();
        if (hd == null) hd = camGO.AddComponent<HDAdditionalCameraData>();
        hd.volumeLayerMask  = 0;
        hd.clearColorMode   = HDAdditionalCameraData.ClearColorMode.None;

        var rootGO = new GameObject("_VMRoot");
        rootGO.transform.SetParent(camGO.transform, false);
        _vmRoot = rootGO.transform;
    }

    // ── Weapon model ──────────────────────────────────────────────────────────

    void TrackEquipChanges()
    {
        if (_vmRoot == null) return;
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        LootItem current = inv.GetEquippedThrown() ?? inv.GetEquippedLoot(EquipSlot.MainHand);
        if (current != _tracked) RefreshWeapon();
    }

    // Called automatically when equipped weapon changes, and by external code after
    // the inventory changes before the next TrackEquipChanges() poll fires.
    public void RefreshWeapon()
    {
        if (_weaponGO != null) { Destroy(_weaponGO); _weaponGO = null; }
        _phase = VMPhase.Idle;
        IsInHitWindow = false;

        if (_vmRoot == null) return;

        var inv  = InventorySystem.Instance;
        var item = inv?.GetEquippedThrown() ?? inv?.GetEquippedLoot(EquipSlot.MainHand);
        _tracked = item;

        if (item == null) { _mode = VMMode.Empty; return; }
        _mode = Classify(item);

        // Spawn a clean visual-only copy under the VM root.
        _weaponGO = Instantiate(item.gameObject, _vmRoot);
        _weaponGO.SetActive(true);
        _weaponGO.name = "_VMWeapon";

        foreach (var c in _weaponGO.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(c);
        foreach (var c in _weaponGO.GetComponentsInChildren<Collider>(true))      Destroy(c);
        foreach (var c in _weaponGO.GetComponentsInChildren<Rigidbody>(true))     Destroy(c);
        foreach (var l in _weaponGO.GetComponentsInChildren<Light>(true))         Destroy(l);
        SetLayer(_weaponGO, vmLayer);

        // Reuse the per-item grip offsets so it matches how the weapon looks on the
        // paperdoll character.
        HeldItemVisual.ApplyGrip(_weaponGO.transform, item, EquipSlot.MainHand);

        SnapToRest();
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    // Called by PlayerCombat, PlayerRanged, and PlayerThrown when the player attacks.
    public void TriggerAttack()
    {
        if (_mode == VMMode.Empty || _vmRoot == null) return;
        if (_phase != VMPhase.Idle) return;

        BuildKeyframes();
        _phase = VMPhase.Windup;
        _timer = 0f;
        IsInHitWindow = false;
    }

    void BuildKeyframes()
    {
        _kfRestPos = CurrentRestPos;
        _kfRestRot = CurrentRestRot;

        switch (_mode)
        {
            case VMMode.MeleeOneHand:
                _kfWindupPos = new Vector3( 0.33f, -0.16f, 0.38f);
                _kfWindupRot = Quaternion.Euler( 22f, -22f, -10f);
                _kfSwingPos  = new Vector3(-0.12f,  0.06f, 0.42f);
                _kfSwingRot  = Quaternion.Euler(-18f,  38f,  14f);
                break;
            case VMMode.MeleeTwoHand:
                _kfWindupPos = new Vector3( 0.28f, -0.10f, 0.34f);
                _kfWindupRot = Quaternion.Euler( 30f, -18f, -15f);
                _kfSwingPos  = new Vector3(-0.18f,  0.10f, 0.40f);
                _kfSwingRot  = Quaternion.Euler(-25f,  45f,  18f);
                break;
            case VMMode.Bow:
                // Rises from rest (bottom-left) to aim position, then recoils slightly on release.
                _kfWindupPos = new Vector3(-0.08f, -0.06f, 0.46f);
                _kfWindupRot = Quaternion.Euler( -4f,  16f,  -2f);
                _kfSwingPos  = new Vector3(-0.12f, -0.10f, 0.43f); // subtle recoil
                _kfSwingRot  = Quaternion.Euler( -2f,  22f,  -1f);
                break;
            case VMMode.Thrown:
                // Cocks back high then snaps forward (projectile spawns at release moment).
                _kfWindupPos = new Vector3( 0.28f,  0.10f, 0.37f);
                _kfWindupRot = Quaternion.Euler(-28f, -22f,   5f);
                _kfSwingPos  = new Vector3( 0.04f, -0.06f, 0.44f);
                _kfSwingRot  = Quaternion.Euler( 15f,   8f,   0f);
                break;
        }
    }

    void TickAnimation()
    {
        if (_phase == VMPhase.Idle || _vmRoot == null) return;

        _timer += Time.deltaTime;

        switch (_phase)
        {
            case VMPhase.Windup:
            {
                float t = Mathf.Clamp01(_timer / WindupDuration);
                _vmRoot.localPosition = Vector3.Lerp(_kfRestPos, _kfWindupPos, EaseIn(t));
                _vmRoot.localRotation = Quaternion.Slerp(_kfRestRot, _kfWindupRot, EaseIn(t));
                IsInHitWindow = false;
                if (_timer >= WindupDuration) { _timer = 0f; _phase = VMPhase.Active; }
                break;
            }
            case VMPhase.Active:
            {
                float t = Mathf.Clamp01(_timer / ActiveDuration);
                _vmRoot.localPosition = Vector3.Lerp(_kfWindupPos, _kfSwingPos, EaseOut(t));
                _vmRoot.localRotation = Quaternion.Slerp(_kfWindupRot, _kfSwingRot, EaseOut(t));
                // Hit window: first 65% of the active phase. For bow, the shot fires at
                // the moment of release (handled by PlayerRanged), not via sweep.
                IsInHitWindow = _mode != VMMode.Bow && _mode != VMMode.Thrown
                                && t > 0.05f && t < 0.65f;
                if (_timer >= ActiveDuration) { _timer = 0f; _phase = VMPhase.Return; IsInHitWindow = false; }
                break;
            }
            case VMPhase.Return:
            {
                float t = Mathf.Clamp01(_timer / returnTime);
                _vmRoot.localPosition = Vector3.Lerp(_kfSwingPos, _kfRestPos, EaseInOut(t));
                _vmRoot.localRotation = Quaternion.Slerp(_kfSwingRot, _kfRestRot, EaseInOut(t));
                IsInHitWindow = false;
                if (_timer >= returnTime) { _timer = 0f; _phase = VMPhase.Idle; SnapToRest(); }
                break;
            }
        }
    }

    void SnapToRest()
    {
        if (_vmRoot == null) return;
        _vmRoot.localPosition = CurrentRestPos;
        _vmRoot.localRotation = CurrentRestRot;
    }

    Vector3    CurrentRestPos => _mode == VMMode.Bow ? bowRestPos : meleeRestPos;
    Quaternion CurrentRestRot => Quaternion.Euler(_mode == VMMode.Bow ? bowRestRot : meleeRestRot);

    // ── Helpers ───────────────────────────────────────────────────────────────

    static VMMode Classify(LootItem item)
    {
        if (item == null) return VMMode.Empty;
        if (item.itemType == LootItemType.Projectile) return VMMode.Thrown;
        if (item.weaponCategory == WeaponCategory.Ranged) return VMMode.Bow;
        return item.isTwoHanded ? VMMode.MeleeTwoHand : VMMode.MeleeOneHand;
    }

    static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    static float EaseIn(float t)    => t * t;
    static float EaseOut(float t)   => 1f - (1f - t) * (1f - t);
    static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
}
