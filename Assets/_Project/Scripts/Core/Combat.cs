using System.Collections.Generic;
using UnityEngine;

// Central helper for resolving a hit against a target: applies the primary
// damage, then each on-hit rider's bonus damage and (rolled) debuff. Used by
// melee, projectiles, thrown weapons, and spells so they all behave the same.
public static class Combat
{
    public static void ApplyHit(GameObject target, DamageInfo primary, IReadOnlyList<OnHitEffect> riders)
    {
        if (target == null) return;
        var dmg = target.GetComponentInParent<IDamageable>();
        if (dmg == null) return;

        dmg.TakeDamage(primary);
        AccrueCorruption(primary);

        if (riders == null) return;
        CharacterBuffs buffs = null;
        foreach (var r in riders)
        {
            if (r == null) continue;

            if (r.damage > 0f)
            {
                var rd = new DamageInfo(r.damage, r.type, primary.source, primary.isCrit);
                dmg.TakeDamage(rd);
                AccrueCorruption(rd);
            }

            if (r.debuff != null && Random.value <= r.procChance)
            {
                buffs ??= target.GetComponentInParent<CharacterBuffs>();
                buffs?.TryApplyStatus(r.debuff);   // immunity + resist handled inside
            }
        }
    }

    // When the PLAYER deals Corruption damage, it taints them — fill their meter
    // proportionally to the corruption damage dealt.
    static void AccrueCorruption(DamageInfo info)
    {
        if (info.type != DamageType.Corruption || info.amount <= 0f) return;
        if (info.source == null || !info.source.CompareTag("Player")) return;
        CorruptionTracker.Instance?.AddFromDamageDealt(info.amount);
    }

    // Convenience for physical melee/projectile hits with optional riders.
    public static void ApplyHit(GameObject target, GameObject source, float physicalDamage,
                                IReadOnlyList<OnHitEffect> riders, bool isCrit = false)
        => ApplyHit(target, new DamageInfo(physicalDamage, DamageType.Physical, source, isCrit), riders);
}
