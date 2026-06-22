using System.Collections;
using UnityEngine;

// Sends one NPC to a point and (optionally) waits until it arrives — e.g. a
// guard walking to the door before opening it.
public class MoveNpcStep : EventStep
{
    public NpcController npc;
    public Transform target;
    public bool  waitUntilArrived = true;
    [Tooltip("Safety cap so a blocked path can't hang the sequence.")]
    public float timeout = 20f;
    [Tooltip("Return the NPC to its normal AI after arriving. Turn off to keep it held in place for following steps.")]
    public bool  releaseControlAfter = true;

    public override IEnumerator Run()
    {
        if (npc == null || target == null) yield break;

        npc.BeginScripted();
        npc.ScriptedMoveTo(target.position);

        if (waitUntilArrived)
        {
            float t = 0f;
            while (!npc.ScriptedArrived && t < timeout)
            {
                npc.ScriptedMoveTo(target.position);
                t += Time.deltaTime;
                yield return null;
            }
        }

        if (releaseControlAfter) npc.EndScripted();
    }
}
