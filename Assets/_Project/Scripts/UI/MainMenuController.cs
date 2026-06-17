using UnityEngine;
using System.Collections.Generic;

// Placeholder main menu using immediate-mode GUI so it works with zero
// canvas setup. Swap for a proper uGUI canvas during the UI art pass —
// all actions just call GameManager, so only the visuals will change.
public class MainMenuController : MonoBehaviour
{
    private enum Screen { Main, LoadGame, Settings }

    private Screen _screen = Screen.Main;
    private List<SaveSummary> _saves;
    private string _confirmingDelete; // fileName awaiting delete confirmation
    private Vector2 _scroll;

    private void Awake()
    {
        GameBootstrap.EnsureSystems();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        // Solid background — covers any scene geometry or transition remnants
        // visible through a camera that has not yet cleared its buffer.
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.skin.button.fontSize = 20;
        GUI.skin.label.fontSize = 20;

        switch (_screen)
        {
            case Screen.Main: DrawMain(); break;
            case Screen.LoadGame: DrawLoadGame(); break;
            case Screen.Settings: DrawSettings(); break;
        }
    }

    private Rect Centered(int row, float width = 300f, float height = 50f) =>
        new Rect((UnityEngine.Screen.width - width) / 2f, 180f + row * 64f, width, height);

    private void DrawMain()
    {
        GUI.Label(new Rect(0, 80, UnityEngine.Screen.width, 60),
            "<size=42><b>PROJECT VALDRIS</b></size>",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });

        bool hasSaves = SaveSystem.Instance != null && SaveSystem.Instance.HasAnySave;

        if (GUI.Button(Centered(0), "New Game"))
            GameManager.Instance?.StartNewGame();

        GUI.enabled = hasSaves;
        if (GUI.Button(Centered(1), "Continue"))
            GameManager.Instance?.ContinueGame();
        if (GUI.Button(Centered(2), "Load"))
        {
            _saves = SaveSystem.Instance.ListSaves();
            _confirmingDelete = null;
            _screen = Screen.LoadGame;
        }
        GUI.enabled = true;

        if (GUI.Button(Centered(3), "Settings"))
            { _screen = Screen.Settings; SettingsPanel.Open(); }

        if (GUI.Button(Centered(4), "Quit"))
            GameManager.Instance?.QuitGame();
    }

    private void DrawLoadGame()
    {
        GUI.Label(new Rect(0, 60, UnityEngine.Screen.width, 50),
            "<size=32><b>Load Game</b></size>",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });

        float listWidth = 620f;
        float x = (UnityEngine.Screen.width - listWidth) / 2f;
        Rect viewport = new Rect(x, 130, listWidth, UnityEngine.Screen.height - 240);
        Rect content = new Rect(0, 0, listWidth - 20, Mathf.Max(_saves.Count * 70f, viewport.height));
        _scroll = GUI.BeginScrollView(viewport, _scroll, content);

        for (int i = 0; i < _saves.Count; i++)
        {
            SaveSummary save = _saves[i];
            float y = i * 70f;
            GUI.Box(new Rect(0, y, content.width, 62), "");
            GUI.Label(new Rect(12, y + 6, content.width - 250, 26),
                $"{save.sceneName}  —  Level {save.playerLevel}");
            GUI.Label(new Rect(12, y + 32, content.width - 250, 24),
                $"<size=14>{save.savedAtUtc.ToLocalTime():g}</size>",
                new GUIStyle(GUI.skin.label) { richText = true });

            if (GUI.Button(new Rect(content.width - 230, y + 12, 100, 38), "Load"))
            {
                GameManager.Instance?.LoadGame(save.fileName);
            }

            if (_confirmingDelete == save.fileName)
            {
                if (GUI.Button(new Rect(content.width - 120, y + 12, 110, 38), "Confirm?"))
                {
                    SaveSystem.Instance.DeleteSave(save.fileName);
                    _saves = SaveSystem.Instance.ListSaves();
                    _confirmingDelete = null;
                }
            }
            else if (GUI.Button(new Rect(content.width - 120, y + 12, 110, 38), "Delete"))
            {
                _confirmingDelete = save.fileName; // second click confirms
            }
        }
        GUI.EndScrollView();

        if (_saves.Count == 0)
            GUI.Label(new Rect(0, 200, UnityEngine.Screen.width, 40), "No saved games.",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

        if (GUI.Button(new Rect(x, UnityEngine.Screen.height - 90, 150, 46), "Back"))
            _screen = Screen.Main;
    }

    private void DrawSettings() =>
        SettingsPanel.Draw(() => _screen = Screen.Main);
}
