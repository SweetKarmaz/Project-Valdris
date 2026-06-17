using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, -5f);

    [Header("Rotation")]
    [Tooltip("Mouse delta is in pixels/frame, so sensitivity is much smaller than the legacy axis value.")]
    public float sensitivity = 0.15f;
    public float minPitch = -20f;
    public float maxPitch = 60f;

    private float _yaw;
    private float _pitch;

    private void OnEnable() => LockCursor(true);
    private void OnDisable() => LockCursor(false); // leaving gameplay frees the cursor

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Pause menu owns Esc and the cursor; alt-tab can also free it.
        // Either way, don't spin the camera while the cursor is free.
        if (PauseMenuController.IsPaused || GameUI.IsOpen || Cursor.lockState != CursorLockMode.Locked)
        {
            // Recover from alt-tab: click re-locks only when no UI is blocking input.
            if (!PauseMenuController.IsPaused && !GameUI.IsOpen && InputManager.AttackPressed) LockCursor(true);
            return;
        }

        Vector2 look = InputManager.Look;
        _yaw += look.x * sensitivity;
        _pitch -= look.y * sensitivity;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.position = target.position + rot * offset;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
