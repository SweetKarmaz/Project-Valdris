using UnityEngine;
using UnityEngine.SceneManagement;

// Owns high-level game flow: new game, continue, load, quit.
// Lives in the Persistent scene; UI controllers call into it.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string MainMenuScene = "MainMenu";
    public const string IntroScene = "IntroCinematic";
    public const string FirstGameplayScene = "Act1_Part1_Greyspire";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Dev conveniences while testing; remove or gate for release.
        if (PauseMenuController.IsPaused) return;
        if (InputManager.QuickSavePressed) SaveSystem.Instance?.Save();
        if (InputManager.QuickLoadPressed)
        {
            SaveSummary latest = SaveSystem.Instance?.GetLatestSave();
            if (latest != null) SaveSystem.Instance.LoadGame(latest.fileName);
        }
    }

    public void StartNewGame()
    {
        SceneStateManager.DeleteAllSceneStates();
        SceneManager.LoadScene(IntroScene);
    }

    // Called by the intro cinematic when it finishes or is skipped.
    public void BeginAct1() => SceneManager.LoadScene(FirstGameplayScene);

    public void ContinueGame()
    {
        SaveSummary latest = SaveSystem.Instance?.GetLatestSave();
        if (latest != null) SaveSystem.Instance.LoadGame(latest.fileName);
        else Debug.LogWarning("Continue pressed with no saves.");
    }

    public void LoadGame(string fileName) => SaveSystem.Instance?.LoadGame(fileName);

    public void ReturnToMainMenu()
    {
        // Disable NPCs and suppress auto-save BEFORE loading so the scene JSON
        // is never written during an unload (partial snapshots corrupt the data).
        SceneStateManager.PrepareForExit();
        SceneManager.LoadScene(MainMenuScene);
    }

    public void QuitGame()
    {
        SceneStateManager.PrepareForExit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Also fires when the user presses Stop in the Unity Editor, ensuring the
    // editor Play-mode exit path never triggers an auto-save.
    void OnApplicationQuit() => SaveSystem.SuppressSceneAutoSave = true;
}
