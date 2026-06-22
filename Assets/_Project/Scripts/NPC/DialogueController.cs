using UnityEngine;

// Player-initiated conversation on an NPC. Reached through the reticle Talk path
// (InteractionHUD → NPCBase.Interact). Optionally turns the NPC hostile the
// moment the conversation ends — the "talks to you, then attacks" beat.
public class DialogueController : NPCBase
{
    public DialogueData dialogueData;

    [Tooltip("If set, the NPC enters combat with the player as soon as this conversation ends.")]
    public bool becomeHostileOnEnd = false;

    public override void Interact()
    {
        base.Interact();
        DialogueSystem.EnsureExists();

        var npc = GetComponent<NpcController>();
        System.Action onEnd = becomeHostileOnEnd && npc != null ? npc.EnterCombat : (System.Action)null;
        DialogueSystem.Instance.StartDialogue(dialogueData, npc, forced: false, onComplete: onEnd);
    }
}
