using System.Collections;
using UnityEngine;

// Accepts (gives) a quest as part of a sequence — e.g. between a DialogueStep and
// an NpcHostileStep. The "New Quest" popup appears automatically via QuestSystem,
// and this step waits for the player to dismiss it before the sequence continues.
public class GiveQuestStep : EventStep
{
    public QuestData quest;
    [Tooltip("Wait for the player to close the quest popup before the next step runs.")]
    public bool waitForPopup = true;

    public override IEnumerator Run()
    {
        if (quest == null) yield break;

        QuestSystem.Instance?.AcceptQuest(quest);

        if (waitForPopup)
            while (QuestPopupSystem.IsOpen)
                yield return null;
    }
}
