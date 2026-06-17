using UnityEngine;

// Central registry of every data asset, so saved games (which store asset
// names) can be resolved back to the real assets on load. Keep this asset's
// arrays up to date as new content is added.
[CreateAssetMenu(fileName = "GameDatabase", menuName = "Valdris/Game Database")]
public class GameDatabase : ScriptableObject
{
    public SkillData[] skills;
    public SpellData[] spells;
    public BuffData[]  buffs;
    public QuestData[] quests;

    [Tooltip("SpellRegistry is the authoritative spell list. " +
             "FindSpell checks this after the spells array so every registered spell " +
             "is resolvable on load without duplicating entries here.")]
    public SpellRegistry spellRegistry;

    [Tooltip("Auto-populated by LootRegistryBuilder. Holds every prefab in Assets/_Project/Prefabs/Loot/ " +
             "that carries a LootItem component. Used by loot tables, NPC item pickers, and the inventory UI.")]
    public LootRegistry lootRegistry;

    [Tooltip("Loot-container prefab spawned at the player's feet when an external item grant " +
             "(e.g. a quest reward) can't fit in a full inventory. Point at a LootContainers/ bag.")]
    public LootContainer droppedBagPrefab;

    [Tooltip("Optional override for the randomized-loot drop tables (one row per source rarity). " +
             "Leave empty to use the built-in defaults in LootDropTable. A row is used only if its " +
             "goldMultiplier > 0.")]
    public System.Collections.Generic.List<RarityTier> lootTiers = new();

    public SkillData FindSkill(string assetName) => Find(skills, assetName);
    public BuffData  FindBuff(string assetName)  => Find(buffs,  assetName);
    public QuestData FindQuest(string assetName) => Find(quests, assetName);

    public LootItem FindLootItem(string itemName)
    {
        if (lootRegistry == null || string.IsNullOrEmpty(itemName)) return null;
        return lootRegistry.FindByName(itemName);
    }

    public SpellData FindSpell(string assetName)
    {
        var result = Find(spells, assetName);
        if (result != null) return result;
        // SpellRegistry is the live spell list — fall back so spells added there
        // are resolvable without also maintaining the spells array above.
        return spellRegistry != null ? spellRegistry.FindByAssetName(assetName) : null;
    }

    private static T Find<T>(T[] array, string assetName) where T : ScriptableObject
    {
        if (array == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (T asset in array)
            if (asset != null && asset.name == assetName) return asset;
        return null;
    }
}
