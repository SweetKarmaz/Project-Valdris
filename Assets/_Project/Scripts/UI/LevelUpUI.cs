using UnityEngine;
using TMPro;

public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }
    public GameObject panel;
    public TMP_Text levelText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        panel?.SetActive(false);
    }

    public void Show(int newLevel)
    {
        panel?.SetActive(true);
        if (levelText != null) levelText.text = $"Level {newLevel}!";
    }

    public void Hide() => panel?.SetActive(false);
}
