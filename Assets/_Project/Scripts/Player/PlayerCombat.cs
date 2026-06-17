using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat")]
    public float attackRange = 2f;
    public LayerMask enemyLayer;

    private PlayerStats _stats;
    private CharacterBuffs _buffs;
    private float _attackCooldown;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _buffs = GetComponent<CharacterBuffs>();
    }

    private void Update()
    {
        if (PauseMenuController.IsPaused || GameUI.IsOpen || UIModal.IsOpen) return;
        if (_attackCooldown > 0f) _attackCooldown -= Time.deltaTime;
        if (_buffs != null && _buffs.AreAbilitiesPrevented) return; // asleep/stunned
        if (InteractionHUD.HasTarget) return; // left-click is claimed by an interaction
        if (InputManager.AttackPressed && _attackCooldown <= 0f) Attack();
    }

    private void Attack()
    {
        _attackCooldown = 1f / _stats.AttackSpeed;

        // Elemental riders come from the equipped main-hand weapon.
        var weapon = InventorySystem.Instance?.GetEquippedLoot(EquipSlot.MainHand);
        var riders = weapon != null ? weapon.onHitEffects : null;

        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, attackRange, enemyLayer);
        var struckThisSwing = new HashSet<IDamageable>();
        foreach (Collider hit in hits)
        {
            var target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !struckThisSwing.Add(target)) continue;   // one hit per target

            // Physical crit: rolled per target hit, scales with Dexterity.
            bool isCrit = _stats.RollPhysicalCrit();
            float damage = _stats.AttackDamage * (isCrit ? _stats.CritMultiplier : 1f);
            Combat.ApplyHit(((Component)target).gameObject, gameObject, damage, riders, isCrit);
        }
    }
}
