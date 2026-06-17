using UnityEngine;
using System.Collections.Generic;

// How resistant a character is to status effects. Works on the player, NPCs,
// and enemies alike. Resist chance per effect =
//   Wisdom-based resist + story-driven resilience, capped at 95%.
// Immunities always block, regardless of any roll.
public class CharacterResistances : MonoBehaviour
{
    [System.Serializable]
    public class Resilience
    {
        public StatusEffectType effect;
        [Range(0f, 100f)] public float resistPercent;
    }

    [Tooltip("Status effects that can never be applied to this character (e.g. a construct can't sleep, a story boss can't be charmed).")]
    public List<StatusEffectType> immunities = new();

    [Tooltip("Extra resist chance per effect, set by story beats or creature design.")]
    public List<Resilience> resiliences = new();

    [Tooltip("Wisdom used for resist rolls when this character has no PlayerStats (NPCs/enemies). 10 = neutral.")]
    public float wisdom = 10f;

    [Tooltip("Resist chance gained per point of Wisdom above 10. 2 = 2%/point.")]
    public float resistPerWisdom = 2f;

    private PlayerStats _playerStats;

    private void Awake() => _playerStats = GetComponent<PlayerStats>();

    public bool IsImmune(StatusEffectType effect) => immunities.Contains(effect);

    public float ResistChance(StatusEffectType effect)
    {
        float effectiveWisdom = _playerStats != null ? _playerStats.Wisdom : wisdom;
        float chance = Mathf.Max(0f, (effectiveWisdom - 10f) * resistPerWisdom);
        foreach (Resilience r in resiliences)
            if (r.effect == effect) chance += r.resistPercent;
        return Mathf.Clamp(chance, 0f, 95f); // never a guaranteed resist without immunity
    }
}
