using System.Collections;
using UnityEngine;

// Makes an NPC draw and attack the player immediately (the "dialogue ends, then
// it lunges" beat). Uses the existing combat entry, which defaults its target to
// the player.
public class NpcHostileStep : EventStep
{
    public NpcController npc;

    public override IEnumerator Run()
    {
        if (npc != null) npc.EnterCombat();
        yield break;
    }
}
