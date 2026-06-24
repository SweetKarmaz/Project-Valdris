using UnityEngine;

// Shared settings panel used by both MainMenuController and PauseMenuController.
//
// Usage:
//   Call Open() when entering the settings screen — captures a revert snapshot.
//   Call Draw(onBack) from OnGUI every frame while the settings screen is active.
//
// Changes are live (the game reads GameSettings directly) but NOT persisted until
// Save is pressed. Back with unsaved changes shows a confirmation popup.
//
// To add a new setting: add it to GameSettings, then add a control in DrawContent().
public static class SettingsPanel
{
    static GameSettingsSnapshot _snapshot;  // state when panel was opened — used by Revert
    static bool _isDirty;                   // true if in-memory values differ from snapshot
    static bool _showPopup;
    static System.Action _pendingBack;      // stored so the popup can invoke it

    static bool _inControls;                // showing the keybinds sub-screen
    static InputManager.Rebindable _rebindingEntry;  // row currently listening for a key
    static Vector2 _controlsScroll;

    // ── Entry point ───────────────────────────────────────────────────────────

    // Call this whenever the player navigates INTO the settings screen.
    public static void Open()
    {
        _snapshot  = GameSettings.Capture();
        _isDirty   = false;
        _showPopup = false;
        _inControls = false;
        _rebindingEntry = null;
    }

    // Call from OnGUI every frame while showing the settings screen.
    public static void Draw(System.Action onBack)
    {
        // Sync master volume live so the player can hear changes immediately.
        AudioListener.volume = GameSettings.MasterVolume;

        if (_inControls) { DrawControls(); return; }

        if (_showPopup)
        {
            // Draw the panel greyed-out behind the popup.
            GUI.enabled = false;
            DrawContent();
            DrawButtonRow(null);
            GUI.enabled = true;

            DrawUnsavedPopup(onBack);
            return;
        }

        DrawContent();
        DrawButtonRow(onBack);
    }

    // ── Settings content ──────────────────────────────────────────────────────

    static void DrawContent()
    {
        GUI.skin.button.fontSize = 20;
        GUI.skin.label.fontSize  = 20;

        var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };

        GUI.Label(new Rect(0, 30, Screen.width, 50), "<size=30><b>Settings</b></size>", centred);

        float x = (Screen.width - 480f) / 2f;
        float y = 100f;

        // ── Audio ─────────────────────────────────────────────────────────────

        SectionHeader("Audio", x, ref y);

        VolumeRow("Master",  ref GameSettings.MasterVolume,  x, ref y);
        VolumeRow("Effects", ref GameSettings.EffectsVolume, x, ref y);
        VolumeRow("Music",   ref GameSettings.MusicVolume,   x, ref y);
        VolumeRow("Voice",   ref GameSettings.VoiceVolume,   x, ref y);

        y += 8f;

        // ── Camera ────────────────────────────────────────────────────────────

        SectionHeader("Camera", x, ref y);

        // Mouse sensitivity — exposed as -1 … 0 … +1
        {
            float prev = GameSettings.MouseSensitivitySlider;
            LabelRow("Mouse Sensitivity",
                prev == 0f ? "Default" : $"{prev:+0.0;-0.0}",
                x, ref y);
            float next = GUI.HorizontalSlider(SliderRect(x, y), prev, -1f, 1f);
            if (Mathf.Abs(next) < 0.04f) next = 0f; // dead-zone so Default is easy to land
            if (!Mathf.Approximately(next, prev)) { GameSettings.MouseSensitivitySlider = next; _isDirty = true; }
            y += 30f;
        }

        y += 10f;

        // Field of view — shown in degrees
        {
            float prev = GameSettings.FieldOfView;
            string fovLabel = Mathf.Approximately(prev, GameSettings.DefaultFieldOfView)
                ? $"{prev:F0}° (Default)" : $"{prev:F0}°";
            LabelRow("Field of View", fovLabel, x, ref y);
            float next = Mathf.Round(GUI.HorizontalSlider(SliderRect(x, y), prev,
                GameSettings.MinFOV, GameSettings.MaxFOV));
            if (!Mathf.Approximately(next, prev)) { GameSettings.FieldOfView = next; _isDirty = true; }
            y += 30f;
        }

        y += 10f;

        // Head bob toggle
        {
            bool prev = GameSettings.HeadBobEnabled;
            bool next = GUI.Toggle(new Rect(x + 4f, y, 480f, 34f), prev, "  Head Bob");
            if (next != prev) { GameSettings.HeadBobEnabled = next; _isDirty = true; }
            y += 44f;
        }

        // ── Controls ────────────────────────────────────────────────────────────
        SectionHeader("Controls", x, ref y);
        if (GUI.Button(new Rect(x, y, 240f, 38f), "Configure Keybinds…")) _inControls = true;
        y += 44f;
    }

    // ── Controls / keybind sub-screen ─────────────────────────────────────────

    static void DrawControls()
    {
        var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
        GUI.Label(new Rect(0, 30, Screen.width, 50), "<size=30><b>Controls</b></size>", centred);

        var im = InputManager.Instance;
        float w = 560f;
        float x = (Screen.width - w) / 2f;
        float top = 100f;
        float listH = Screen.height - top - 110f;

        if (im == null || im.Rebindables == null)
        {
            GUI.Label(new Rect(x, top, w, 30f), "Input system not ready.");
        }
        else
        {
            const float rowH = 38f;
            var view    = new Rect(x, top, w, listH);
            var content = new Rect(0, 0, w - 20f, im.Rebindables.Count * rowH);
            _controlsScroll = GUI.BeginScrollView(view, _controlsScroll, content);

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var keyStyle  = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };

            for (int i = 0; i < im.Rebindables.Count; i++)
            {
                var r = im.Rebindables[i];
                float ry = i * rowH;
                GUI.Label(new Rect(0, ry, 220f, rowH), r.Label, nameStyle);

                bool listening = _rebindingEntry == r;
                if (listening)
                {
                    var hint = new GUIStyle(GUI.skin.label)
                        { fontSize = 14, alignment = TextAnchor.MiddleCenter,
                          normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
                    GUI.Label(new Rect(230f, ry, content.width - 240f, rowH), "Press a key…  (Esc to cancel)", hint);
                }
                else
                {
                    GUI.Label(new Rect(230f, ry, content.width - 350f, rowH), InputManager.DisplayFor(r), keyStyle);

                    GUI.enabled = !im.IsRebinding;
                    if (GUI.Button(new Rect(content.width - 100f, ry + 3f, 96f, rowH - 8f), "Rebind"))
                    {
                        _rebindingEntry = r;
                        im.StartRebind(r, _ => _rebindingEntry = null);
                    }
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }

        // Bottom buttons.
        const float BW = 170f, BH = 46f, GAP = 14f;
        float totalW = BW * 2 + GAP;
        float bx = (Screen.width - totalW) / 2f;
        float by = Screen.height - 80f;

        GUI.enabled = im == null || !im.IsRebinding;
        if (GUI.Button(new Rect(bx, by, BW, BH), "Reset to Default"))
            im?.ResetBindingsToDefault();
        if (GUI.Button(new Rect(bx + BW + GAP, by, BW, BH), "Back"))
            _inControls = false;
        GUI.enabled = true;
    }

    // ── Button row ────────────────────────────────────────────────────────────

    static void DrawButtonRow(System.Action onBack)
    {
        // Four equal buttons centred at the bottom, with a dirty indicator.
        const float BW = 120f, BH = 46f, GAP = 14f;
        float totalW = BW * 4 + GAP * 3;
        float bx = (Screen.width - totalW) / 2f;
        float by = Screen.height - 90f;

        if (GUI.Button(new Rect(bx,                   by, BW, BH), "Save"))
        {
            GameSettings.Save();
            _isDirty  = false;
            _snapshot = GameSettings.Capture(); // revert now targets the saved state
        }

        if (GUI.Button(new Rect(bx + (BW + GAP),      by, BW, BH), "Revert"))
        {
            GameSettings.Restore(_snapshot);
            _isDirty = false;
        }

        if (GUI.Button(new Rect(bx + (BW + GAP) * 2f, by, BW, BH), "Defaults"))
        {
            GameSettings.ResetToDefaults();
            _isDirty = true;
        }

        if (GUI.Button(new Rect(bx + (BW + GAP) * 3f, by, BW, BH), "Back"))
        {
            if (_isDirty) { _showPopup = true; _pendingBack = onBack; }
            else onBack?.Invoke();
        }

        // Small unsaved-changes hint above the buttons
        if (_isDirty)
        {
            var hint = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 0.8f, 0.3f) }
            };
            GUI.Label(new Rect(0, by - 26f, Screen.width, 22f), "Unsaved changes", hint);
        }
    }

    // ── Unsaved-changes popup ─────────────────────────────────────────────────

    static void DrawUnsavedPopup(System.Action onBack)
    {
        // Dark overlay
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Popup box
        const float PW = 420f, PH = 180f;
        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;
        GUI.Box(new Rect(px, py, PW, PH), "");

        var bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true,
            fontSize  = 18,
        };
        GUI.Label(new Rect(px + 20f, py + 20f, PW - 40f, 60f),
            "You have unsaved changes.\nSave before going back?", bodyStyle);

        float btnY  = py + PH - 60f;
        float btnX0 = px + (PW - (3 * 110f + 2 * 12f)) / 2f;

        if (GUI.Button(new Rect(btnX0,          btnY, 110f, 40f), "Save"))
        {
            GameSettings.Save();
            _isDirty   = false;
            _showPopup = false;
            (_pendingBack ?? onBack)?.Invoke();
            _pendingBack = null;
        }

        if (GUI.Button(new Rect(btnX0 + 122f,   btnY, 110f, 40f), "Not Save"))
        {
            GameSettings.Restore(_snapshot);
            _isDirty   = false;
            _showPopup = false;
            (_pendingBack ?? onBack)?.Invoke();
            _pendingBack = null;
        }

        if (GUI.Button(new Rect(btnX0 + 244f,   btnY, 110f, 40f), "Cancel"))
        {
            _showPopup   = false;
            _pendingBack = null;
        }
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    static void SectionHeader(string title, float x, ref float y)
    {
        var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 17 };
        GUI.Label(new Rect(x, y, 480f, 26f), title, style);
        y += 30f;
    }

    // Label on left, current value on right, leaves y at the slider row.
    static void LabelRow(string label, string value, float x, ref float y)
    {
        var small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        GUI.Label(new Rect(x, y, 300f, 24f), label);
        GUI.Label(new Rect(x + 310f, y, 170f, 24f), value, small);
        y += 24f;
    }

    // Volume row: label + percentage value + slider, all in one call.
    static void VolumeRow(string label, ref float value, float x, ref float y)
    {
        LabelRow(label, $"{value * 100f:F0} %", x, ref y);
        float next = GUI.HorizontalSlider(SliderRect(x, y), value, 0f, 1f);
        if (!Mathf.Approximately(next, value)) { value = next; _isDirty = true; }
        y += 30f;
    }

    static Rect SliderRect(float x, float y) => new Rect(x, y, 480f, 22f);
}
