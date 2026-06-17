using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    public GameObject panel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        panel?.SetActive(false);
    }

    public void Toggle() => panel?.SetActive(!panel.activeSelf);
    public void Refresh() { /* TODO: rebuild item slots from InventorySystem */ }
}
