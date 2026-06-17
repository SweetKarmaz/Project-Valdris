using UnityEngine;

// Attach to an NPC to offer a quest via dialogue interaction.
// If the quest has dialogueText or an offerLine it should be set on a
// QuestTriggerProp instead; this component auto-accepts on interact
// (suitable for tutorial hand-offs, cutscene triggers, etc.).
public class QuestGiver : NPCBase
{
    public QuestData questToOffer;

    public override void Interact()
    {
        base.Interact();
        if (questToOffer != null && QuestSystem.Instance != null
            && QuestSystem.Instance.CanOffer(questToOffer))
        {
            QuestSystem.Instance.AcceptQuest(questToOffer);
        }
    }
}
