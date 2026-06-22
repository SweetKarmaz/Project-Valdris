using System.Collections;
using UnityEngine;

// Walks the player along a set of waypoints while keeping mouse-look free, with
// optional escort NPCs that follow alongside. The player's body is driven by
// this step; gravity is still applied by PlayerController's locked branch.
//
// Author waypoints as empty GameObjects placed on the floor along the route.
public class EscortPlayerStep : EventStep
{
    [Tooltip("Floor points the player walks through, in order.")]
    public Transform[] waypoints;
    public float speed = 2.5f;
    [Tooltip("How close (XZ) counts as reaching a waypoint.")]
    public float arriveDistance = 0.5f;
    [Tooltip("Safety cap so a blocked path can't hang the sequence forever.")]
    public float perWaypointTimeout = 25f;

    [Tooltip("NPCs that walk along with the player (they path to the player's position).")]
    public NpcController[] escorts;

    public override IEnumerator Run()
    {
        var go = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        if (go == null || waypoints == null || waypoints.Length == 0) yield break;

        var cc   = go.GetComponent<CharacterController>();
        var anim = go.GetComponent<PlayerAnimator>();

        CutsceneControl.Lock();                       // ref-counted; safe if already locked
        BeginEscorts();

        foreach (var wp in waypoints)
        {
            if (wp == null) continue;
            float t = 0f;
            while (true)
            {
                Vector3 to = wp.position - go.transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d <= arriveDistance || t >= perWaypointTimeout) break;

                if (cc != null) cc.Move(to / Mathf.Max(d, 0.0001f) * speed * Time.deltaTime);
                anim?.SetSpeed(0.55f);
                FollowEscorts(go.transform.position);

                t += Time.deltaTime;
                yield return null;
            }
        }

        anim?.SetSpeed(0f);
        EndEscorts();
        CutsceneControl.Unlock();
    }

    void BeginEscorts()  { if (escorts != null) foreach (var e in escorts) if (e != null) e.BeginScripted(); }
    void EndEscorts()    { if (escorts != null) foreach (var e in escorts) if (e != null) e.EndScripted(); }
    void FollowEscorts(Vector3 target)
    { if (escorts != null) foreach (var e in escorts) if (e != null) e.ScriptedMoveTo(target); }
}
