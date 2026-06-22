using UnityEngine;

// Handles ranged weapon firing for the player in first-person.
// Add this component to the Player root alongside PlayerCombat.
//
// Setup:
//   1. Create an empty child GameObject on the camera named "ProjectileSpawnPoint".
//      Position it just in front of and slightly below the camera (e.g. 0, -0.1, 0.3).
//   2. Assign that Transform to the spawnPoint field.
//   3. Assign the player's equipped ranged LootItem to equippedWeapon.
//      (When CharacterEquipment is migrated to LootItem this will be wired automatically.)
//
// Fire flow:
//   AttackPressed → check weapon + ammo + cooldown → raycast for aim point →
//   apply spread → Instantiate projectile prefab → Launch() → consume ammo → start cooldown.
[RequireComponent(typeof(PlayerStats))]
public class PlayerRanged : MonoBehaviour
{
    [Header("Equipped Weapon")]
    [Tooltip("Leave unassigned — weapon is read from InventorySystem automatically. " +
             "Assign only to force a specific weapon for testing.")]
    public LootItem equippedWeaponOverride;

    [Header("Spawn")]
    [Tooltip("Empty child Transform on the camera from which projectiles emerge. " +
             "If unassigned, falls back to the active camera Transform.")]
    public Transform spawnPoint;

    [Tooltip("Layer mask the aim raycast tests against. " +
             "Include terrain, walls, props, and NPC layers; exclude the Player layer.")]
    public LayerMask aimMask = ~0;

    [Tooltip("Maximum aim raycast distance. Beyond this the projectile aims at the horizon point.")]
    public float aimRange = 200f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    PlayerStats _stats;
    float       _cooldown;   // seconds remaining until next shot is allowed

    // Auto-reads from InventorySystem; falls back to the inspector override.
    LootItem equippedWeapon =>
        equippedWeaponOverride
        ?? InventorySystem.Instance?.GetEquippedLoot(EquipSlot.MainHand);

    // 0..1 reload progress for HUD display (1 = ready to fire).
    public float ReloadProgress =>
        equippedWeapon != null && equippedWeapon.reloadTime > 0f
            ? Mathf.Clamp01(1f - _cooldown / equippedWeapon.reloadTime)
            : 1f;

    public bool ReadyToFire => _cooldown <= 0f;
    public bool HasWeapon   => equippedWeapon != null && equippedWeapon.IsWeapon
                               && equippedWeapon.weaponCategory == WeaponCategory.Ranged;
    public bool HasAmmo     => HasWeapon
                               && InventorySystem.Instance != null
                               && InventorySystem.Instance.HasProjectile(
                                      equippedWeapon.requiredProjectile);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake() => _stats = GetComponent<PlayerStats>();

    void Update()
    {
        if (_cooldown > 0f)
            _cooldown -= Time.deltaTime;

        if (PauseMenuController.IsPaused || GameUI.IsOpen || UIModal.IsOpen) return;
        if (InteractionHUD.HasTarget) return; // click is claimed by an interactable
        if (!HasWeapon)  return;
        if (!InputManager.AttackPressed) return;

        TryFire();
    }

    // ── Firing ────────────────────────────────────────────────────────────────

    void TryFire()
    {
        if (!ReadyToFire)
        {
            // Optional: play a "not ready" sound cue here.
            return;
        }

        var inv = InventorySystem.Instance;
        if (inv == null) return;

        ProjectileType needed = equippedWeapon.requiredProjectile;
        if (!inv.HasProjectile(needed))
        {
            Debug.Log($"[PlayerRanged] No {needed} — cannot fire {equippedWeapon.ItemName}.");
            return;
        }

        // Find the actual ammo LootItem (we need the prefab reference to Instantiate).
        LootItem ammo = FindAmmoItem(needed);
        if (ammo == null)
        {
            Debug.LogWarning($"[PlayerRanged] Ammo slot found for {needed} but LootItem is null.");
            return;
        }

        // Aim: raycast from camera centre → world point → direction from spawn.
        Vector3 direction = ComputeAimDirection();

        // Apply weapon spread (random cone offset).
        if (equippedWeapon.spread > 0f)
        {
            float half  = equippedWeapon.spread;
            direction   = Quaternion.Euler(
                Random.Range(-half, half),
                Random.Range(-half, half),
                0f) * direction;
            direction.Normalize();
        }

        // Play the bow draw → release animation on the viewmodel.
        PlayerViewmodel.Instance?.TriggerAttack();

        // Spawn and launch.
        Transform spawn    = GetSpawnPoint();
        var instance       = Instantiate(ammo.gameObject, spawn.position,
                                         Quaternion.LookRotation(direction));

        // Ensure ProjectileBehaviour exists on the prefab. If the designer forgot
        // to add it in the editor, add it at runtime as a fallback.
        var proj = instance.GetComponent<ProjectileBehaviour>()
                   ?? instance.AddComponent<ProjectileBehaviour>();

        // Damage is the arrow's own + the bow's (weaponDamage + elemental riders),
        // resolved on hit. Player melee AttackDamage does not apply to arrows.
        proj.Launch(direction, 0f, ammo, equippedWeapon, gameObject);

        // Consume one round of ammo.
        inv.ConsumeProjectile(needed);

        // Start per-weapon cooldown.
        _cooldown = equippedWeapon.reloadTime;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 ComputeAimDirection()
    {
        Camera cam = FirstPersonCamera.Active?.GetComponent<Camera>();
        if (cam == null) return transform.forward;

        // Cast from camera centre into the world.
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 aimPoint = Physics.Raycast(ray, out var hit, aimRange, aimMask)
            ? hit.point
            : ray.origin + ray.direction * aimRange;

        // Direction from the physical spawn point toward the aim world position.
        // This removes the parallax gap between reticle and projectile origin.
        Transform spawn = GetSpawnPoint();
        Vector3 dir     = (aimPoint - spawn.position);
        return dir.sqrMagnitude > 0.001f ? dir.normalized : cam.transform.forward;
    }

    Transform GetSpawnPoint()
    {
        if (spawnPoint != null) return spawnPoint;
        var fp = FirstPersonCamera.Active;
        return fp != null ? fp.transform : transform;
    }

    LootItem FindAmmoItem(ProjectileType type)
    {
        var slots = InventorySystem.Instance?.GetSlots();
        if (slots == null) return null;
        foreach (var slot in slots)
            if (slot.item != null
                && slot.item.itemType == LootItemType.Projectile
                && slot.item.projectileType == type
                && slot.count > 0)
                return slot.item;
        return null;
    }
}
