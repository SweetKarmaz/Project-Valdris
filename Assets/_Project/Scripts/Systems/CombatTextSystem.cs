using UnityEngine;
using TMPro;

// Spawns floating world-space text over characters: status results
// ("Immune!", "Resisted!"), buff applications, damage numbers, etc.
public class CombatTextSystem : MonoBehaviour
{
    public static CombatTextSystem Instance { get; private set; }

    [Header("Appearance")]
    public float fontSize = 4f;
    public float spawnHeight = 2.2f; // meters above the target's origin

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Show(Transform target, string text, Color color, float sizeMultiplier = 1f)
    {
        if (target == null) return;
        Show(target.position + Vector3.up * spawnHeight, text, color, sizeMultiplier);
    }

    public void Show(Vector3 worldPosition, string text, Color color, float sizeMultiplier = 1f)
    {
        var go = new GameObject("CombatText");
        go.transform.position = worldPosition;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize * sizeMultiplier;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        go.AddComponent<FloatingCombatText>();
    }

    // Damage number over a target; crits are bigger and orange.
    public void ShowDamage(Transform target, float amount, bool isCrit)
    {
        if (isCrit) Show(target, $"{amount:0}!", new Color(1f, 0.55f, 0.1f), 1.6f);
        else Show(target, amount.ToString("0"), Color.white);
    }

    // Typed damage number, coloured by damage type. Crits are bigger.
    public void ShowDamage(Transform target, float amount, bool isCrit, DamageType type)
    {
        Color c = DamageColors.Of(type);
        if (isCrit) Show(target, $"{amount:0}!", c, 1.6f);
        else        Show(target, amount.ToString("0"), c);
    }

    // ---- Convenience wrappers with consistent colors ----

    public void ShowStatusResult(Transform target, StatusApplyResult result, string effectName)
    {
        switch (result)
        {
            case StatusApplyResult.Applied:
                Show(target, effectName, new Color(0.6f, 1f, 0.6f)); // soft green
                break;
            case StatusApplyResult.Resisted:
                Show(target, "Resisted!", new Color(1f, 0.85f, 0.3f)); // gold
                break;
            case StatusApplyResult.Immune:
                Show(target, "Immune", new Color(0.6f, 0.7f, 0.9f)); // cool gray-blue
                break;
        }
    }
}
