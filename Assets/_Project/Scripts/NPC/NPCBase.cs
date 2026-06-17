using UnityEngine;

public class NPCBase : MonoBehaviour
{
    public string npcName;
    public bool isInteractable = true;

    public virtual void Interact()
    {
        if (!isInteractable) return;
        Debug.Log($"Interacting with {npcName}");
    }
}
