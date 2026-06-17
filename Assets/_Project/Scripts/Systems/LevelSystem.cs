using UnityEngine;

public class LevelSystem : MonoBehaviour
{
    public static LevelSystem Instance { get; private set; }

    public int CurrentLevel { get; private set; } = 1;
    public int UnspentSkillPoints { get; private set; }
    public LevelData levelTable;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void CheckLevelUp(int totalXP)
    {
        if (levelTable == null) return;
        // Loop in case a large XP grant crosses multiple thresholds at once
        while (CurrentLevel < levelTable.MaxLevel)
        {
            LevelData.LevelEntry next = levelTable.GetEntry(CurrentLevel + 1);
            if (next == null || totalXP < next.xpRequired) break;
            CurrentLevel++;
            UnspentSkillPoints += next.skillPointsGranted;
            LevelUpUI.Instance?.Show(CurrentLevel);
        }
    }

    public void RestoreState(int level, int unspentSkillPoints)
    {
        CurrentLevel = Mathf.Max(1, level);
        UnspentSkillPoints = Mathf.Max(0, unspentSkillPoints);
    }

    public bool SpendSkillPoints(int count)
    {
        if (count <= 0 || UnspentSkillPoints < count) return false;
        UnspentSkillPoints -= count;
        return true;
    }
}

