using UnityEngine;

// Runs branching conversations from DialogueData and draws them as a bottom
// dialogue box (IMGUI, to match the rest of the in-game HUD). While a dialogue
// is active it holds a UIModal so the cursor is freed, the reticle stops
// targeting, and melee/cast input is suppressed.
//
// Choices can be gated by world flags and can set world flags on pick, which is
// how Greyspire's branching beats (befriended guard, prisoner spared, etc.) feed
// WorldStateSystem.
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    public bool IsActive { get; private set; }

    DialogueData   _data;
    int            _node = -1;
    string         _npcName;     // fallback speaker if data + node leave it blank
    bool           _modalHeld;
    bool           _forced;      // forced → Esc can't bail; only a Next=-1 exit ends it
    System.Action  _onComplete;  // invoked once the conversation closes

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null) return;
        new GameObject("DialogueSystem").AddComponent<DialogueSystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // npc is optional — used only to default the speaker name to the NPC.
    // forced: Esc can't bail; onComplete fires when the conversation closes;
    // faceTarget: snap the player's view toward this transform on open.
    public void StartDialogue(DialogueData data, NpcController npc = null,
                              bool forced = false, System.Action onComplete = null,
                              Transform faceTarget = null)
        => Begin(data, npc != null ? npc.DisplayName : null, forced, onComplete, faceTarget);

    // Player-inescapable conversation not tied to an on-screen NPC (e.g. a line
    // of dialogue overheard through a door, with a narrator + named voices).
    public void StartOverheard(DialogueData data, System.Action onComplete = null)
        => Begin(data, null, true, onComplete, null);

    void Begin(DialogueData data, string npcName, bool forced,
               System.Action onComplete, Transform faceTarget)
    {
        if (data == null || data.nodes == null || data.nodes.Length == 0) { onComplete?.Invoke(); return; }
        if (IsActive) EndDialogue();

        _data       = data;
        _npcName    = npcName;
        _forced     = forced;
        _onComplete = onComplete;
        _node       = data.ResolveEntryIndex(WorldStateSystem.Instance);
        if (_node < 0) { _data = null; _onComplete = null; onComplete?.Invoke(); return; }

        IsActive = true;
        if (!_modalHeld) { UIModal.Push(); _modalHeld = true; }
        if (faceTarget != null) FirstPersonCamera.Active?.FaceToward(faceTarget.position);
    }

    public void EndDialogue()
    {
        var done = _onComplete;
        _data       = null;
        _node       = -1;
        _forced     = false;
        _onComplete = null;
        IsActive    = false;
        if (_modalHeld) { UIModal.Pop(); _modalHeld = false; }
        done?.Invoke();   // after teardown, so a chained dialogue/event can start cleanly
    }

    void GoTo(int next)
    {
        if (next < 0 || _data == null || next >= _data.nodes.Length) { EndDialogue(); return; }
        _node = next;
    }

    void ApplyAndGo(DialogueData.Choice c)
    {
        if (!string.IsNullOrEmpty(c.setFlag))
        {
            WorldStateSystem.Instance?.SetFlag(c.setFlag, c.setValue);
        }
        GoTo(c.next);
    }

    bool ChoiceVisible(DialogueData.Choice c)
    {
        if (string.IsNullOrEmpty(c.requireFlag)) return true;
        bool v = WorldStateSystem.Instance != null && WorldStateSystem.Instance.GetFlag(c.requireFlag);
        return v == c.requireValue;
    }

    void Update()
    {
        if (!IsActive) return;
        // Esc / skip closes the conversation — unless it's forced (must click through).
        if (!_forced && InputManager.SkipPressed) EndDialogue();
    }

    // ── IMGUI ───────────────────────────────────────────────────────────────────
    GUIStyle _bodyStyle, _speakerStyle, _choiceStyle;

    void OnGUI()
    {
        if (!IsActive || _data == null) return;
        if (PauseMenuController.IsPaused) return;

        EnsureStyles();
        var node = _data.nodes[_node];

        float w  = Mathf.Min(820f, Screen.width - 80f);
        float x  = (Screen.width - w) * 0.5f;
        float boxH = 150f;
        float y  = Screen.height - boxH - 110f;

        // Backing panel.
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(x, y, w, boxH), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.4f, 0.7f);
        GUI.DrawTexture(new Rect(x, y, w, 2f), Texture2D.whiteTexture);            // top accent line
        GUI.color = Color.white;

        string speaker = !string.IsNullOrEmpty(node.speaker) ? node.speaker
                       : !string.IsNullOrEmpty(_data.defaultSpeaker) ? _data.defaultSpeaker
                       : !string.IsNullOrEmpty(_npcName) ? _npcName : "";
        GUI.Label(new Rect(x + 18f, y + 10f, w - 36f, 24f), speaker, _speakerStyle);
        GUI.Label(new Rect(x + 18f, y + 38f, w - 36f, boxH - 48f), node.text, _bodyStyle);

        // Choices (or Continue) stacked below the box.
        float cy = y + boxH + 10f;
        if (node.HasChoices)
        {
            int shown = 0;
            for (int i = 0; i < node.choices.Length; i++)
            {
                var c = node.choices[i];
                if (!ChoiceVisible(c)) continue;
                var r = new Rect(x, cy + shown * 34f, w, 30f);
                if (GUI.Button(r, $"{shown + 1}.  {c.text}", _choiceStyle)) { ApplyAndGo(c); return; }
                shown++;
            }
            if (shown == 0) GoTo(node.next);   // all choices gated out → fall through
        }
        else
        {
            var r = new Rect(x + w - 160f, cy, 160f, 30f);
            string label = node.next >= 0 ? "Continue  ▸" : "End  ▸";
            if (GUI.Button(r, label, _choiceStyle)) GoTo(node.next);
        }
    }

    void EnsureStyles()
    {
        if (_bodyStyle != null) return;
        _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = true };
        _bodyStyle.normal.textColor = new Color(0.92f, 0.92f, 0.9f);
        _speakerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        _speakerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
        _choiceStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, richText = true };
        _choiceStyle.padding = new RectOffset(14, 14, 6, 6);
    }
}
