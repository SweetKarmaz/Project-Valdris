using System.Collections;

// Returns movement control to the player. Pair with an earlier LockPlayerStep.
public class UnlockPlayerStep : EventStep
{
    public override IEnumerator Run()
    {
        CutsceneControl.Unlock();
        yield break;
    }
}
