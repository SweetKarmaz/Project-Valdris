using System.Collections;
using UnityEngine;

// Pauses the sequence for a fixed time.
public class WaitStep : EventStep
{
    public float seconds = 1f;

    public override IEnumerator Run()
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
    }
}
