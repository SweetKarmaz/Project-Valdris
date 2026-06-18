using System;
using UnityEngine;

// The player's Corruption meter. Fills from dealing Corruption damage, corrupting
// spells, and corrupting zones/quests; drained at cleansing shrines/areas.
// `corruptionLevel` is raw points in [0, corruptionMax]; thresholds and the HUD
// work off Percent (0–100).
public class CorruptionTracker : MonoBehaviour
{
    public static CorruptionTracker Instance { get; private set; }

    [Tooltip("Current corruption in raw points (saved). Clamped to [0, Corruption Max].")]
    public float corruptionLevel;

    [Min(1f)]
    [Tooltip("Points needed to completely fill the Corruption bar.")]
    public float corruptionMax = 100f;

    [Range(0f, 1f)]
    [Tooltip("Fraction of Corruption damage the player DEALS that is added to their own " +
             "Corruption meter (0.5 = half the damage becomes corruption points).")]
    public float corruptionPerDamageDealt = 0.5f;

    public float Percent    => corruptionMax > 0f ? corruptionLevel / corruptionMax * 100f : 0f;
    public float Normalized => corruptionMax > 0f ? corruptionLevel / corruptionMax : 0f;

    // Raised whenever the meter changes (UI / NPC reactions can subscribe).
    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => Refresh();

    // Adds corruption (positive). High Wisdom resists a fraction of the gain.
    public void AddCorruption(float amount)
    {
        if (amount <= 0f) return;
        // High Wisdom slows how fast the meter fills (self-corruption reduction).
        var stats = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerStats>();
        if (stats != null) amount *= 1f - stats.CorruptionGainResistance;
        SetCorruption(corruptionLevel + amount);
    }

    // Corruption gained from dealing Corruption damage (scaled by the factor).
    public void AddFromDamageDealt(float corruptionDamage) =>
        AddCorruption(corruptionDamage * corruptionPerDamageDealt);

    // Drains corruption (e.g. shrines). No Wisdom scaling.
    public void ReduceCorruption(float amount)
    {
        if (amount <= 0f) return;
        SetCorruption(corruptionLevel - amount);
    }

    // Clears the meter for a fresh New Game.
    public void ResetCorruption() => SetCorruption(0f);

    void SetCorruption(float value)
    {
        corruptionLevel = Mathf.Clamp(value, 0f, corruptionMax);
        Refresh();
    }

    void Refresh()
    {
        HUDController.Instance?.UpdateCorruption(Percent);
        OnChanged?.Invoke();
    }
}

// How an NPC regards the player's corruption level.
public enum CorruptionStance
{
    None,     // unaware / unaffected
    Notice,   // remarks on it, still interacts
    Refuse,   // won't trade or deal with the player
    Hostile,  // attacks the player
}
