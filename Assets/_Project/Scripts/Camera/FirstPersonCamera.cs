using UnityEngine;

// First-person camera. Mounts at eye level; mouse X rotates the player body
// (so movement stays camera-relative), mouse Y pitches only the camera.
//
// Head bob: two-axis sine wave that ramps up when moving and fades out on stop.
// Amplitude increases slightly when sprinting. Toggle via GameSettings.HeadBobEnabled.
//
// The player model is kept off-screen via layer culling, not by toggling
// renderers: the model sits on the "UICharacter" layer, which the main camera
// excludes (see PlayerManager.SetupCamera). The inventory portrait camera
// renders that layer, so the same model can be shown there without conflict.
public class FirstPersonCamera : MonoBehaviour
{
    public static FirstPersonCamera Active { get; private set; }

    [Header("Look")]
    [Tooltip("Pitch clamp. Sensitivity and FOV are driven by GameSettings.")]
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Eye Position")]
    [Tooltip("Camera height above the player root (ground level). " +
             "Default matches a CharacterController height of 1.8 m.")]
    public float eyeHeight = 1.65f;

    [Header("Head Bob")]
    [Tooltip("Vertical bob amplitude while walking, in metres.")]
    public float walkBobAmount    = 0.04f;
    [Tooltip("Bob cycles per second while walking.")]
    public float walkBobFrequency = 1.8f;
    [Tooltip("Multiplier applied to both frequency and amplitude when sprinting.")]
    public float runBobMultiplier = 1.7f;

    Transform           _player;
    Camera              _cam;
    CharacterController _cc;

    public float Pitch => _pitch;

    float _yaw, _pitch;
    float _bobTimer;
    float _bobIntensity; // smoothed 0→1 blend used to ramp bob in/out

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake() => _cam = GetComponent<Camera>();

    void OnEnable()
    {
        Active = this;
        LockCursor(true);
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        LockCursor(false);
    }

    // Called by PlayerManager every scene load to (re)bind the player.
    public void SetTarget(Transform player)
    {
        _player = player;
        _cc     = player != null ? player.GetComponent<CharacterController>() : null;
        _yaw    = player != null ? player.eulerAngles.y : 0f;

        // A fresh scene must never inherit a cutscene movement-lock left dangling
        // by an interrupted sequence.
        CutsceneControl.ForceClear();

        // Restore camera pitch from a save if one is pending.
        if (SaveSystem.PendingCameraPitch.HasValue)
        {
            _pitch = SaveSystem.PendingCameraPitch.Value;
            SaveSystem.PendingCameraPitch = null;
        }
        else
        {
            _pitch = 0f;
        }

        // Re-lock the cursor whenever a new target is bound so scene transitions
        // (including from the intro cinematic which unlocks it) don't leave the
        // camera frozen.
        if (player != null) LockCursor(true);
    }

    // Snap the view to look at a world point. Used by scripted events ("turn the
    // player toward the NPC"). Applies immediately so it works even while look is
    // suspended (e.g. a modal dialogue is open).
    public void FaceToward(Vector3 worldPoint)
    {
        if (_player == null) return;
        Vector3 dir = worldPoint - transform.position;
        Vector3 flat = dir; flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f) _yaw = Quaternion.LookRotation(flat).eulerAngles.y;
        _pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg, minPitch, maxPitch);
        _player.rotation   = Quaternion.Euler(0f, _yaw, 0f);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    // ── Camera update ──────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_player == null) return;

        // Never re-lock during the death screen — cursor must stay free for the UI buttons.
        if (HUDController.Instance != null && HUDController.Instance.IsDead) return;

        // Yield control while paused, a UI window is open, or the cursor is freed.
        if (PauseMenuController.IsPaused || GameUI.IsOpen || UIModal.IsOpen
            || Cursor.lockState != CursorLockMode.Locked)
        {
            if (!PauseMenuController.IsPaused && !GameUI.IsOpen && !UIModal.IsOpen
                && InputManager.AttackPressed)
                LockCursor(true);
            return;
        }

        // Apply FOV from settings each frame (cheap no-op when unchanged).
        if (_cam != null) _cam.fieldOfView = GameSettings.FieldOfView;

        // Mouse look — sensitivity comes from GameSettings so the pause-menu
        // slider takes effect immediately without needing a restart.
        float sens = GameSettings.MouseSensitivity;
        Vector2 look = InputManager.Look;
        _yaw   += look.x * sens;
        _pitch -= look.y * sens;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);

        // Player body yaw follows camera so movement is always camera-relative.
        _player.rotation = Quaternion.Euler(0f, _yaw, 0f);

        // Bob is in player-local space so it always stays level with the floor.
        Vector3 bob      = ComputeBob();
        Vector3 eyeBase  = _player.position + Vector3.up * eyeHeight;
        Vector3 bobWorld = _player.right * bob.x + Vector3.up * bob.y;

        transform.SetPositionAndRotation(
            eyeBase + bobWorld,
            Quaternion.Euler(_pitch, _yaw, 0f));
    }

    // ── Head bob ───────────────────────────────────────────────────────────────

    Vector3 ComputeBob()
    {
        if (!GameSettings.HeadBobEnabled)
        {
            _bobIntensity = 0f;
            _bobTimer     = 0f;
            return Vector3.zero;
        }

        float horizSpeed = _cc != null
            ? new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude
            : 0f;

        bool moving  = horizSpeed > 0.3f;
        bool running = moving && InputManager.SprintHeld;

        // Smoothly ramp intensity so bob fades in/out rather than snapping.
        float targetIntensity = moving ? 1f : 0f;
        _bobIntensity = Mathf.MoveTowards(_bobIntensity, targetIntensity, Time.deltaTime * 8f);

        float freq   = walkBobFrequency * (running ? runBobMultiplier : 1f);
        float amount = walkBobAmount    * (running ? runBobMultiplier : 1f);

        // Advance timer proportional to intensity so the wave dies cleanly.
        _bobTimer += Time.deltaTime * freq * _bobIntensity;

        float t        = _bobTimer * Mathf.PI * 2f;
        float vertical = Mathf.Sin(t)          * amount * _bobIntensity;
        float lateral  = Mathf.Cos(t * 0.5f)  * amount * 0.5f * _bobIntensity;

        return new Vector3(lateral, vertical, 0f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
