using System.Collections;
using UnityEngine;

// Plays a conversation and waits for it to close before the sequence continues.
// Works for both a forced face-to-face talk and an overheard line set (leave
// faceTarget empty and author the speakers as a narrator + named voices).
public class DialogueStep : EventStep
{
    public DialogueData dialogue;
    [Tooltip("Forced: the player can't Esc out — they must click through to the end.")]
    public bool forced = true;
    [Tooltip("Hold the player still for the duration of this dialogue.")]
    public bool stopPlayer = true;
    [Tooltip("Optional: snap the player's view toward this on open (e.g. the speaking NPC).")]
    public Transform faceTarget;

    public override IEnumerator Run()
    {
        if (dialogue == null) yield break;

        DialogueSystem.EnsureExists();
        if (stopPlayer) CutsceneControl.Lock();

        bool done = false;
        DialogueSystem.Instance.StartDialogue(dialogue, null, forced, () => done = true, faceTarget);
        while (!done) yield return null;

        if (stopPlayer) CutsceneControl.Unlock();
    }
}
