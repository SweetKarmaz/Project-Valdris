using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Central scene-transition + loading-screen controller. Persistent singleton.
//
// Gateways call into it to either:
//   • LoadScene  — load another scene and place the player at a named SpawnPoint
//                  there, showing a full-screen "Loading …" overlay meanwhile.
//   • Teleport   — move the player to a SpawnPoint in the current scene.
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    // Set just before a scene load; consumed by PlayerManager.InitializeForScene
    // once the destination scene's SpawnPoints exist. Cleared after use.
    public static string PendingSpawnId;

    // Raised by SceneGameManager once a freshly-loaded scene has finished its
    // OnSceneReady() build step (geometry generation, NPC spawning), so the
    // loading overlay can come down only when the scene is actually playable.
    static bool _sceneReady;
    public static void NotifySceneReady() => _sceneReady = true;

    bool   _loading;
    string _loadingLabel = "Loading…";

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("SceneTransition");
        go.AddComponent<SceneTransition>();   // Awake sets Instance + DontDestroyOnLoad
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API (called by Gateway) ──────────────────────────────────────────

    public void LoadScene(string sceneName, string spawnId, string displayName)
    {
        if (_loading) return;
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneTransition] LoadScene called with no scene name.");
            return;
        }
        StartCoroutine(LoadRoutine(sceneName, spawnId, displayName));
    }

    public void TeleportInScene(string spawnId)
    {
        var sp = SpawnPoint.FindById(spawnId);
        if (sp == null)
        {
            Debug.LogWarning($"[SceneTransition] SpawnPoint '{spawnId}' not found in the current scene.");
            return;
        }
        PlayerManager.Instance?.RepositionPlayer(sp.SpawnPosition, sp.SpawnRotation);
    }

    // ── Load coroutine ──────────────────────────────────────────────────────────

    IEnumerator LoadRoutine(string sceneName, string spawnId, string displayName)
    {
        _loading       = true;
        _loadingLabel  = string.IsNullOrEmpty(displayName) ? "Loading…" : $"Loading {displayName}…";
        PendingSpawnId = spawnId;
        _sceneReady    = false;

        // Persist the current scene's state before it unloads (loot containers,
        // NPC positions, etc.) just like the save system does on exit.
        SceneStateManager.Instance?.SaveState();

        // The explicit save above is the authoritative snapshot. Suppress the
        // per-scene auto-save that SceneStateManager.OnDestroy would otherwise
        // fire while the old scene unloads — during a Single-mode unload objects
        // are destroyed in undefined order, so that auto-save could overwrite the
        // clean snapshot with a partial one (missing already-destroyed NPCs).
        SaveSystem.SuppressSceneAutoSave = true;

        Time.timeScale = 1f;   // a paused game must not freeze the load

        yield return null;     // let the overlay paint one frame before the hitch

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (op != null && !op.isDone) yield return null;

        // The new scene's SceneStateManager.Awake has already read its JSON; it's
        // safe to auto-save again for future gameplay/transitions.
        SaveSystem.SuppressSceneAutoSave = false;

        // Hold the overlay until the new scene's SceneGameManager reports it has
        // finished building, with a safety timeout in case a scene never signals.
        float t = 0f;
        while (!_sceneReady && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }

        PendingSpawnId = null;
        _loading = false;
    }

    // ── Loading overlay ─────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!_loading) return;

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 34,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = new Color(0.90f, 0.85f, 0.70f);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), _loadingLabel, style);
    }
}
