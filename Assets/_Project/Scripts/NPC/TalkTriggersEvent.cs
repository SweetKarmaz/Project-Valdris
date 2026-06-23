using UnityEngine;

// Bridges the reticle Talk interaction to a ScriptedEvent. Put this on a
// talkable NPC (NpcController.canTalk = true) whose conversation is driven by a
// sequence rather than a plain DialogueController — e.g. the Mad Prisoner, where
// talking should run dialogue + give the stick + start the quest.
//
// The linked ScriptedEvent should use Trigger = Manual. If its `once` is on it
// fires a single time; turn `once` off to make the NPC re-talkable.
public class TalkTriggersEvent : NPCBase
{
    [Tooltip("The ScriptedEvent (Trigger = Manual) to run when the player talks to this NPC.")]
    public ScriptedEvent scriptedEvent;

    public override void Interact()
    {
        base.Interact();
        if (scriptedEvent != null) scriptedEvent.Fire();
    }
}
