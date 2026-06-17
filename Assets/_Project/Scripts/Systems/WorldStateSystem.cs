using UnityEngine;
using System;
using System.Collections.Generic;

// Records the outcomes of binary world choices ("saved the miller",
// "sided with the garrison", "crypt cleared"). Other systems subscribe to
// OnFlagChanged or query GetFlag to vary the world accordingly.
public class WorldStateSystem : MonoBehaviour
{
    public static WorldStateSystem Instance { get; private set; }

    private readonly Dictionary<string, bool> _flags = new();

    public static event Action<string, bool> OnFlagChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetFlag(string key, bool value)
    {
        if (_flags.TryGetValue(key, out bool current) && current == value) return;
        _flags[key] = value;
        OnFlagChanged?.Invoke(key, value);
    }

    // Returns false for flags that were never set.
    public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;

    public bool HasFlag(string key) => _flags.ContainsKey(key);

    // ---- Save/load ----

    public List<WorldFlag> CaptureState()
    {
        var saved = new List<WorldFlag>();
        foreach (var kvp in _flags)
            saved.Add(new WorldFlag { key = kvp.Key, value = kvp.Value });
        return saved;
    }

    public void RestoreState(List<WorldFlag> saved)
    {
        _flags.Clear();
        if (saved == null) return;
        foreach (WorldFlag flag in saved)
            _flags[flag.key] = flag.value;
    }
}

// JsonUtility cannot serialize dictionaries, so flags are saved as a list.
[Serializable]
public class WorldFlag
{
    public string key;
    public bool value;
}
