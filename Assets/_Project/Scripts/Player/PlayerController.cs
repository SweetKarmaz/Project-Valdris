using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    [Tooltip("How quickly the character turns to face the movement direction, degrees/second.")]
    public float turnSpeed = 720f;

    private CharacterController _cc;
    private PlayerStats _stats;
    private CharacterBuffs _buffs;
    private PlayerAnimator _animator;
    private Vector3 _velocity;
    private bool _isGrounded;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        _buffs = GetComponent<CharacterBuffs>();
    }

    private void Start()
    {
        _animator = GetComponent<PlayerAnimator>();
    }

    private void Update()
    {
        if (PauseMenuController.IsPaused || GameUI.IsOpen) return;
        if (_buffs != null && _buffs.IsMovementPrevented) return; // asleep/rooted

        _isGrounded = _cc.isGrounded;
        if (_isGrounded && _velocity.y < 0f) _velocity.y = -2f;

        Vector2 input = InputManager.Move;
        bool running = InputManager.SprintHeld;
        float speed = (running ? runSpeed : moveSpeed) + (_stats != null ? _stats.MoveSpeedBonus : 0f);
        speed = Mathf.Max(0.5f, speed); // slows can't fully root the player

        // Camera-relative movement: "forward" is wherever the camera looks
        // (flattened to the ground plane), and the character turns to face
        // the direction it is moving.
        Vector3 move = Vector3.zero;
        if (input.sqrMagnitude > 0.001f)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : transform;
            Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
            move = (camRight * input.x + camForward * input.y).normalized * Mathf.Min(input.magnitude, 1f);

            // In first-person mode the camera owns player yaw — skip body rotation.
            if (FirstPersonCamera.Active == null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }
        _cc.Move(move * speed * Time.deltaTime);

        // Normalise to [0,1]: walk=5 m/s → 0.56, run=9 m/s → 1.0
        float normSpeed = move.magnitude * speed / runSpeed;
        _animator?.SetSpeed(normSpeed);

        if (InputManager.JumpPressed && _isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }
}
