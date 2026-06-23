using System.Collections;
using UnityEngine;

// Trigger + gate + sequence. Fires its ScriptedSequence once the trigger fires
// and the flag gate passes. "Once" events remember they ran via a WorldState
// flag, which persists in saves — so they don't re-fire after reload.
//
// Triggers:
//   ProximityRadius — player comes within `radius` of this object.
//   TriggerVolume   — player enters a trigger collider on this object.
//   DoorOpened      — `watchedDoor` becomes open.
//   Manual          — only fires when another step/script calls Fire().
public class ScriptedEvent : MonoBehaviour
{
    public enum TriggerKind { ProximityRadius, TriggerVolume, DoorOpened, Manual }

    [Header("Trigger")]
    public TriggerKind trigger = TriggerKind.ProximityRadius;
    [Tooltip("ProximityRadius: fire when the player is within this distance.")]
    public float radius = 3f;
    [Tooltip("DoorOpened: fire when this door opens.")]
    public DoorController watchedDoor;

    [Header("Gate")]
    [Tooltip("All of these world flags must be SET for the event to fire.")]
    public string[] requiredFlags;
    [Tooltip("None of these world flags may be set for the event to fire.")]
    public string[] blockedFlags;

    [Header("Repeat")]
    public bool once = true;
    [Tooltip("World flag remembering this event ran (persists in saves). Blank = auto from object name.")]
    public string completionFlag;

    [Header("What runs")]
    public ScriptedSequence sequence;

    bool _fired;

    string Key => string.IsNullOrEmpty(completionFlag) ? $"event_{gameObject.name}" : completionFlag;

    void Awake()
    {
        if (sequence == null) sequence = GetComponent<ScriptedSequence>();
    }

    void Start()
    {
        if (once && WorldStateSystem.Instance != null && WorldStateSystem.Instance.GetFlag(Key))
            _fired = true;
    }

    void Update()
    {
        if (_fired) return;
        switch (trigger)
        {
            case TriggerKind.ProximityRadius:
                var p = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
                if (p != null && Vector3.Distance(p.transform.position, transform.position) <= radius)
                    TryFire();
                break;
            case TriggerKind.DoorOpened:
                if (watchedDoor != null && watchedDoor.IsOpen) TryFire();
                break;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired || trigger != TriggerKind.TriggerVolume) return;
        if (IsPlayer(other)) TryFire();
    }

    static bool IsPlayer(Collider other)
    {
        var root = PlayerManager.Instance != null && PlayerManager.Instance.Player != null
            ? PlayerManager.Instance.Player.transform : null;
        return (root != null && other.transform.IsChildOf(root)) || other.CompareTag("Player");
    }

    // Manual fire (e.g. chained from another event). Still respects the flag gate.
    public void Fire() => TryFire();

    void TryFire()
    {
        if (_fired || !FlagsPass()) return;
        _fired = true;
        StartCoroutine(RunRoutine());
    }

    bool FlagsPass()
    {
        var ws = WorldStateSystem.Instance;
        if (requiredFlags != null)
            foreach (var f in requiredFlags)
                if (!string.IsNullOrEmpty(f) && (ws == null || !ws.GetFlag(f))) return false;
        if (blockedFlags != null && ws != null)
            foreach (var f in blockedFlags)
                if (!string.IsNullOrEmpty(f) && ws.GetFlag(f)) return false;
        return true;
    }

    IEnumerator RunRoutine()
    {
        if (sequence != null) yield return sequence.Run();
        // Mark complete only after the sequence finishes, so a save taken mid-event
        // (then reloaded) replays it rather than leaving it half-done.
        if (once) WorldStateSystem.Instance?.SetFlag(Key, true);
        else      _fired = false;   // repeatable — allow it to fire again
    }
}
