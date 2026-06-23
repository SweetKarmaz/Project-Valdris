using UnityEngine;
using UnityEngine.SceneManagement;

// Esc pauses gameplay: time freezes, cursor frees, menu appears.
// Lives in the Persistent scene; inert while in the main menu or intro.
// Placeholder IMGUI visuals, same as the main menu — swap in the UI pass.
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    private bool _inSettings;
    private bool _showQuitPopup;
    private bool _transitioning;
    private float _savedFlashUntil;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!InGameplayScene())
        {
            // Reset gameplay state without touching cursor — the menu scene
            // (MainMenuController.Awake) owns cursor setup there.
            if (IsPaused)
            {
                IsPaused            = false;
                Time.timeScale      = 1f;
                AudioListener.pause = false;
            }
            _transitioning = false; // main menu draws its own solid background
            return;
        }
        // A quest popup owns Esc (it closes itself); never let Esc open the pause
        // menu while one is open or on the frame it just closed.
        if (QuestPopupSystem.IsOpen || QuestPopupSystem.ClosedThisFrame) return;

        if (InputManager.SkipPressed && !GameUI.EscConsumedByGameUI)
        {
            if (IsPaused) Resume();
            else if (!GameUI.IsOpen) Pause();
        }
    }

    private static bool InGameplayScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene != GameManager.MainMenuScene && scene != GameManager.IntroScene && scene != "Persistent";
    }

    private void Pause()
    {
        IsPaused = true;
        _inSettings   = false;
        _showQuitPopup = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Resume()
    {
        IsPaused      = false;
        _transitioning = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnGUI()
    {
        if (_transitioning)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            return;
        }

        if (!IsPaused) return;

        GUI.skin.button.fontSize = 20;
        GUI.skin.label.fontSize = 20;

        // Dim the game behind the menu
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (_inSettings) { DrawSettings(); return; }

        GUI.Label(new Rect(0, 120, Screen.width, 50), "<size=34><b>Paused</b></size>",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });

        if (GUI.Button(Row(0), "Resume")) Resume();

        if (GUI.Button(Row(1), "Save Game"))
        {
            SaveSystem.Instance?.Save();
            _savedFlashUntil = Time.unscaledTime + 1.5f;
        }

        if (GUI.Button(Row(2), "Settings")) { _inSettings = true; SettingsPanel.Open(); }

        if (GUI.Button(Row(3), "Quit to Menu"))
        {
            _transitioning = true;
            GameManager.Instance?.ReturnToMainMenu();
        }

        if (GUI.Button(Row(4), "Quit to Windows"))
            _showQuitPopup = true;

        if (Time.unscaledTime < _savedFlashUntil)
            GUI.Label(new Rect(0, Row(5).y, Screen.width, 40), "Game saved.",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

        if (_showQuitPopup)
            DrawQuitPopup();
    }

    private void DrawQuitPopup()
    {
        // Dim overlay
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        const float PW = 440f, PH = 190f;
        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;
        GUI.Box(new Rect(px, py, PW, PH), "");

        var bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true,
            fontSize  = 17,
        };
        GUI.Label(new Rect(px + 20f, py + 20f, PW - 40f, 80f),
            "Make sure you have saved your game before quitting — any unsaved progress will be lost.",
            bodyStyle);

        float btnY = py + PH - 58f;
        float btnX = px + (PW - (2 * 110f + 16f)) / 2f;

        if (GUI.Button(new Rect(btnX,         btnY, 110f, 42f), "OK"))
            GameManager.Instance?.QuitGame();

        if (GUI.Button(new Rect(btnX + 126f,  btnY, 110f, 42f), "Cancel"))
            _showQuitPopup = false;
    }

    private static Rect Row(int row) =>
        new Rect((Screen.width - 300f) / 2f, 200f + row * 64f, 300f, 50f);

    private void DrawSettings() =>
        SettingsPanel.Draw(() => _inSettings = false);
}
