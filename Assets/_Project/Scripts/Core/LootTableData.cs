using UnityEngine;

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Valdris/LootTable")]
public class LootTableData : ScriptableObject
{
    [System.Serializable]
    public class Entry { public LootItem item; [Range(0f, 1f)] public float dropChance; }
    public Entry[] entries;
}
