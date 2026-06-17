using UnityEngine.SceneManagement;

// Ensures the Persistent scene's systems exist. Scenes that can be entered
// directly (MainMenu, or any scene during editor testing) call this first.
// The systems mark themselves DontDestroyOnLoad, so this only ever loads
// the Persistent scene once per run.
public static class GameBootstrap
{
    public static void EnsureSystems()
    {
        if (GameManager.Instance != null) return; // already booted
        SceneManager.LoadScene("Persistent", LoadSceneMode.Additive);
    }
}
