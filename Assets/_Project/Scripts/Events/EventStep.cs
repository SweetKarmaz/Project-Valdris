using System.Collections;
using UnityEngine;

// One step in a ScriptedSequence. A step may take several frames — yield until
// it's done (e.g. wait for a dialogue to close, an NPC to arrive). Steps live as
// components on child GameObjects under a ScriptedSequence and run in hierarchy
// order.
public abstract class EventStep : MonoBehaviour
{
    public abstract IEnumerator Run();
}
