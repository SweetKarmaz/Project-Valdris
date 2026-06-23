using System.Collections.Generic;
using UnityEngine;

// Modal popup shown when a quest is accepted ("New Quest") or finished
// ("Quest Complete"). While open it pauses the game, frees the cursor, blocks
// the tabbed menu, and is dismissed with the Close button or Esc (Esc does NOT
// open the pause menu). Popups queue, so several at once show one at a time.
//
// Driven by QuestSystem events, so it fires for both scripted GiveQuestStep
// accepts and auto-completing tutorial quests, with no per-call wiring.
public class QuestPopupSystem : MonoBehaviour
{
    public static QuestPopupSystem Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    static int _closedFrame = -1;
    // True on the same frame the popup closed — lets PauseMenuController ignore
    // the Esc that closed us instead of opening the pause menu.
    public static bool ClosedThisFrame => Time.frameCount == _closedFrame;

    enum Mode { Accepted, Complete }
    class Entry { public QuestData quest; public Mode mode; }

    readonly Queue<Entry> _queue = new();
    Entry   _current;
    Vector2 _scroll;
    LootItem _hoverItem;
    bool    _pausedByUs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null) return;
        new GameObject("QuestPopupSystem").AddComponent<QuestPopupSystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        QuestSystem.OnQuestAccepted += HandleAccepted;
        QuestSystem.OnQuestTurnedIn += HandleComplete;
    }

    void OnDisable()
    {
        QuestSystem.OnQuestAccepted -= HandleAccepted;
        QuestSystem.OnQuestTurnedIn -= HandleComplete;
    }

    void HandleAccepted(QuestData q) => Enqueue(q, Mode.Accepted);
    void HandleComplete(QuestData q) => Enqueue(q, Mode.Complete);

    void Enqueue(QuestData q, Mode mode)
    {
        if (q == null) return;
        _queue.Enqueue(new Entry { quest = q, mode = mode });
        if (_current == null) ShowNext();
    }

    void ShowNext()
    {
        if (_queue.Count == 0) { EndModal(); return; }
        _current = _queue.Dequeue();
        if (!IsOpen)
        {
            IsOpen = true;
            // UIModal frees the cursor AND tells FirstPersonCamera/PlayerCombat to
            // stand down (no look, no cursor re-lock, no click-through attack).
            UIModal.Push();
            // Pause the world too, unless the pause menu already owns the freeze.
            if (!PauseMenuController.IsPaused)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                _pausedByUs = true;
            }
        }
    }

    void Dismiss()
    {
        if (_queue.Count > 0) { _current = _queue.Dequeue(); return; }
        _current = null;
        EndModal();
    }

    void EndModal()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _closedFrame = Time.frameCount;
        if (_pausedByUs)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            _pausedByUs = false;
        }
        UIModal.Pop();   // restores the cursor (unless another modal/pause still holds it)
    }

    void Update()
    {
        if (IsOpen && InputManager.SkipPressed) Dismiss();   // Esc closes the popup
    }

    // ── Drawing ────────────────────────────────────────────────────────────────
    GUIStyle _heading, _title, _body, _section, _rewardLine;

    void OnGUI()
    {
        if (!IsOpen || _current == null) return;
        EnsureStyles();
        _hoverItem = null;

        var q = _current.quest;
        bool complete = _current.mode == Mode.Complete;

        float w = 520f;
        float x = (Screen.width - w) * 0.5f;
        float h = 420f;
        float y = (Screen.height - h) * 0.5f;

        // Dim + panel.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.07f, 0.07f, 0.08f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = complete ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(0.5f, 0.8f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float pad = 22f;
        float cx = x + pad, cw = w - pad * 2f;
        float cy = y + 16f;

        GUI.Label(new Rect(cx, cy, cw, 30f), complete ? "Quest Complete" : "New Quest", _heading);
        cy += 34f;
        GUI.Label(new Rect(cx, cy, cw, 26f), q.title, _title);
        cy += 30f;

        // Body: completion flavour (complete) or the quest description (accept).
        string body = complete && !string.IsNullOrEmpty(q.completionText) ? q.completionText : q.description;
        if (!string.IsNullOrEmpty(body))
        {
            float bh = _body.CalcHeight(new GUIContent(body), cw);
            GUI.Label(new Rect(cx, cy, cw, bh), body, _body);
            cy += bh + 10f;
        }

        // Scrollable detail area (objectives or rewards).
        float areaTop = cy;
        float areaH = (y + h - 58f) - areaTop;
        var viewport = new Rect(cx, areaTop, cw, areaH);
        var content = new Rect(0, 0, cw - 18f, Mathf.Max(areaH, complete ? RewardsHeight(q) : ObjectivesHeight(q)));
        _scroll = GUI.BeginScrollView(viewport, _scroll, content);
        if (complete) DrawRewards(q, content.width);
        else          DrawObjectives(q, content.width);
        GUI.EndScrollView();

        // Close button.
        float bw = 140f, bh2 = 34f;
        if (GUI.Button(new Rect(x + (w - bw) * 0.5f, y + h - bh2 - 14f, bw, bh2), "Close"))
            Dismiss();

        // Hover tooltip for a reward item (drawn last, on top).
        if (_hoverItem != null) ItemTooltip.Draw(_hoverItem, Event.current.mousePosition);
    }

    // ── Accept: objective checklist ──────────────────────────────────────────────
    float ObjectivesHeight(QuestData q) =>
        28f + (q.objectives != null ? q.objectives.Count : 0) * 24f;

    void DrawObjectives(QuestData q, float w)
    {
        float iy = 0f;
        GUI.Label(new Rect(0, iy, w, 22f), "Objectives", _section);
        iy += 26f;
        if (q.objectives != null)
            foreach (var obj in q.objectives)
            {
                GUI.Label(new Rect(12f, iy, w - 12f, 22f), $"• {obj.description}", _rewardLine);
                iy += 24f;
            }
    }

    // ── Complete: rewards ────────────────────────────────────────────────────────
    float RewardsHeight(QuestData q)
    {
        int lines = 0;
        if (q.xpReward > 0) lines++;
        if (q.goldReward > 0) lines++;
        if (q.statPointReward > 0) lines++;
        if (q.spellRewards != null) lines += q.spellRewards.Count;
        int itemRows = q.itemRewards != null
            ? Mathf.CeilToInt(CountNonNull(q.itemRewards) / 3f) : 0;
        return 28f + lines * 24f + itemRows * 34f + 8f;
    }

    void DrawRewards(QuestData q, float w)
    {
        float iy = 0f;
        GUI.Label(new Rect(0, iy, w, 22f), "Rewards", _section);
        iy += 26f;

        if (q.xpReward > 0)        { GUI.Label(new Rect(12f, iy, w - 12f, 22f), $"+{q.xpReward} XP", _rewardLine); iy += 24f; }
        if (q.goldReward > 0)      { GUI.Label(new Rect(12f, iy, w - 12f, 22f), $"+{q.goldReward} Gold", _rewardLine); iy += 24f; }
        if (q.statPointReward > 0) { GUI.Label(new Rect(12f, iy, w - 12f, 22f), $"+{q.statPointReward} Attribute Point(s)", _rewardLine); iy += 24f; }
        if (q.spellRewards != null)
            foreach (var s in q.spellRewards)
                if (s != null) { GUI.Label(new Rect(12f, iy, w - 12f, 22f), $"Spell: {s.name}", _rewardLine); iy += 24f; }

        // Item rewards as rarity-coloured slots, 3 per row, hover for the tooltip.
        if (q.itemRewards != null)
        {
            const float sw = 150f, sh = 30f, gap = 6f;
            int col = 0; float rowY = iy;
            foreach (var item in q.itemRewards)
            {
                if (item == null) continue;
                var r = new Rect(12f + col * (sw + gap), rowY, sw, sh);
                GUI.color = RarityTint(item.rarity);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                var nameStyle = new GUIStyle(_rewardLine) { alignment = TextAnchor.MiddleLeft };
                nameStyle.normal.textColor = RarityColor(item.rarity);
                GUI.Label(new Rect(r.x + 6f, r.y, r.width - 8f, r.height), item.ItemName, nameStyle);
                if (r.Contains(Event.current.mousePosition)) _hoverItem = item;

                if (++col >= 3) { col = 0; rowY += sh + gap; }
            }
        }
    }

    static int CountNonNull(List<LootItem> items)
    {
        int n = 0;
        foreach (var i in items) if (i != null) n++;
        return n;
    }

    static Color RarityColor(ItemRarity r) => r switch
    {
        ItemRarity.Uncommon  => new Color(0.4f, 1f, 0.4f),
        ItemRarity.Rare      => new Color(0.4f, 0.6f, 1f),
        ItemRarity.Epic      => new Color(0.75f, 0.4f, 1f),
        ItemRarity.Legendary => new Color(1f, 0.65f, 0.2f),
        _                    => Color.white,
    };

    static Color RarityTint(ItemRarity r)
    {
        Color c = RarityColor(r);
        return new Color(c.r, c.g, c.b, 0.16f);
    }

    void EnsureStyles()
    {
        if (_heading != null) return;
        _heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _heading.normal.textColor = new Color(1f, 0.9f, 0.6f);
        _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Italic };
        _body.normal.textColor = new Color(0.85f, 0.85f, 0.82f);
        _section = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _section.normal.textColor = new Color(1f, 0.85f, 0.4f);
        _rewardLine = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        _rewardLine.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
    }
}
