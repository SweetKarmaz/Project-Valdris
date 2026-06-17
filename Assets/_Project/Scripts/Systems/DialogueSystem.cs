using UnityEngine;

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartDialogue(DialogueData data)
    {
        if (data == null) return;
        DialogueUI.Instance?.Show(data);
    }

    public void EndDialogue() => DialogueUI.Instance?.Hide();
}

