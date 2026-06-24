using UnityEngine;

public class LevelSystem : MonoBehaviour
{
    public static LevelSystem Instance { get; private set; }

    public const int LevelCap = 999;

    // Every 5th level grants an attribute point instead of a skill point.
    public const int AttributeMilestone = 5;

    public int CurrentLevel { get; private set; } = 1;
    public int UnspentSkillPoints { get; private set; }
    public int UnspentAttributePoints { get; private set; }
    public LevelData levelTable;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void CheckLevelUp(long totalXP)
    {
        if (levelTable == null) return;
        // Loop in case a large XP grant crosses multiple thresholds at once (XP
        // rolls over naturally — extra counts toward the next level). Capped at
        // LevelCap and the curve's own maxLevel.
        int cap = Mathf.Min(levelTable.MaxLevel, LevelCap);
        while (CurrentLevel < cap)
        {
            if (totalXP < levelTable.CumulativeXpForLevel(CurrentLevel + 1)) break;
            CurrentLevel++;
            // Every 5th level → an attribute point; otherwise skill point(s).
            if (CurrentLevel % AttributeMilestone == 0) UnspentAttributePoints += 1;
            else UnspentSkillPoints += levelTable.SkillPointsForLevel(CurrentLevel);
            LevelUpUI.Instance?.Show(CurrentLevel);
        }
    }

    // ── UI helpers (per-level progress, derived from cumulative XP) ────────────

    public int  Cap        => levelTable != null ? Mathf.Min(levelTable.MaxLevel, LevelCap) : LevelCap;
    public bool IsMaxLevel => CurrentLevel >= Cap;

    long TotalXP => XPSystem.Instance != null ? XPSystem.Instance.CurrentXP : 0;

    // XP earned into the current level, and the XP span of the current level.
    public long XpIntoCurrentLevel =>
        levelTable == null ? 0 : System.Math.Max(0, TotalXP - levelTable.CumulativeXpForLevel(CurrentLevel));

    public long XpForCurrentLevel =>
        levelTable == null ? 0 : levelTable.CumulativeXpForLevel(CurrentLevel + 1) - levelTable.CumulativeXpForLevel(CurrentLevel);

    public long XpToNextLevel => System.Math.Max(0, XpForCurrentLevel - XpIntoCurrentLevel);

    public float LevelProgress
    {
        get { long span = XpForCurrentLevel; return span > 0 ? Mathf.Clamp01((float)XpIntoCurrentLevel / span) : 1f; }
    }

    public void RestoreState(int level, int unspentSkillPoints, int unspentAttributePoints)
    {
        CurrentLevel = Mathf.Max(1, level);
        UnspentSkillPoints = Mathf.Max(0, unspentSkillPoints);
        UnspentAttributePoints = Mathf.Max(0, unspentAttributePoints);
    }

    // Fresh start (New Game).
    public void ResetState()
    {
        CurrentLevel = 1;
        UnspentSkillPoints = 0;
        UnspentAttributePoints = 0;
    }

    public bool SpendSkillPoints(int count)
    {
        if (count <= 0 || UnspentSkillPoints < count) return false;
        UnspentSkillPoints -= count;
        return true;
    }

    public bool SpendAttributePoints(int count)
    {
        if (count <= 0 || UnspentAttributePoints < count) return false;
        UnspentAttributePoints -= count;
        return true;
    }
}

