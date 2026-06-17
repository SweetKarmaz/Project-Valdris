using System.Collections.Generic;
using UnityEngine;

// Single ScriptableObject that lists every spell in the game.
// Adding a new spell: create a SpellData asset, then drag it into this registry.
// Systems that need to look up spells by name (save/load, debug console) use this.
//
// Create via: Assets > Create > Valdris > Spell Registry
[CreateAssetMenu(fileName = "SpellRegistry", menuName = "Valdris/Spell Registry")]
public class SpellRegistry : ScriptableObject
{
    public List<SpellData> allSpells = new();

    public SpellData FindByName(string spellName)
    {
        foreach (var s in allSpells)
            if (s != null && s.spellName == spellName) return s;
        return null;
    }

    public SpellData FindByAssetName(string assetName)
    {
        foreach (var s in allSpells)
            if (s != null && s.name == assetName) return s;
        return null;
    }
}
