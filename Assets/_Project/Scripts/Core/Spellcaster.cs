using System.Collections.Generic;
using UnityEngine;

// Universal spell-casting component. Works on any character — player, NPC, or enemy.
//
// PLAYER:  Spells must be learned first (tracked by SpellbookSystem).
//          Effective damage / mana cost are scaled by PlayerStats and buffs.
//
// NON-PLAYER: Drag spells into availableSpells in the inspector.
//             Flat values from SpellData are used — no stat math.
//
// Casting always goes through TryCast() — it handles the learned/available
// check, mana deduction, cooldown, projectile spawn, and animation.
[DisallowMultipleComponent]
public class Spellcaster : MonoBehaviour
{
    [Header("NPC / Enemy Spells")]
    [Tooltip("Leave empty for the player — their spells come from SpellbookSystem.")]
    public List<SpellData> availableSpells = new();

    [Header("Cast Origin")]
    [Tooltip("Point spells spawn from. Falls back to chest-height in front of the character.")]
    public Transform castPoint;

    // ── Private state ────────────────────────────────────────────────────────────

    bool _isPlayer;
    PlayerStats _stats;
    CharacterBuffs _buffs;
    CharacterAnimator _charAnim;
    CharacterAnimator CharAnim => _charAnim != null ? _charAnim : (_charAnim = GetComponent<CharacterAnimator>());
    readonly Dictionary<SpellData, float> _cooldownUntil = new();

    void Awake()
    {
        _stats    = GetComponent<PlayerStats>();
        _buffs    = GetComponent<CharacterBuffs>();
        // CharacterAnimator resolved lazily — may be added after Spellcaster on
        // dynamically built player GameObjects.
        _isPlayer = _stats != null;
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    // All spells this character is currently allowed to cast.
    public IReadOnlyList<SpellData> GetAvailableSpells()
    {
        if (_isPlayer && SpellbookSystem.Instance != null)
            return SpellbookSystem.Instance.GetSpells();
        return availableSpells;
    }

    public bool KnowsSpell(SpellData spell)
    {
        if (spell == null) return false;
        if (_isPlayer)
            return SpellbookSystem.Instance != null && SpellbookSystem.Instance.KnowsSpell(spell);
        return availableSpells.Contains(spell);
    }

    public float CooldownRemaining(SpellData spell)
    {
        if (spell == null) return 0f;
        return _cooldownUntil.TryGetValue(spell, out float t)
            ? Mathf.Max(0f, t - Time.time) : 0f;
    }

    public bool CanCast(SpellData spell)
    {
        if (spell == null) return false;
        if (CooldownRemaining(spell) > 0f) return false;
        if (!KnowsSpell(spell)) return false;
        if (_isPlayer && _stats != null && _stats.CurrentMana < EffectiveManaCost(spell)) return false;
        if (_buffs != null && _buffs.AreAbilitiesPrevented) return false;
        return true;
    }

    // Attempts to cast spell in the given world-space direction.
    // Returns true if the cast succeeded.
    public bool TryCast(SpellData spell, Vector3 direction)
    {
        if (!CanCast(spell)) return false;

        // Deduct mana (player only — NPCs have no mana pool).
        if (_isPlayer && _stats != null)
            _stats.UseMana(EffectiveManaCost(spell));

        // Start cooldown.
        float cd = _isPlayer ? EffectiveCooldown(spell) : spell.cooldown;
        if (cd > 0f) _cooldownUntil[spell] = Time.time + cd;

        // Corruption (player only).
        if (_isPlayer && spell.corruptionGain > 0f)
            CorruptionTracker.Instance?.AddCorruption(spell.corruptionGain);

        // Animation.
        CharAnim?.TriggerCast();

        // Cast VFX.
        Vector3 origin = GetCastOrigin();
        if (spell.castVFXPrefab != null)
            Instantiate(spell.castVFXPrefab, origin, Quaternion.identity);

        // Execute the spell.
        switch (spell.spellType)
        {
            case SpellType.Projectile:
                FireProjectile(spell, origin, direction);
                break;

            case SpellType.Utility:
                if (spell.statusBuff != null) ApplyStatusToNearest(spell);
                break;

            case SpellType.Detection:
                Debug.Log($"[Spellcaster] {name} cast Detection: {spell.spellName}");
                break;

            case SpellType.Shield:
                Debug.Log($"[Spellcaster] {name} cast Shield: {spell.spellName}");
                break;
        }

        return true;
    }

    // ── Effective value helpers (player has stat scaling; NPCs use flat values) ──

    public float EffectiveDamage(SpellData spell)
    {
        if (!_isPlayer || _stats == null) return spell.damage;
        return spell.damage
            * _stats.SpellPowerMultiplier
            * SpellModifierMultiplier(spell, m => m.damagePercent);
    }

    public float EffectiveManaCost(SpellData spell)
    {
        if (!_isPlayer) return 0f;
        return Mathf.Max(0f, spell.manaCost
            * SpellModifierMultiplier(spell, m => m.manaCostPercent));
    }

    public float EffectiveCooldown(SpellData spell)
    {
        if (!_isPlayer) return spell.cooldown;
        return Mathf.Max(0f, spell.cooldown
            * SpellModifierMultiplier(spell, m => m.cooldownPercent));
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    Vector3 GetCastOrigin()
    {
        if (castPoint != null) return castPoint.position;
        return transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;
    }

    void FireProjectile(SpellData spell, Vector3 origin, Vector3 direction)
    {
        if (spell.projectilePrefab == null)
        {
            Debug.LogWarning($"[Spellcaster] {spell.spellName} has no projectile prefab.");
            return;
        }

        var go = Instantiate(spell.projectilePrefab, origin,
                             Quaternion.LookRotation(direction.normalized));

        float damage = EffectiveDamage(spell);
        float range  = spell.range > 0f ? spell.range : 50f;

        // Support both projectile types — EmberProjectile and the generic SpellProjectile.
        var ember = go.GetComponent<EmberProjectile>();
        if (ember != null) { ember.Launch(direction, damage, range); return; }

        var generic = go.GetComponent<SpellProjectile>();
        if (generic != null)
        {
            bool isCrit = _isPlayer && _stats != null && _stats.RollSpellCrit();
            float finalDamage = isCrit ? damage * (_stats?.CritMultiplier ?? 1.5f) : damage;
            generic.Launch(finalDamage, range, isCrit);
        }
    }

    void ApplyStatusToNearest(SpellData spell)
    {
        float range = spell.range > 0f ? spell.range : 10f;
        CharacterBuffs nearest = null;
        float bestSqr = float.MaxValue;

        foreach (var col in Physics.OverlapSphere(transform.position, range))
        {
            if (col.transform == transform) continue;
            if (!col.TryGetComponent<CharacterBuffs>(out var buffs)) continue;
            float sqr = (col.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; nearest = buffs; }
        }

        if (nearest == null) return;

        float duration = spell.duration;
        if (_isPlayer && _stats != null)
            duration += Mathf.Max(0f, _stats.Intelligence - 10f) * spell.durationPerIntelligence;

        var result = nearest.TryApplyStatus(spell.statusBuff, duration);
        CombatTextSystem.Instance?.ShowStatusResult(nearest.transform, result, spell.statusBuff.buffName);
    }

    float SpellModifierMultiplier(SpellData spell, System.Func<SpellModifier, float> selector)
    {
        float percent = 0f;
        if (SkillSystem.Instance != null) percent += SkillSystem.Instance.SpellPercent(spell, selector);
        if (_buffs != null) percent += _buffs.SpellPercent(spell, selector);
        return 1f + percent / 100f;
    }
}
