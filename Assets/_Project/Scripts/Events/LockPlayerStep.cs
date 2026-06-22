using System.Collections;

// Takes movement away from the player (mouse-look still works). Pair with an
// UnlockPlayerStep later in the sequence.
public class LockPlayerStep : EventStep
{
    public override IEnumerator Run()
    {
        CutsceneControl.Lock();
        yield break;
    }
}
