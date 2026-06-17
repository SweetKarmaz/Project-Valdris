using UnityEngine;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    public GameObject panel;
    public TMP_Text speakerText;
    public TMP_Text bodyText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        panel?.SetActive(false);
    }

    public void Show(DialogueData data)
    {
        if (data == null) return;
        panel?.SetActive(true);
        // TODO: populate speaker and body from data
    }

    public void Hide() => panel?.SetActive(false);
}
