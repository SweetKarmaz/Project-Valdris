using UnityEngine;

// Persistent in-game clock. A full 24h day passes in `dayLengthMinutes` real
// minutes (default 60). Tracks the Day number and time-of-day (hours 0..24),
// which the save system persists so reloading restores the exact day/time.
//
// Self-boots at play (RuntimeInitializeOnLoadMethod) and survives scene loads.
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    [Tooltip("Real minutes for one full 24-hour in-game day.")]
    public float dayLengthMinutes = 60f;

    public int   Day        { get; private set; } = 1;
    public float TimeOfDay  { get; private set; } = 8f;   // hours, 0..24
    public int   Hour => Mathf.FloorToInt(TimeOfDay) % 24;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("GameClock");
        go.AddComponent<GameClock>();   // Awake wires Instance + DontDestroyOnLoad
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        float secondsPerDay = Mathf.Max(1f, dayLengthMinutes * 60f);
        TimeOfDay += 24f / secondsPerDay * Time.deltaTime;
        while (TimeOfDay >= 24f) { TimeOfDay -= 24f; Day++; }
    }

    // Restore from a save.
    public void SetTime(int day, float timeOfDay)
    {
        Day = Mathf.Max(1, day);
        TimeOfDay = Mathf.Repeat(timeOfDay, 24f);
    }

    // New game.
    public void ResetClock()
    {
        Day = 1;
        TimeOfDay = 8f;
    }
}
