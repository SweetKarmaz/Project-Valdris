using UnityEngine;

// An invisible (editor-gizmo only) barrier that pushes the player back out when
// they try to enter — for sealing off areas. Optionally flag-gated so it can be
// opened later (e.g. after a quest) or only become active under certain
// conditions. Works against both walking and teleporting in (it shoves on the
// next physics step while overlapping).
[RequireComponent(typeof(BoxCollider))]
public class BlockZone : MonoBehaviour
{
    [Tooltip("If ANY of these world flags is set, the barrier is lifted (passable).")]
    public string[] openWhenFlags;
    [Tooltip("If set, the barrier is only active once ALL these flags are set (otherwise passable).")]
    public string[] activeWhenFlags;
    [Tooltip("Shown when the player is turned back. Blank = no message.")]
    public string blockedMessage = "You can't go this way yet.";

    float _nextMessageTime;

    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    void OnTriggerStay(Collider other) => PushOut(other);
    void OnTriggerEnter(Collider other) => PushOut(other);

    void PushOut(Collider other)
    {
        if (!other.CompareTag("Player") || !IsBlocking()) return;

        var player = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc == null) return;

        // Push out through the nearest horizontal face of the box (+ a small margin).
        Bounds b = GetComponent<BoxCollider>().bounds;
        Vector3 p = player.transform.position;
        float dx = b.extents.x - Mathf.Abs(p.x - b.center.x);
        float dz = b.extents.z - Mathf.Abs(p.z - b.center.z);

        Vector3 push = dx < dz
            ? new Vector3(Mathf.Sign(p.x - b.center.x) * (dx + 0.2f), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(p.z - b.center.z) * (dz + 0.2f));
        cc.Move(push);

        if (!string.IsNullOrEmpty(blockedMessage) && Time.time >= _nextMessageTime)
        {
            ScreenNotifier.Show(blockedMessage);
            _nextMessageTime = Time.time + 2f;
        }
    }

    bool IsBlocking()
    {
        var ws = WorldStateSystem.Instance;
        if (openWhenFlags != null)
            foreach (var f in openWhenFlags)
                if (!string.IsNullOrEmpty(f) && ws != null && ws.GetFlag(f)) return false;  // opened
        if (activeWhenFlags != null && activeWhenFlags.Length > 0)
            foreach (var f in activeWhenFlags)
                if (string.IsNullOrEmpty(f) || ws == null || !ws.GetFlag(f)) return false;   // not yet active
        return true;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.14f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.85f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
