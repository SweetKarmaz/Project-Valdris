using UnityEngine;

// An invisible trigger volume that makes its assigned NPCs attack the player the
// moment the player steps inside. Drop it on a GameObject with a trigger Collider
// (a Box is easiest), size the box over the area, and drag the guards into the
// list. Re-arms on each entry by default so leaving and returning re-aggros them.
//
// Optional flag gate lets you disable the ambush once a condition is met
// (e.g. requiredFlags / blockedFlags = ["guards_pacified"]).
[RequireComponent(typeof(Collider))]
public class GuardAlertZone : MonoBehaviour
{
    [Tooltip("NPCs that turn hostile and attack when the player enters this zone.")]
    public NpcController[] guards;

    [Tooltip("Fire only the first time ever (persists in saves). Off = re-aggro on every entry.")]
    public bool onlyOnce = false;
    [Tooltip("World flag remembering it fired (only used when Only Once is on). Blank = auto from object name.")]
    public string completionFlag;

    [Header("Gate (optional)")]
    [Tooltip("All of these world flags must be SET for the zone to fire.")]
    public string[] requiredFlags;
    [Tooltip("None of these world flags may be set for the zone to fire.")]
    public string[] blockedFlags;

    bool _fired;

    string Key => string.IsNullOrEmpty(completionFlag) ? $"alertzone_{gameObject.name}" : completionFlag;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void Start()
    {
        if (onlyOnce && WorldStateSystem.Instance != null && WorldStateSystem.Instance.GetFlag(Key))
            _fired = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && onlyOnce) return;
        if (!IsPlayer(other) || !FlagsPass()) return;

        if (guards != null)
            foreach (var g in guards)
                if (g != null && !g.IsDead) g.EnterCombat();

        if (onlyOnce)
        {
            _fired = true;
            WorldStateSystem.Instance?.SetFlag(Key, true);
        }
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

    static bool IsPlayer(Collider other)
    {
        var root = PlayerManager.Instance != null && PlayerManager.Instance.Player != null
            ? PlayerManager.Instance.Player.transform : null;
        return (root != null && other.transform.IsChildOf(root)) || other.CompareTag("Player");
    }

    // Draw the zone in the editor (it's invisible in-game) plus links to its guards.
    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.18f);
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 1f);
        }

        if (guards == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        foreach (var g in guards)
            if (g != null) Gizmos.DrawLine(transform.position, g.transform.position);
    }
}
