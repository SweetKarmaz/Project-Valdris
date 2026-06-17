using UnityEngine;

// Detect Corruption: an active skill, not a spell — no mana cost, always
// available once the skill is unlocked. Pulses a circle around the player;
// each NPC/mob in range rolls its immunity/resilience against Corruption,
// and on failure its hidden Corruption buffs become visible.
public class PlayerDetection : MonoBehaviour
{
    [Header("Detect Corruption")]
    [Tooltip("The skill that unlocks this ability.")]
    public SkillData detectCorruptionSkill;
    [Tooltip("Pulse radius in meters. ~3m = 10 feet.")]
    public float radius = 3f;
    public float cooldown = 5f;
    public GameObject pulseVFXPrefab;

    private CharacterBuffs _buffs;
    private float _readyAt;

    private void Awake() => _buffs = GetComponent<CharacterBuffs>();

    private void Update()
    {
        if (PauseMenuController.IsPaused) return;
        if (!InputManager.DetectPressed) return;
        if (Time.time < _readyAt) return;
        if (_buffs != null && _buffs.AreAbilitiesPrevented) return; // asleep/silenced
        if (SkillSystem.Instance == null || !SkillSystem.Instance.HasSkill(detectCorruptionSkill)) return;

        _readyAt = Time.time + cooldown;
        Pulse();
    }

    private void Pulse()
    {
        if (pulseVFXPrefab != null)
            Instantiate(pulseVFXPrefab, transform.position, Quaternion.identity);

        foreach (Collider hit in Physics.OverlapSphere(transform.position, radius))
        {
            if (hit.transform == transform) continue; // not the caster
            if (!hit.TryGetComponent<CharacterBuffs>(out var targetBuffs)) continue;

            // The target's immunity/resilience resists being seen through.
            if (hit.TryGetComponent<CharacterResistances>(out var resistances))
            {
                if (resistances.IsImmune(StatusEffectType.Corruption))
                {
                    CombatTextSystem.Instance?.ShowStatusResult(hit.transform, StatusApplyResult.Immune, null);
                    continue;
                }
                if (Random.Range(0f, 100f) < resistances.ResistChance(StatusEffectType.Corruption))
                {
                    CombatTextSystem.Instance?.ShowStatusResult(hit.transform, StatusApplyResult.Resisted, null);
                    continue;
                }
            }

            int revealed = targetBuffs.RevealHidden(StatusEffectType.Corruption);
            if (revealed > 0)
                CombatTextSystem.Instance?.Show(hit.transform, "Corruption!", new Color(0.8f, 0.3f, 1f)); // violet
            // TODO: corruption highlight VFX on revealed targets.
        }
    }
}
