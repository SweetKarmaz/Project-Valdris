using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// IMGUI HUD overlay drawn on top of everything else in the game view.
// Handles:
//   · Player health + mana bars (bottom-left, always visible during gameplay)
//   · NPC health bars floating above their heads when in combat
//   · Death screen ("YOU ARE DEAD") with Load / Main Menu options
//
// Attach to a DontDestroyOnLoad object in the Persistent scene (or let
// PlayerManager find it via Instance). PlayerStats pushes health/mana
// updates; everything else is polled each OnGUI frame.
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    // ── Bar layout ────────────────────────────────────────────────────────────

    // Health bar is double the old size; mana bar is 1.5×. Both anchored top-left.
    const float HpBarW    = 520f;
    const float HpBarH    = 36f;
    const float MpBarW    = 390f;
    const float MpBarH    = 27f;
    const float BarPad    = 8f;   // vertical gap between bars
    const float BarMargin = 24f;  // distance from screen edge

    // ── Cached values (pushed by PlayerStats) ────────────────────────────────

    float _hp = 1f, _hpMax = 1f;
    float _mp = 1f, _mpMax = 1f;

    // ── Death screen state ────────────────────────────────────────────────────

    bool _dead;
    public bool IsDead => _dead;
    bool _showLoadList;
    List<SaveSummary> _saves;
    Vector2 _loadScroll;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // Any scene load (Load Game or Main Menu) clears the death state so the
    // "YOU ARE DEAD" overlay never lingers into the next scene.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Reset();

    // ── API called by PlayerStats ─────────────────────────────────────────────

    public void UpdateHealth(float current, float max)
    {
        _hp = current; _hpMax = Mathf.Max(1f, max);
        if (current <= 0f && !_dead) TriggerDeath();
    }

    public void UpdateMana(float current, float max)
    {
        _mp = current; _mpMax = Mathf.Max(1f, max);
    }

    float _corruption;
    public void UpdateCorruption(float percent01) => _corruption = Mathf.Clamp01(percent01);

    // ── Death ─────────────────────────────────────────────────────────────────

    void TriggerDeath()
    {
        _dead = true;
        _showLoadList = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void Reset()
    {
        _dead          = false;
        _showLoadList  = false;
        Time.timeScale = 1f;
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (_dead)          { DrawDeathScreen(); return; }
        if (GameUI.IsOpen)  return;  // inventory / character tabs cover the screen
        if (!IsGameplayScene) return; // no HUD in the main menu / intro

        DrawPlayerBars();
        DrawNpcBars();
    }

    // True only in actual gameplay scenes — hides the HUD in the main menu and intro.
    static bool IsGameplayScene
    {
        get
        {
            string s = SceneManager.GetActiveScene().name;
            return s != GameManager.MainMenuScene && s != GameManager.IntroScene;
        }
    }

    // ── Player health / mana bars ─────────────────────────────────────────────

    void DrawPlayerBars()
    {
        float sx = BarMargin;
        // Stack from the top: health on top, mana below, corruption below that.
        float hpY = BarMargin;
        float mpY = hpY + HpBarH + BarPad;

        DrawBar(sx, hpY, HpBarW, HpBarH, _hp / _hpMax, new Color(0.80f, 0.12f, 0.12f, 0.92f), "HP");
        DrawBar(sx, mpY, MpBarW, MpBarH, _mp / _mpMax, new Color(0.15f, 0.35f, 0.85f, 0.92f), "MP");
        if (_corruption > 0f)
        {
            float corY = mpY + MpBarH + BarPad;
            DrawBar(sx, corY, MpBarW, MpBarH, _corruption, new Color(0.45f, 0.10f, 0.65f, 0.92f), "Corruption");
        }
    }

    static void DrawBar(float x, float y, float w, float h, float fill,
                        Color barColor, string label)
    {
        fill = Mathf.Clamp01(fill);

        // Dark background.
        GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // Filled portion.
        GUI.color = barColor;
        GUI.DrawTexture(new Rect(x, y, w * fill, h), Texture2D.whiteTexture);

        // Thin border.
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        DrawBorderStatic(new Rect(x, y, w, h), 1f);

        GUI.color = Color.white;

        // Percentage label centred in the bar (font scales with bar height).
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = Mathf.Clamp(Mathf.RoundToInt(h * 0.55f), 10, 22),
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, w, h), $"{label}  {fill * 100f:0}%", style);
    }

    // ── NPC health bars ───────────────────────────────────────────────────────

    void DrawNpcBars()
    {
        var cam = Camera.main;
        if (cam == null) return;

        foreach (var npc in NpcController.All)
        {
            if (npc == null || npc.IsDead) continue;
            if (!npc.IsInCombat)           continue;

            float fill = npc.MaxHealth > 0f
                ? Mathf.Clamp01(npc.CurrentHealth / npc.MaxHealth)
                : 0f;

            // World-space position above the NPC's head.
            Vector3 headWorld = npc.transform.position + Vector3.up * 2.4f;
            Vector3 screenPos = cam.WorldToScreenPoint(headWorld);

            // Skip if behind the camera.
            if (screenPos.z < 0f) continue;

            // IMGUI Y is flipped relative to screen-space Y.
            float sx = screenPos.x - 50f;
            float sy = Screen.height - screenPos.y - 8f;

            DrawBar(sx, sy, 100f, 10f, fill, new Color(0.80f, 0.10f, 0.10f, 0.88f), "");

            // NPC name in small text above the bar.
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 10,
            };
            nameStyle.normal.textColor = new Color(1f, 0.85f, 0.75f, 0.9f);
            GUI.Label(new Rect(sx - 10f, sy - 14f, 120f, 14f), npc.DisplayName, nameStyle);
        }
    }

    // ── Death screen ──────────────────────────────────────────────────────────

    void DrawDeathScreen()
    {
        // Full-screen solid black overlay (hides the game world entirely).
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;

        if (!_showLoadList)
        {
            // "YOU ARE DEAD" — non-interactable; force the red color in every
            // style state so it never tints on mouse-over.
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 52,
                fontStyle = FontStyle.Bold,
            };
            SetAllTextColors(titleStyle, new Color(0.85f, 0.08f, 0.08f, 1f));
            GUI.Label(new Rect(cx - 300f, cy - 120f, 600f, 80f), "YOU ARE DEAD", titleStyle);

            DrawDeathButtons(cx, cy);
        }
        else
        {
            DrawLoadList(cx, cy);
        }
    }

    void DrawDeathButtons(float cx, float cy)
    {
        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        float bw = 220f, bh = 52f, gap = 18f;
        float by = cy - 10f;

        if (GUI.Button(new Rect(cx - bw * 0.5f, by, bw, bh), "Load Game", btnStyle))
        {
            _saves = SaveSystem.Instance?.ListSaves();
            _showLoadList = true;
        }

        if (GUI.Button(new Rect(cx - bw * 0.5f, by + bh + gap, bw, bh), "Main Menu", btnStyle))
        {
            // Restore timescale first so the scene load isn't frozen,
            // but keep _dead = true so this screen stays up until the
            // new scene destroys us. PrepareForExit + LoadScene are called
            // directly so there's no null-silence risk.
            Time.timeScale = 1f;
            SceneStateManager.PrepareForExit();
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameManager.MainMenuScene);
        }
    }

    void DrawLoadList(float cx, float cy)
    {
        var btnStyle  = new GUIStyle(GUI.skin.button)  { fontSize = 16 };
        var lblStyle  = new GUIStyle(GUI.skin.label)   { fontSize = 14 };
        var titleStyle = new GUIStyle(GUI.skin.label)  { fontSize = 20, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Color.white;

        // Solid panel behind the list so nothing shows through.
        Rect panel = new Rect(cx - 230f, cy - 175f, 460f, 360f);
        FillSolid(panel, new Color(0.04f, 0.04f, 0.04f, 1f));
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        DrawBorderStatic(panel, 1f);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx - 200f, cy - 160f, 400f, 30f), "Choose a Save", titleStyle);

        Rect viewport = new Rect(cx - 200f, cy - 120f, 400f, 220f);
        float entryH  = 58f;
        float contentH = Mathf.Max((_saves?.Count ?? 0) * entryH, viewport.height);
        _loadScroll = GUI.BeginScrollView(viewport, _loadScroll,
                                          new Rect(0, 0, 380f, contentH));

        if (_saves == null || _saves.Count == 0)
        {
            lblStyle.normal.textColor = Color.grey;
            GUI.Label(new Rect(10f, 10f, 360f, 30f), "No saved games found.", lblStyle);
        }
        else
        {
            for (int i = 0; i < _saves.Count; i++)
            {
                var s = _saves[i];
                float ey = i * entryH;
                GUI.Box(new Rect(0, ey, 378f, entryH - 4f), "");
                lblStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(10f, ey + 4f, 260f, 22f),
                          $"{s.sceneName}  –  Level {s.playerLevel}", lblStyle);
                GUI.Label(new Rect(10f, ey + 26f, 260f, 18f),
                          $"<size=12>{s.savedAtUtc.ToLocalTime():g}</size>",
                          new GUIStyle(lblStyle) { richText = true });

                if (GUI.Button(new Rect(285f, ey + 12f, 85f, 32f), "Load", btnStyle))
                {
                    var save = SaveSystem.Instance;
                    if (save != null)
                    {
                        // Restore timescale before the load — SceneManager and
                        // coroutines won't run at timeScale 0. Keep _dead = true
                        // so this screen stays up until the new scene is loaded.
                        Time.timeScale = 1f;
                        save.LoadGame(s.fileName);
                    }
                    else
                    {
                        Debug.LogError("[HUDController] SaveSystem.Instance is null — cannot load game.");
                    }
                }
            }
        }

        GUI.EndScrollView();

        if (GUI.Button(new Rect(cx - 60f, cy + 115f, 120f, 36f), "Back", btnStyle))
            _showLoadList = false;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    // Fills a rect with an opaque color (restores GUI.color afterwards).
    static void FillSolid(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // Forces a single text color across every interactive state so a Label
    // never tints on hover / focus / active.
    static void SetAllTextColors(GUIStyle s, Color c)
    {
        s.normal.textColor   = c;
        s.hover.textColor    = c;
        s.active.textColor   = c;
        s.focused.textColor  = c;
        s.onNormal.textColor = c;
        s.onHover.textColor  = c;
        s.onActive.textColor = c;
    }

    static void DrawBorderStatic(Rect r, float t)
    {
        GUI.DrawTexture(new Rect(r.x,             r.y,              r.width, t),       Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,             r.y + r.height - t, r.width, t),    Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,             r.y,              t, r.height),      Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + r.width - t, r.y,           t, r.height),      Texture2D.whiteTexture);
    }
}
