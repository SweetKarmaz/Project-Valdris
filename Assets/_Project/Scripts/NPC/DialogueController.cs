using UnityEngine;

public class DialogueController : NPCBase
{
    public DialogueData dialogueData;

    public override void Interact()
    {
        base.Interact();
        DialogueSystem.Instance?.StartDialogue(dialogueData);
    }
}
