using UnityEngine;

// Handles thrown-weapon attacks for the player.
// Thrown weapons (ThrowingKnife, Javelin, Dart, etc.) live in the player's
// inventory as stackable Projectile-type LootItems.  The player equips one
// stack into InventorySystem's thrown-weapon slot, which this component reads
// each frame.  On each throw:
//   1. Deduct one from the inventory stack via InventorySystem.ConsumeThrown().
//   2. Spawn the LootItem prefab and call ProjectileBehaviour.Launch().
//   3. Start the per-item throwCooldown.
// When the stack runs out InventorySystem auto-clears the slot and the
// MainHand equipment UI updates via OnThrownEquipChanged.
[RequireComponent(typeof(PlayerStats))]
public class PlayerThrown : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Empty child Transform on the camera from which thrown weapons emerge. " +
             "If unassigned, falls back to the active camera Transform.")]
    public Transform spawnPoint;

    [Tooltip("Layer mask the aim raycast tests against. " +
             "Include terrain, walls, props, and NPC layers; exclude the Player layer.")]
    public LayerMask aimMask = ~0;

    [Tooltip("Maximum aim raycast distance. Beyond this the weapon aims at the horizon point.")]
    public float aimRange = 60f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    PlayerStats _stats;
    float       _cooldown;

    LootItem EquippedThrown => InventorySystem.Instance?.GetEquippedThrown();

    public bool ReadyToThrow   => _cooldown <= 0f;
    public bool HasThrownWeapon => InventorySystem.Instance != null
                                   && InventorySystem.Instance.HasThrownAmmo;

    // 0..1 progress for HUD display (1 = ready to throw).
    public float ThrowProgress =>
        EquippedThrown != null && EquippedThrown.throwCooldown > 0f
            ? Mathf.Clamp01(1f - _cooldown / EquippedThrown.throwCooldown)
            : 1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake() => _stats = GetComponent<PlayerStats>();

    void Update()
    {
        if (_cooldown > 0f)
            _cooldown -= Time.deltaTime;

        if (PauseMenuController.IsPaused || GameUI.IsOpen) return;
        if (InteractionHUD.HasTarget) return;
        if (!HasThrownWeapon) return;
        if (!InputManager.AttackPressed) return;

        TryThrow();
    }

    // ── Throwing ──────────────────────────────────────────────────────────────

    void TryThrow()
    {
        if (!ReadyToThrow) return;

        var inv = InventorySystem.Instance;
        if (inv == null) return;

        LootItem thrown = EquippedThrown;
        if (thrown == null || inv.GetCount(thrown) <= 0)
        {
            Debug.Log("[PlayerThrown] No thrown weapon equipped or stack empty.");
            return;
        }

        Vector3 direction = ComputeAimDirection();

        if (thrown.spread > 0f)
        {
            float half = thrown.spread;
            direction  = Quaternion.Euler(
                Random.Range(-half, half),
                Random.Range(-half, half),
                0f) * direction;
            direction.Normalize();
        }

        Transform spawn = GetSpawnPoint();
        var instance    = Instantiate(thrown.gameObject, spawn.position,
                                      Quaternion.LookRotation(direction));

        var proj = instance.GetComponent<ProjectileBehaviour>()
                   ?? instance.AddComponent<ProjectileBehaviour>();

        float attackerDamage = _stats != null ? _stats.AttackDamage : 0f;
        proj.Launch(direction, attackerDamage, thrown);

        // Deduct one knife; InventorySystem auto-unequips if the stack hits 0.
        inv.ConsumeThrown();

        _cooldown = thrown.throwCooldown;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 ComputeAimDirection()
    {
        Camera cam = FirstPersonCamera.Active?.GetComponent<Camera>();
        if (cam == null) return transform.forward;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 aimPoint = Physics.Raycast(ray, out var hit, aimRange, aimMask)
            ? hit.point
            : ray.origin + ray.direction * aimRange;

        Transform spawn = GetSpawnPoint();
        Vector3 dir     = aimPoint - spawn.position;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : cam.transform.forward;
    }

    Transform GetSpawnPoint()
    {
        if (spawnPoint != null) return spawnPoint;
        var fp = FirstPersonCamera.Active;
        return fp != null ? fp.transform : transform;
    }
}
