using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("References")]
    public Slider healthBar;
    public Slider manaBar;
    public Slider corruptionBar;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void UpdateHealth(float current, float max) => SetSlider(healthBar, current, max);
    public void UpdateMana(float current, float max) => SetSlider(manaBar, current, max);
    public void UpdateCorruption(float value) => SetSlider(corruptionBar, value, 100f);

    private void SetSlider(Slider s, float current, float max)
    {
        if (s != null) s.value = current / max;
    }
}
