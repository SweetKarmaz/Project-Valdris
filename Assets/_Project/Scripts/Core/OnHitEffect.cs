using System;
using UnityEngine;

// An on-hit "rider" carried by a weapon, projectile, or spell: extra typed
// damage and/or a chance to apply a debuff (BuffData) to whatever is hit.
// e.g. a flaming sword = a Fire rider with bonus damage + a "Burning" DoT debuff;
// a frost bolt = an Ice rider with a "Chilled" Slow debuff.
[Serializable]
public class OnHitEffect
{
    [Tooltip("Damage type of this rider (drives colour, resistances, and the debuff theme).")]
    public DamageType type = DamageType.Fire;

    [Tooltip("Extra damage dealt on hit, of the type above. Can be 0 for a pure debuff rider.")]
    public float damage;

    [Range(0f, 1f)]
    [Tooltip("Chance to ATTEMPT the debuff (Corruption should be 1). The target's " +
             "resist roll still decides whether it sticks.")]
    public float procChance = 1f;

    [Tooltip("Debuff applied on a successful proc (a DoT, a Slow, etc.). Optional.")]
    public BuffData debuff;
}
