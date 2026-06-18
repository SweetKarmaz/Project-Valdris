using System.Collections.Generic;
using UnityEngine;

// A named location the player can be placed at — the destination end of a
// Gateway, or a same-scene teleport target. Drop one into a scene, give it a
// unique spawnId, and optionally a zoneDisplayName for the loading screen.
//
// Invisible in play (no renderer) — it shows as a bright coloured gizmo in the
// editor so it's easy to find in a complex scene.
[DisallowMultipleComponent]
public class SpawnPoint : MonoBehaviour
{
    public static readonly List<SpawnPoint> All = new();

    [Tooltip("Unique id within this scene. Gateways reference this id to place the player here.")]
    public string spawnId = "spawn";

    [Tooltip("Human-readable area name shown on the loading screen, e.g. 'The City of Zarn'. " +
             "A Gateway loading into this scene can use it for its 'Loading …' text.")]
    public string zoneDisplayName;

    [Tooltip("Gizmo colour so the marker stands out in a busy scene.")]
    public Color gizmoColor = new Color(0.2f, 1f, 0.45f);

    void OnEnable()  { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    public Vector3    SpawnPosition => transform.position;
    public Quaternion SpawnRotation => transform.rotation;

    public static SpawnPoint FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var s in All)
            if (s != null && s.spawnId == id) return s;
        return null;
    }

    // ── Editor gizmo ────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, 0.4f);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
        Gizmos.DrawWireSphere(transform.position, 0.7f);

        // Facing arrow so the spawn rotation is visible.
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.3f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = gizmoColor;
        string label = string.IsNullOrEmpty(zoneDisplayName) ? spawnId : $"{spawnId}  ({zoneDisplayName})";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.9f, label);
#endif
    }
}
