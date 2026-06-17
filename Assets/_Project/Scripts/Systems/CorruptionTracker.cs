using UnityEngine;

public class CorruptionTracker : MonoBehaviour
{
    public static CorruptionTracker Instance { get; private set; }

    [Range(0f, 100f)]
    public float corruptionLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddCorruption(float amount)
    {
        // High Wisdom resists a fraction of incoming corruption.
        var stats = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerStats>();
        if (stats != null) amount *= 1f - stats.CorruptionResistance;

        corruptionLevel = Mathf.Clamp(corruptionLevel + amount, 0f, 100f);
        HUDController.Instance?.UpdateCorruption(corruptionLevel);
    }

    public void ReduceCorruption(float amount) => corruptionLevel = Mathf.Max(0f, corruptionLevel - amount);
}

