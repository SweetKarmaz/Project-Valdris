using System.Collections.Generic;
using UnityEngine;

// A lootable world container (chest, sack, bag) backed by the shared Inventory +
// LootWindowGUI, looted by aiming at it (InteractionHUD raycast). Two flavours:
//   • Placed in the editor — fill "Starting Contents", set a unique Container Id;
//     contents + looted state persist via SceneStateManager.
//   • Dropped at runtime — created by SpawnDropped() for inventory overflow;
//     destroyed when emptied, persisted while it still holds anything.
[DisallowMultipleComponent]
public class LootContainer : MonoBehaviour
{
    public static readonly List<LootContainer> All = new();

    public enum EmptyBehavior { KeepEmpty, Hide, Destroy }

    [Header("Loot Quality")]
    [Tooltip("Rarity of this container. Drives the randomized-loot roll (higher rarity = " +
             "better/more items and gold). See LootDropTable.")]
    public ItemRarity rarity = ItemRarity.Common;

    [Header("Contents")]
    [Tooltip("Items this container starts with. dropChance < 1 makes an item a random roll, " +
             "generated once when first opened. These are added ON TOP of the randomized roll.")]
    public List<NpcItem> startingContents = new();
    public int goldMin;
    public int goldMax;

    [Header("Behavior")]
    [Tooltip("What happens once the container is emptied.")]
    public EmptyBehavior emptyBehavior = EmptyBehavior.Hide;
    [Tooltip("Reticle verb (e.g. Search, Open, Loot).")]
    public string interactionLabel = "Search";

    [Header("Save")]
    [Tooltip("Unique ID within the scene. Required for a placed container to persist.")]
    public string containerId;

    // ── Runtime ───────────────────────────────────────────────────────────────

    readonly Inventory _loot = new(100000);
    int     _gold;
    bool    _rolled;
    bool    _emptied;
    bool    _open;
    Vector2 _scroll;

    // True for runtime overflow bags (always destroy when emptied; re-spawned
    // from save rather than matched to an existing scene object).
    [System.NonSerialized] public bool isDropped;

    public string Label => string.IsNullOrEmpty(interactionLabel) ? "Search" : interactionLabel;

    // Interactable until it has been emptied.
    public bool CanLoot => !_emptied;

    void Awake() => All.Add(this);

    void Start()
    {
        // Placed containers self-register and restore; dropped ones are registered
        // by SpawnDropped/SceneStateManager directly, so skip auto-register here.
        if (!isDropped && !string.IsNullOrEmpty(containerId))
            SceneStateManager.Instance?.RegisterContainer(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
        SceneStateManager.Instance?.UnregisterContainer(this);
    }

    // ── Interaction ─────────────────────────────────────────────────────────

    public void OpenLoot()
    {
        if (!CanLoot || _open) return;
        if (!_rolled) RollContents();
        _open = true;
        UIModal.Push();
    }

    void CloseLoot()
    {
        if (!_open) return;
        _open = false;
        UIModal.Pop();
    }

    void RollContents()
    {
        _rolled = true;

        // Randomized loot based on this container's rarity.
        foreach (var r in LootDropTable.Roll(rarity))
        {
            var item = LootGenerator.GenerateItem(r);
            if (item != null) _loot.Add(item, 1);
        }

        // Hand-authored starting contents, added on top.
        foreach (var entry in startingContents)
            if (entry.lootItem != null && Random.value <= entry.dropChance)
                _loot.Add(entry.lootItem, Mathf.Max(1, entry.quantity));

        int baseGold = goldMax > goldMin ? Random.Range(goldMin, goldMax + 1) : goldMin;
        _gold = Mathf.RoundToInt(baseGold * LootDropTable.GoldMultiplier(rarity));
    }

    void OnGUI()
    {
        if (!_open) return;
        if (PauseMenuController.IsPaused || GameUI.IsOpen) { CloseLoot(); return; }

        if (!LootWindowGUI.Draw(Label, _loot, ref _gold, ref _scroll))
            CloseLoot();

        if (_loot.SlotCount == 0 && _gold == 0)
            Empty();
    }

    void Empty()
    {
        _emptied = true;
        CloseLoot();

        bool remove = isDropped || emptyBehavior == EmptyBehavior.Destroy;

        // For removed containers, unregister BEFORE saving so the now-empty
        // container isn't written to the scene state (and won't respawn).
        if (remove) SceneStateManager.Instance?.UnregisterContainer(this);
        SceneStateManager.Instance?.SaveState();

        if (remove)
            Destroy(gameObject);
        else if (emptyBehavior == EmptyBehavior.Hide)
            gameObject.SetActive(false);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public SavedContainerState CaptureState() => new()
    {
        containerId = containerId,
        rolled      = _rolled,
        isDropped   = isDropped,
        prefabName  = gameObject.name,
        position    = transform.position,
        yRotation   = transform.eulerAngles.y,
        gold        = _gold,
        items       = _loot.Capture(),
    };

    public void RestoreState(SavedContainerState s, LootRegistry registry)
    {
        _rolled = s.rolled;
        _gold   = s.gold;
        _loot.Restore(s.items, registry);

        if (_rolled && _loot.SlotCount == 0 && _gold == 0)
        {
            _emptied = true;
            if (!isDropped && emptyBehavior == EmptyBehavior.Hide)
                gameObject.SetActive(false);
        }
    }

    // ── Dropped-bag spawning ──────────────────────────────────────────────────

    // Spawns the configured dropped-bag prefab at a position holding the given
    // items + gold (used for inventory overflow). Returns the new container.
    public static LootContainer SpawnDropped(Vector3 position, IEnumerable<(LootItem item, int count)> items, int gold)
    {
        var prefab = SaveSystem.Instance?.database?.droppedBagPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[LootContainer] No droppedBagPrefab assigned on GameDatabase — overflow items lost.");
            return null;
        }

        var bag = Instantiate(prefab, position, Quaternion.identity);
        bag.isDropped        = true;
        bag.emptyBehavior    = EmptyBehavior.Destroy;
        bag.containerId      = System.Guid.NewGuid().ToString("N");
        bag._rolled          = true;
        bag._gold            = Mathf.Max(0, gold);
        if (items != null)
            foreach (var (item, count) in items)
                if (item != null && count > 0) bag._loot.Add(item, count);

        SceneStateManager.Instance?.RegisterContainer(bag);
        return bag;
    }

    // Convenience: add an item to the player's inventory, dropping any overflow
    // in a bag at the player's feet. Use for external grants (quest rewards etc.).
    public static void GiveToPlayerOrDrop(LootItem item, int count)
    {
        if (item == null || count <= 0) return;
        var inv = InventorySystem.Instance;
        int leftover = inv != null ? inv.AddLootItem(item, count) : count;
        if (leftover <= 0) return;

        var player = PlayerManager.Instance?.Player;
        Vector3 pos = player != null ? player.transform.position : Vector3.zero;
        SpawnDropped(pos, new[] { (item, leftover) }, 0);
    }
}
