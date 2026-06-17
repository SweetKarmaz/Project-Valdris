using UnityEngine;

public class XPSystem : MonoBehaviour
{
    public static XPSystem Instance { get; private set; }

    public int CurrentXP { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddXP(int amount)
    {
        CurrentXP += amount;
        LevelSystem.Instance?.CheckLevelUp(CurrentXP);
    }

    public void RestoreState(int xp) => CurrentXP = xp;
}

