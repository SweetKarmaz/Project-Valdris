using UnityEngine;

public class EnemyLootDropper : MonoBehaviour
{
    public LootTableData lootTable;

    public void DropLoot()
    {
        if (lootTable == null) return;
        // TODO: roll loot table and spawn pickups
        Debug.Log("Loot dropped.");
    }
}
