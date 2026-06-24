using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelTable", menuName = "Valdris/Level Table")]
public class LevelData : ScriptableObject
{
    [System.Serializable]
    public class LevelEntry
    {
        public int level;
        public int xpRequired;
        public int skillPointsGranted;
    }

    public LevelEntry[] levels;   // legacy hand-authored table (unused by the formula)

    [Header("XP Curve (polynomial)")]
    [Tooltip("Cumulative XP to reach a level = baseXP * (level-1)^exponent. " +
             "baseXP 300 + exponent 2 → level 2 costs 300, then 1200, 2700, ...")]
    public long  baseXP   = 300;
    public float exponent = 2f;
    [Tooltip("Hard level cap for this curve.")]
    public int   maxLevel = 999;
    [Tooltip("Skill points granted each level.")]
    public int   skillPointsPerLevel = 1;

    public int MaxLevel => maxLevel > 0 ? maxLevel : 999;

    // Total XP required to REACH the given level (level 1 = 0). Computed in double
    // for precision, returned as long for headroom at high levels.
    public long CumulativeXpForLevel(int level)
    {
        if (level <= 1) return 0;
        long   b = baseXP   > 0  ? baseXP   : 300;
        double e = exponent > 0f ? exponent : 2.0;
        return (long)System.Math.Round(b * System.Math.Pow(level - 1, e));
    }

    public int SkillPointsForLevel(int level) => skillPointsPerLevel > 0 ? skillPointsPerLevel : 1;

    public LevelEntry GetEntry(int level)   // legacy; no longer used for thresholds
    {
        if (levels == null || level < 1 || level > levels.Length) return null;
        return levels[level - 1];
    }
}
