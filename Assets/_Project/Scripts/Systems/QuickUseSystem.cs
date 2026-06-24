using UnityEngine;

// Two quick-use slots (keys 9 and 0) for consumables like potions. Assignment of
// items into the slots is wired up later; for now the slots exist, render on the
// HUD, and fire their use logic when the key is pressed (no-op while empty).
public class QuickUseSystem : MonoBehaviour
{
    public static QuickUseSystem Instance { get; private set; }
    public const int SlotCount = 2;

    readonly LootItem[] _slots = new LootItem[SlotCount];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null) return;
        new GameObject("QuickUseSystem").AddComponent<QuickUseSystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public LootItem GetSlot(int i) => i >= 0 && i < SlotCount ? _slots[i] : null;
    public void SetSlot(int i, LootItem item) { if (i >= 0 && i < SlotCount) _slots[i] = item; }
    public void Clear() { for (int i = 0; i < SlotCount; i++) _slots[i] = null; }

    void Update()
    {
        if (PauseMenuController.IsPaused || GameUI.IsOpen || UIModal.IsOpen) return;
        if (InputManager.QuickUse1Pressed) Use(0);
        if (InputManager.QuickUse2Pressed) Use(1);
    }

    public void Use(int i)
    {
        var item = GetSlot(i);
        var inv  = InventorySystem.Instance;
        if (item == null || inv == null) return;

        // Apply a consumable's restore effects to the player, then consume one.
        var player = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        var stats  = player != null ? player.GetComponent<PlayerStats>() : null;
        if (item.itemType == LootItemType.Consumable && stats != null)
        {
            if (item.restoresHealth)
                stats.Heal(item.healthIsPercent ? stats.MaxHealth * item.healthAmount : item.healthAmount);
            if (item.restoresMana)
                stats.RestoreMana(item.manaIsPercent ? stats.MaxMana * item.manaAmount : item.manaAmount);
        }

        inv.RemoveLootItem(item, 1);
    }
}
