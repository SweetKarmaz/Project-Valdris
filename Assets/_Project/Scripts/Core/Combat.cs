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

        if (riders == null) return;
        CharacterBuffs buffs = null;
        foreach (var r in riders)
        {
            if (r == null) continue;

            if (r.damage > 0f)
                dmg.TakeDamage(new DamageInfo(r.damage, r.type, primary.source, primary.isCrit));

            if (r.debuff != null && Random.value <= r.procChance)
            {
                buffs ??= target.GetComponentInParent<CharacterBuffs>();
                buffs?.TryApplyStatus(r.debuff);   // immunity + resist handled inside
            }
        }
    }

    // Convenience for physical melee/projectile hits with optional riders.
    public static void ApplyHit(GameObject target, GameObject source, float physicalDamage,
                                IReadOnlyList<OnHitEffect> riders, bool isCrit = false)
        => ApplyHit(target, new DamageInfo(physicalDamage, DamageType.Physical, source, isCrit), riders);
}
