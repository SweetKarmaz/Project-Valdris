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

    public LevelEntry[] levels;

    public int MaxLevel => levels != null ? levels.Length : 0;

    public LevelEntry GetEntry(int level)
    {
        if (levels == null || level < 1 || level > levels.Length) return null;
        return levels[level - 1];
    }
}
