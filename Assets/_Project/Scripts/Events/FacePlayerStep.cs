using System.Collections;
using UnityEngine;

// Snaps the player's view toward a target (defaults to this step's own transform).
public class FacePlayerStep : EventStep
{
    [Tooltip("Who/what to face. Defaults to this GameObject if left empty.")]
    public Transform target;

    public override IEnumerator Run()
    {
        Transform t = target != null ? target : transform;
        FirstPersonCamera.Active?.FaceToward(t.position);
        yield break;
    }
}
