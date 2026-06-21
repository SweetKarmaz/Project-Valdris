using System.Collections.Generic;
using UnityEngine;

// Persistent set of key names the player has collected. Looting a key adds its
// name here (permanently) and drops a single "Keys" token into the inventory;
// doors/containers query Has(name) to unlock and NEVER consume anything. Saved
// with the game so the ring survives reloads.
public class Keyring : MonoBehaviour
{
    public static Keyring Instance { get; private set; }

    readonly HashSet<string> _keys = new();
    LootItem _ringItem;

    public IReadOnlyCollection<string> Keys => _keys;
    public bool Has(string keyName) => !string.IsNullOrEmpty(keyName) && _keys.Contains(keyName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null) return;
        new GameObject("Keyring").AddComponent<Keyring>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Adds a key ability (idempotent) and makes sure the Keys token is in the inventory.
    public void Add(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return;
        bool isNew = _keys.Add(keyName);
        EnsureRingToken();
        if (isNew) ScreenNotifier.Show($"Added ‘{keyName}’ to your keys.");
    }

    public void Clear() => _keys.Clear();

    // ── Save / load ────────────────────────────────────────────────────────────
    public List<string> Capture() => new(_keys);
    public void Restore(List<string> names)
    {
        _keys.Clear();
        if (names != null) foreach (var n in names) if (!string.IsNullOrEmpty(n)) _keys.Add(n);
    }

    // ── Token item ───────────────────────────────────────────────────────────
    void EnsureRingToken()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;
        foreach (var s in inv.GetSlots())
            if (s.item != null && s.item.isKeyRing) return;   // already carried

        var ring = RingItem();
        if (ring != null) inv.AddLootItem(ring, 1);
    }

    LootItem RingItem()
    {
        if (_ringItem != null) return _ringItem;
        var go = Resources.Load<GameObject>("KeyRing/Keys");
        if (go != null) _ringItem = go.GetComponent<LootItem>();
        if (_ringItem == null) Debug.LogWarning("[Keyring] Keys token not found at Resources/KeyRing/Keys.");
        return _ringItem;
    }
}
