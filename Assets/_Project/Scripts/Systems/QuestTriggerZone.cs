using UnityEngine;

// Attach to a trigger collider in the scene. When the player enters the zone
// the quest is automatically accepted (no prompt). Use for environmental
// discoveries, trap-sprung quests, etc.
[RequireComponent(typeof(Collider))]
public class QuestTriggerZone : MonoBehaviour
{
    [Tooltip("The quest to accept when the player enters this zone.")]
    public QuestData questData;

    [Tooltip("When true the trigger fires only once and then disables itself.")]
    public bool oneShot = true;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (questData == null) return;
        if (QuestSystem.Instance == null || !QuestSystem.Instance.CanOffer(questData)) return;

        QuestSystem.Instance.AcceptQuest(questData);

        if (oneShot) gameObject.SetActive(false);
    }
}
