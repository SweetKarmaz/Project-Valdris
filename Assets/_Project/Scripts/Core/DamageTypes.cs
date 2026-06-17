using UnityEngine;

// All damage types in the game. Serialized as ints on assets — append only,
// never reorder. Physical is weapon/projectile/thrown damage (mitigated by
// Defense). The elemental types deal full initial damage and may apply a debuff
// on hit (see OnHitEffect). Poison is reserved (colour only for now) and folded
// into Corruption gameplay-wise.
public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Holy,
    Corruption,
    Poison,
}

// A single instance of incoming damage.
public struct DamageInfo
{
    public float      amount;
    public DamageType type;
    public GameObject source;   // attacker (may be null, e.g. a DoT tick)
    public bool       isCrit;

    public DamageInfo(float amount, DamageType type, GameObject source = null, bool isCrit = false)
    {
        this.amount = amount;
        this.type   = type;
        this.source = source;
        this.isCrit = isCrit;
    }
}

// Anything that can receive typed damage: the player, NPCs, and enemies all
// implement this so combat, projectiles, and DoT ticks share one entry point.
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

// Floating-combat-text colour per damage type.
public static class DamageColors
{
    public static Color Of(DamageType type) => type switch
    {
        DamageType.Fire       => new Color(1f,   0.5f,  0.1f),   // orange
        DamageType.Ice        => new Color(0.5f, 0.85f, 1f),     // cyan
        DamageType.Lightning  => new Color(1f,   0.95f, 0.3f),   // yellow
        DamageType.Holy       => new Color(1f,   0.95f, 0.7f),   // pale gold
        DamageType.Corruption => new Color(0.7f, 0.3f,  0.95f),  // purple
        DamageType.Poison     => new Color(0.55f, 0.9f, 0.2f),   // sickly green
        _                     => Color.white,                    // physical
    };
}
