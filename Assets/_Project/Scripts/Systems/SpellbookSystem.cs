using UnityEngine;
using System.Collections.Generic;

public class SpellbookSystem : MonoBehaviour
{
    public static SpellbookSystem Instance { get; private set; }

    public const int SlotCount = 4;

    private readonly List<SpellData> _knownSpells = new();
    private readonly SpellData[] _slots = new SpellData[SlotCount];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LearnSpell(SpellData spell)
    {
        if (spell == null || _knownSpells.Contains(spell)) return;
        _knownSpells.Add(spell);
        // Convenience: new spells fill the first empty hotbar slot.
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i] == null) { _slots[i] = spell; break; }
    }

    public bool KnowsSpell(SpellData spell) => _knownSpells.Contains(spell);
    public IReadOnlyList<SpellData> GetSpells() => _knownSpells;

    public SpellData GetSlot(int index) =>
        index >= 0 && index < SlotCount ? _slots[index] : null;

    public void SetSlot(int index, SpellData spell)
    {
        if (index < 0 || index >= SlotCount) return;
        if (spell != null && !KnowsSpell(spell)) return;
        _slots[index] = spell;
    }

    // ---- Save/load ----

    public List<string> CaptureKnown() => _knownSpells.ConvertAll(s => s.name);

    public List<string> CaptureSlots()
    {
        var saved = new List<string>();
        foreach (SpellData slot in _slots) saved.Add(slot != null ? slot.name : "");
        return saved;
    }

    public void RestoreState(List<string> known, List<string> slots, GameDatabase database)
    {
        _knownSpells.Clear();
        System.Array.Clear(_slots, 0, SlotCount);
        if (known != null)
            foreach (string spellName in known)
            {
                SpellData spell = database.FindSpell(spellName);
                if (spell != null) _knownSpells.Add(spell);
                else Debug.LogWarning($"Saved spell not found: {spellName}");
            }
        if (slots != null)
            for (int i = 0; i < SlotCount && i < slots.Count; i++)
                if (!string.IsNullOrEmpty(slots[i]))
                    _slots[i] = database.FindSpell(slots[i]);
    }
}

