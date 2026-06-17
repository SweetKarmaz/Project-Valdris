using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxHealth = 50f;
    public float attackDamage = 10f;
    public float defense = 2f;

    protected float CurrentHealth;

    protected virtual void Awake() => CurrentHealth = maxHealth;

    public virtual void TakeDamage(float amount) =>
        TakeDamage(new DamageInfo(amount, DamageType.Physical));

    public virtual void TakeDamage(float amount, bool isCrit) =>
        TakeDamage(new DamageInfo(amount, DamageType.Physical, null, isCrit));

    public virtual void TakeDamage(DamageInfo info)
    {
        float damage = info.type == DamageType.Physical
            ? Mathf.Max(0f, info.amount - defense)
            : Mathf.Max(0f, info.amount);
        CurrentHealth -= damage;
        CombatTextSystem.Instance?.ShowDamage(transform, damage, info.isCrit, info.type);
        GetComponent<CharacterBuffs>()?.NotifyDamageTaken(); // wakes sleepers
        if (CurrentHealth <= 0f) Die();
    }

    protected virtual void Die()
    {
        GetComponent<EnemyLootDropper>()?.DropLoot();
        Destroy(gameObject);
    }
}
