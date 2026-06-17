using UnityEngine;

// Central store for all game settings.
//
// Values live in memory (the public static fields) so the game reads them
// directly every frame — no property overhead, live preview works for free.
// Call Save() to persist to PlayerPrefs; Load() runs automatically before
// any scene loads so saved values are in effect from the first frame.
//
// Adding a new setting:
//   1. Add a DefaultXxx const and a public static field here.
//   2. Add a PlayerPrefs key constant and read/write it in Load() / Save().
//   3. Add ResetToDefaults() and Capture() / Restore() lines.
//   4. Add a control in SettingsPanel.DrawContent().
public static class GameSettings
{
    // ── Defaults ──────────────────────────────────────────────────────────────

    public const bool  DefaultHeadBobEnabled        = true;
    public const float DefaultMouseSensitivitySlider = 0f;
    public const float DefaultFieldOfView            = 60f;
    public const float DefaultMasterVolume           = 1f;
    public const float DefaultEffectsVolume          = 1f;
    public const float DefaultMusicVolume            = 1f;
    public const float DefaultVoiceVolume            = 1f;

    public const float MinFOV = 40f;
    public const float MaxFOV = 80f;

    // ── Working values (game reads these at runtime) ───────────────────────────

    public static bool  HeadBobEnabled         = DefaultHeadBobEnabled;
    public static float MouseSensitivitySlider = DefaultMouseSensitivitySlider;
    public static float FieldOfView            = DefaultFieldOfView;
    public static float MasterVolume           = DefaultMasterVolume;
    public static float EffectsVolume          = DefaultEffectsVolume;
    public static float MusicVolume            = DefaultMusicVolume;
    public static float VoiceVolume            = DefaultVoiceVolume;

    // Derived: actual per-frame sensitivity used by FirstPersonCamera.
    //   slider 0  → 1.00 × base  (base = 0.15)
    //   slider -1 → 0.70 × base  (30 % slower)
    //   slider +1 → 1.30 × base  (30 % faster)
    const float BaseSensitivity = 0.15f;
    public static float MouseSensitivity =>
        BaseSensitivity * (1f + MouseSensitivitySlider * 0.3f);

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    const string KeyHeadBob    = "setting_headbob";
    const string KeyMouseSens  = "setting_mousesens";
    const string KeyFOV        = "setting_fov";
    const string KeyMasterVol  = "setting_master_vol";
    const string KeyEffectsVol = "setting_effects_vol";
    const string KeyMusicVol   = "setting_music_vol";
    const string KeyVoiceVol   = "setting_voice_vol";

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    // Runs automatically before the first scene loads — no manual call needed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        HeadBobEnabled         = PlayerPrefs.GetInt(KeyHeadBob,    DefaultHeadBobEnabled ? 1 : 0) == 1;
        MouseSensitivitySlider = PlayerPrefs.GetFloat(KeyMouseSens, DefaultMouseSensitivitySlider);
        FieldOfView            = PlayerPrefs.GetFloat(KeyFOV,       DefaultFieldOfView);
        MasterVolume           = PlayerPrefs.GetFloat(KeyMasterVol,  DefaultMasterVolume);
        EffectsVolume          = PlayerPrefs.GetFloat(KeyEffectsVol, DefaultEffectsVolume);
        MusicVolume            = PlayerPrefs.GetFloat(KeyMusicVol,   DefaultMusicVolume);
        VoiceVolume            = PlayerPrefs.GetFloat(KeyVoiceVol,   DefaultVoiceVolume);

        AudioListener.volume = MasterVolume;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public static void Save()
    {
        PlayerPrefs.SetInt(KeyHeadBob,    HeadBobEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(KeyMouseSens, MouseSensitivitySlider);
        PlayerPrefs.SetFloat(KeyFOV,       FieldOfView);
        PlayerPrefs.SetFloat(KeyMasterVol,  MasterVolume);
        PlayerPrefs.SetFloat(KeyEffectsVol, EffectsVolume);
        PlayerPrefs.SetFloat(KeyMusicVol,   MusicVolume);
        PlayerPrefs.SetFloat(KeyVoiceVol,   VoiceVolume);
        PlayerPrefs.Save();
    }

    // ── Defaults & snapshot ───────────────────────────────────────────────────

    public static void ResetToDefaults()
    {
        HeadBobEnabled         = DefaultHeadBobEnabled;
        MouseSensitivitySlider = DefaultMouseSensitivitySlider;
        FieldOfView            = DefaultFieldOfView;
        MasterVolume           = DefaultMasterVolume;
        EffectsVolume          = DefaultEffectsVolume;
        MusicVolume            = DefaultMusicVolume;
        VoiceVolume            = DefaultVoiceVolume;
    }

    // Captures the current state so SettingsPanel can offer Revert.
    public static GameSettingsSnapshot Capture() => new GameSettingsSnapshot
    {
        HeadBobEnabled         = HeadBobEnabled,
        MouseSensitivitySlider = MouseSensitivitySlider,
        FieldOfView            = FieldOfView,
        MasterVolume           = MasterVolume,
        EffectsVolume          = EffectsVolume,
        MusicVolume            = MusicVolume,
        VoiceVolume            = VoiceVolume,
    };

    public static void Restore(in GameSettingsSnapshot s)
    {
        HeadBobEnabled         = s.HeadBobEnabled;
        MouseSensitivitySlider = s.MouseSensitivitySlider;
        FieldOfView            = s.FieldOfView;
        MasterVolume           = s.MasterVolume;
        EffectsVolume          = s.EffectsVolume;
        MusicVolume            = s.MusicVolume;
        VoiceVolume            = s.VoiceVolume;

        // Apply audio immediately so live preview reverts too.
        AudioListener.volume = MasterVolume;
    }
}

// Plain value-type snapshot — no heap allocation, safe to pass by value.
public struct GameSettingsSnapshot
{
    public bool  HeadBobEnabled;
    public float MouseSensitivitySlider;
    public float FieldOfView;
    public float MasterVolume;
    public float EffectsVolume;
    public float MusicVolume;
    public float VoiceVolume;
}
