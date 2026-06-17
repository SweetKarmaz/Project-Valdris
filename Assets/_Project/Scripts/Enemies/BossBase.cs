using UnityEngine;

public class BossBase : EnemyBase
{
    [Header("Boss")]
    public string bossName;
    public float phase2Threshold = 0.5f;

    private bool _inPhase2;

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        if (!_inPhase2 && CurrentHealth / maxHealth <= phase2Threshold)
        {
            _inPhase2 = true;
            EnterPhase2();
        }
    }

    protected virtual void EnterPhase2()
    {
        Debug.Log($"{bossName} entering phase 2!");
    }
}
