using UnityEngine;
using System;
using System.Collections.Generic;

// A named area of the level, defined by a trigger BoxCollider (editor-only gizmo,
// invisible in-game). Beyond marking the player's location and feeding ReachZone
// quest objectives, a Zone can optionally, on entry: grant one-time XP, trigger a
// quest, set a world flag, auto-save; and while inside: act as a safe zone
// (suppresses new NPC aggro + boosts regen) and/or apply an aura buff.
[RequireComponent(typeof(BoxCollider))]
public class Zone : MonoBehaviour
{
    public string zoneId;

    [Header("On first entry (once, persisted)")]
    [Tooltip("XP granted the first time the player enters this zone.")]
    public int xpReward = 0;
    [Tooltip("Quest offered/accepted on entry (only if currently offerable).")]
    public QuestData questToTrigger;
    [Tooltip("Auto-save when the zone is first entered (checkpoint).")]
    public bool autoSaveCheckpoint = false;

    [Header("On entry (every time)")]
    public string setFlagOnEntry;
    public bool   setFlagValue = true;

    [Header("While inside")]
    [Tooltip("Suppresses new NPC aggro and boosts the player's regen while inside.")]
    public bool safeZone = false;
    [Tooltip("Buff applied while inside, removed on exit (blessing or hazard).")]
    public BuffData auraBuff;

    public static readonly List<Zone> All = new();
    public static string CurrentPlayerZone { get; private set; }
    public static event Action<string> OnPlayerEnteredZone;

    static int _safeCount;
    public static bool PlayerInSafeZone => _safeCount > 0;

    public Bounds Bounds { get; private set; }
    bool _playerInside;

    string EnteredKey => string.IsNullOrEmpty(zoneId) ? $"zone_{name}_entered" : $"zone_{zoneId}_entered";

    void Awake()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        Bounds = box.bounds;
    }

    void OnEnable() => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        if (_playerInside) HandleExit();   // safety: don't leave a safe-zone/aura dangling
        _playerInside = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_playerInside || !other.CompareTag("Player")) return;
        _playerInside = true;
        HandleEnter();
    }

    void OnTriggerExit(Collider other)
    {
        if (!_playerInside || !other.CompareTag("Player")) return;
        _playerInside = false;
        HandleExit();
    }

    void HandleEnter()
    {
        var ws = WorldStateSystem.Instance;

        // Named-zone change → announce + ReachZone objective event.
        if (!string.IsNullOrEmpty(zoneId) && CurrentPlayerZone != zoneId)
        {
            CurrentPlayerZone = zoneId;
            OnPlayerEnteredZone?.Invoke(zoneId);
            ZoneAnnouncer.Announce(zoneId);
        }

        if (!string.IsNullOrEmpty(setFlagOnEntry)) ws?.SetFlag(setFlagOnEntry, setFlagValue);

        // First-entry rewards (persisted so they never repeat).
        bool firstTime = ws == null || !ws.GetFlag(EnteredKey);
        if (firstTime)
        {
            ws?.SetFlag(EnteredKey, true);
            if (xpReward > 0) XPSystem.Instance?.AddXP(xpReward);
            if (autoSaveCheckpoint) SaveSystem.Instance?.Save();
        }

        if (questToTrigger != null && QuestSystem.Instance != null
            && QuestSystem.Instance.CanOffer(questToTrigger))
            QuestSystem.Instance.AcceptQuest(questToTrigger);

        // While-inside behaviours.
        if (safeZone) _safeCount++;
        if (auraBuff != null) PlayerBuffs()?.Apply(auraBuff);
    }

    void HandleExit()
    {
        if (safeZone) _safeCount = Mathf.Max(0, _safeCount - 1);
        if (auraBuff != null) PlayerBuffs()?.Remove(auraBuff);
    }

    static CharacterBuffs PlayerBuffs()
    {
        var p = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        return p != null ? p.GetComponent<CharacterBuffs>() : null;
    }

    public static Zone At(Vector3 point)
    {
        foreach (Zone zone in All)
            if (zone.Bounds.Contains(point)) return zone;
        return null;
    }

    public static List<Zone> WithId(string id)
    {
        var result = new List<Zone>();
        foreach (Zone zone in All)
            if (zone.zoneId == id) result.Add(zone);
        return result;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = safeZone ? new Color(0.3f, 0.8f, 1f, 0.12f) : new Color(0.3f, 1f, 0.5f, 0.10f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = safeZone ? new Color(0.3f, 0.8f, 1f, 0.8f) : new Color(0.3f, 1f, 0.5f, 0.7f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}

// Fades the zone name in at the top of the screen when the player crosses
// a zone boundary. Placeholder presentation, like the other IMGUI bits.
public class ZoneAnnouncer : MonoBehaviour
{
    private static ZoneAnnouncer _instance;
    private string _text;
    private float _showUntil;

    public static void Announce(string zoneName)
    {
        if (_instance == null)
        {
            var go = new GameObject("ZoneAnnouncer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ZoneAnnouncer>();
        }
        _instance._text = zoneName;
        _instance._showUntil = Time.unscaledTime + 3f;
    }

    private void OnGUI()
    {
        float remaining = _showUntil - Time.unscaledTime;
        if (remaining <= 0f || string.IsNullOrEmpty(_text)) return;
        float alpha = Mathf.Clamp01(remaining); // fade out over the last second
        GUI.color = new Color(1f, 0.95f, 0.8f, alpha);
        GUI.Label(new Rect(0, 40, Screen.width, 50), $"<size=30><b>{_text}</b></size>",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });
        GUI.color = Color.white;
    }
}
