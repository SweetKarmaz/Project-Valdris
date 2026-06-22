using System.Collections;
using UnityEngine;

// Opens / closes / toggles a door during a sequence (e.g. a guard opening the
// way, then shutting it behind you). The player's own lock is untouched — this
// is the scripted swing, not a player unlock.
public class DoorStep : EventStep
{
    public enum Mode { Open, Close, Toggle }

    public DoorController door;
    public Mode mode = Mode.Open;
    [Tooltip("Snap instantly instead of swinging.")]
    public bool instant = false;
    [Tooltip("Pause after, to let the swing finish before the next step.")]
    public float waitAfter = 0.6f;

    public override IEnumerator Run()
    {
        if (door == null) yield break;

        bool open = mode == Mode.Open || (mode == Mode.Toggle && !door.IsOpen);

        if (instant) door.SetOpenInstant(open);
        else if (mode == Mode.Toggle) door.Toggle();
        else if (open) door.Open();
        else door.Close();

        if (waitAfter > 0f) yield return new WaitForSeconds(waitAfter);
    }
}
