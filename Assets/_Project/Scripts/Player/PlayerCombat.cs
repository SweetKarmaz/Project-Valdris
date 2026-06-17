using System.Collections.Generic;
using UnityEngine;

// Handles first-person melee attacks. Fires only when the equipped main-hand weapon
// is a melee weapon (or the player is unarmed). PlayerRanged and PlayerThrown handle
// their own attack input for ranged and thrown weapons respectively.
//
// Hit detection: a sphere-sweep cast during the active swing window (opened and
// closed by PlayerViewmodel.IsInHitWindow). Each enemy can be hit at most once per
// swing; the sweep runs every frame during the window so targets that enter range
// mid-swing are still caught.
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("How far forward the sphere-sweep extends. Should match the weapon reach.")]
    public float attackRange = 2.5f;
    [Tooltip("Radius of the sphere used in the sweep. Larger = more forgiving hit box.")]
    public float sweepRadius = 0.40f;
    [Tooltip("Layers that the sweep can hit. Exclude the Player layer.")]
    public LayerMask enemyLayer;

    // ── Runtime ───────────────────────────────────────────────────────────────

    PlayerStats    _stats;
    CharacterBuffs _buffs;
    float          _cooldown;

    // Tracks enemies already struck in the current swing so each takes damage once.
    readonly HashSet<IDamageable> _struckThisSwing = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _buffs = GetComponent<CharacterBuffs>();
    }

    void Update()
    {
        if (PauseMenuController.IsPaused || GameUI.IsOpen || UIModal.IsOpen) return;
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        if (_buffs != null && _buffs.AreAbilitiesPrevented) return;
        if (InteractionHUD.HasTarget) return;     // click is claimed by an interactable
        if (!IsMeleeActive) return;               // bow / thrown handled by their scripts

        if (InputManager.AttackPressed && _cooldown <= 0f)
            BeginAttack();

        // Apply damage every frame during the viewmodel's hit window.
        if (PlayerViewmodel.Instance != null && PlayerViewmodel.Instance.IsInHitWindow)
            SweepHit();
    }

    // True when the player should be attacking in melee mode this frame.
    // Thrown weapons take priority over everything (PlayerThrown handles them).
    // Ranged bows are excluded; unarmed counts as melee.
    bool IsMeleeActive
    {
        get
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return false;
            if (inv.HasThrownAmmo) return false;
            var w = inv.GetEquippedLoot(EquipSlot.MainHand);
            if (w == null) return true; // unarmed
            return w.itemType == LootItemType.Weapon
                && w.weaponCategory != WeaponCategory.Ranged;
        }
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    void BeginAttack()
    {
        _cooldown = 1f / _stats.AttackSpeed;
        _struckThisSwing.Clear();
        PlayerViewmodel.Instance?.TriggerAttack();
    }

    void SweepHit()
    {
        var cam = FirstPersonCamera.Active;
        if (cam == null) return;

        var weapon = InventorySystem.Instance?.GetEquippedLoot(EquipSlot.MainHand);
        var riders = weapon?.onHitEffects;

        var hits = Physics.SphereCastAll(
            cam.transform.position,
            sweepRadius,
            cam.transform.forward,
            attackRange,
            enemyLayer);

        foreach (var hit in hits)
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || !_struckThisSwing.Add(target)) continue;
            if (((Component)target).gameObject == gameObject) continue; // never hit ourselves

            bool  isCrit = _stats.RollPhysicalCrit();
            float damage = _stats.AttackDamage * (isCrit ? _stats.CritMultiplier : 1f);
            Combat.ApplyHit(((Component)target).gameObject, gameObject, damage, riders, isCrit);
        }
    }
}
