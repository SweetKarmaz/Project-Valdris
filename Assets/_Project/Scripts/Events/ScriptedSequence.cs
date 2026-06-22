using System.Collections;
using UnityEngine;

// Runs its EventStep components in hierarchy order, each to completion, one after
// the next. Put the steps on child GameObjects (or extra components on this one)
// and order them by their order in the Hierarchy.
public class ScriptedSequence : MonoBehaviour
{
    public bool IsRunning { get; private set; }

    public IEnumerator Run()
    {
        if (IsRunning) yield break;
        IsRunning = true;

        // GetComponentsInChildren returns self-first, then children depth-first —
        // i.e. top-to-bottom as shown in the Hierarchy.
        var steps = GetComponentsInChildren<EventStep>(true);
        foreach (var step in steps)
        {
            if (step == null || !step.isActiveAndEnabled) continue;
            yield return step.Run();
        }

        IsRunning = false;
    }
}
