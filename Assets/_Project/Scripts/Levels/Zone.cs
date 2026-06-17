using UnityEngine;
using System;
using System.Collections.Generic;

// A named area of the level, defined by a trigger BoxCollider. One zone id
// may span several Zone volumes (e.g. the mines = stair shaft + chamber).
// Used for player location ("Entered: Common Hall"), NPC wander limits,
// and later for area buffs/stealth/AI rules.
[RequireComponent(typeof(BoxCollider))]
public class Zone : MonoBehaviour
{
    public string zoneId;

    public static readonly List<Zone> All = new();
    public static string CurrentPlayerZone { get; private set; }
    public static event Action<string> OnPlayerEnteredZone;

    public Bounds Bounds { get; private set; }

    private void Awake()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        Bounds = box.bounds;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || CurrentPlayerZone == zoneId) return;
        CurrentPlayerZone = zoneId;
        OnPlayerEnteredZone?.Invoke(zoneId);
        ZoneAnnouncer.Announce(zoneId);
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
