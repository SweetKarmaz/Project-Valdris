using UnityEngine;

// Handles player spell INPUT and hotbar slot selection only.
// All spell execution (damage, cooldowns, mana, animations, projectiles)
// lives in Spellcaster — which is shared with NPCs and enemies.
[RequireComponent(typeof(Spellcaster))]
public class PlayerMagic : MonoBehaviour
{
    public int SelectedSlot { get; private set; }

    Spellcaster _caster;

    void Awake() => _caster = GetComponent<Spellcaster>();

    void Update()
    {
        if (PauseMenuController.IsPaused || GameUI.IsOpen) return;

        // Keys 1-4 select a hotbar slot.
        int slot = InputManager.SpellSlotPressed;
        if (slot >= 0) SelectedSlot = slot;

        if (InputManager.CastPressed)
        {
            SpellData spell = SpellbookSystem.Instance?.GetSlot(SelectedSlot);
            if (spell == null) return;

            // Direction: wherever the camera faces (so aiming feels natural).
            Vector3 dir = Camera.main != null
                ? Camera.main.transform.forward
                : transform.forward;

            _caster.TryCast(spell, dir);
        }
    }
}
