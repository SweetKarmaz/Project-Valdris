using UnityEngine;

public class SkillTreeUI : MonoBehaviour
{
    public static SkillTreeUI Instance { get; private set; }
    public GameObject panel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        panel?.SetActive(false);
    }

    public void Toggle() => panel?.SetActive(!panel.activeSelf);
}
