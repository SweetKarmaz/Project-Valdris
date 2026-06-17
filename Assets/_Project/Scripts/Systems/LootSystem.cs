using UnityEngine;

public class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void GenerateLoot(LootTableData table, Vector3 position)
    {
        if (table == null) return;
        // TODO: instantiate loot pickups from table at position
    }
}

