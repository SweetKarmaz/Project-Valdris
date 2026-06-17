using UnityEngine;

// Lightweight transient on-screen toast for short messages ("Inventory is full",
// "Quest updated", etc.). Self-bootstraps into the Persistent layer — no scene
// setup needed. Call ScreenNotifier.Show("…") from anywhere.
//
// Uses IMGUI to match the project's other UI (GameUI, InteractableProp). The
// message fades out over its lifetime and stacks vertically if several arrive.
[DisallowMultipleComponent]
public class ScreenNotifier : MonoBehaviour
{
    public static ScreenNotifier Instance { get; private set; }

    const int   MaxMessages   = 4;
    const float DefaultSeconds = 2.5f;
    const float FadeSeconds    = 0.6f;

    struct Toast { public string text; public float dieAt; public float life; }
    readonly System.Collections.Generic.List<Toast> _toasts = new();

    bool _subscribedToInventory;

    // ── Bootstrap ───────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ScreenNotifier");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenNotifier>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (InventorySystem.Instance != null)
            InventorySystem.Instance.OnInventoryFull -= HandleInventoryFull;
        if (Instance == this) Instance = null;
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    public static void Show(string message, float seconds = DefaultSeconds)
    {
        if (Instance == null || string.IsNullOrEmpty(message)) return;
        Instance.Push(message, seconds);
    }

    void Push(string message, float seconds)
    {
        _toasts.Add(new Toast { text = message, dieAt = Time.unscaledTime + seconds, life = seconds });
        if (_toasts.Count > MaxMessages) _toasts.RemoveAt(0);
    }

    // ── Update / draw ─────────────────────────────────────────────────────────

    void Update()
    {
        // InventorySystem is a separate Persistent singleton that may initialise
        // after this one — subscribe as soon as it exists.
        if (!_subscribedToInventory && InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryFull += HandleInventoryFull;
            _subscribedToInventory = true;
        }

        for (int i = _toasts.Count - 1; i >= 0; i--)
            if (Time.unscaledTime >= _toasts[i].dieAt) _toasts.RemoveAt(i);
    }

    void HandleInventoryFull(LootItem _) => Push("Inventory is full", DefaultSeconds);

    void OnGUI()
    {
        if (_toasts.Count == 0) return;

        const float w = 360f, h = 34f, gap = 6f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = false,
        };

        for (int i = 0; i < _toasts.Count; i++)
        {
            var t = _toasts[i];
            float remaining = t.dieAt - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining / FadeSeconds);   // fade near the end

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha);
            GUI.DrawTexture(new Rect(x, y + i * (h + gap), w, h), Texture2D.whiteTexture);

            style.normal.textColor = new Color(1f, 0.85f, 0.4f, alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x, y + i * (h + gap), w, h), t.text, style);
            GUI.color = prev;
        }
    }
}
