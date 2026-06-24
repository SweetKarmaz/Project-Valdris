using UnityEngine;

public class XPSystem : MonoBehaviour
{
    public static XPSystem Instance { get; private set; }

    public long CurrentXP { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;
        // Skill-based XP gain bonus.
        if (SkillSystem.Instance != null)
        {
            float bonus = SkillSystem.Instance.XpGainPercent();
            if (bonus > 0f) amount = Mathf.RoundToInt(amount * (1f + bonus / 100f));
        }
        CurrentXP += amount;
        LevelSystem.Instance?.CheckLevelUp(CurrentXP);
    }

    public void RestoreState(long xp) => CurrentXP = xp;

    // Fresh start (New Game).
    public void ResetState() => CurrentXP = 0;
}

