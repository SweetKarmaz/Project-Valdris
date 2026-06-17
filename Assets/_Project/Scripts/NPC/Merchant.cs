using System;
using System.Collections.Generic;
using UnityEngine;

// Add to any NPC GameObject (alongside NpcController) to make it a merchant the
// player can trade with. Holds a stock Inventory and gold, both of which refill
// toward a baseline over time so the shop feels "alive". Prices derive from
// LootItem.goldValue, adjusted by markup/markdown and the player's Charisma.
//
// Interaction: InteractionHUD shows a "Trade" prompt on a living merchant and
// calls Open() on click. The shop window is drawn here in OnGUI.
[RequireComponent(typeof(NpcController))]
public class Merchant : MonoBehaviour
{
    [Serializable]
    public class StockEntry
    {
        [Tooltip("LootItem prefab sold by this merchant.")]
        public LootItem item;
        [Min(0), Tooltip("Quantity the merchant restocks toward.")]
        public int baselineQuantity = 1;
        [Min(0f), Tooltip("Seconds to restock one unit toward baseline. 0 = never restocks.")]
        public float restockSeconds = 0f;

        [NonSerialized] public float timer;
    }

    [Header("Stock")]
    public List<StockEntry> stock = new();

    [Header("Gold")]
    public int baselineGold = 200;
    [Min(0f), Tooltip("Seconds to add goldRestockAmount toward baseline. 0 = never.")]
    public float goldRestockSeconds = 8f;
    public int goldRestockAmount = 5;

    [Header("Pricing")]
    [Tooltip("Player BUYS at item value × this (merchant's markup).")]
    public float buyMarkup = 1.5f;
    [Tooltip("Player SELLS at item value × this (merchant's markdown).")]
    public float sellMarkdown = 0.5f;
    [Tooltip("Per Charisma point above 10: player buys cheaper / sells higher by this fraction.")]
    public float charismaPriceShift = 0.01f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    readonly Inventory _stock = new(100000);
    int   _gold;
    float _goldTimer;
    bool  _open;
    Vector2 _buyScroll, _sellScroll;

    NpcController _npc;
    PlayerStats   _playerStats;

    public bool CanTrade => _npc == null || !_npc.IsDead;

    // ── Persistence (called by NpcController save/restore) ────────────────────

    public int CurrentGold => _gold;
    public List<InventorySlotSave> CaptureStock() => _stock.Capture();

    public void RestoreState(List<InventorySlotSave> savedStock, int gold, LootRegistry registry)
    {
        _stock.Restore(savedStock, registry);
        _gold = gold;
    }

    void Awake()
    {
        _npc  = GetComponent<NpcController>();
        _gold = baselineGold;
        foreach (var e in stock)
            if (e.item != null && e.baselineQuantity > 0)
                _stock.Add(e.item, e.baselineQuantity);
    }

    // ── Open / close ────────────────────────────────────────────────────────

    public void Open()
    {
        if (!CanTrade || _open) return;
        // A corrupted player may be refused or attacked instead of traded with.
        if (_npc != null && _npc.CorruptionBlocksInteraction()) return;
        _open = true;
        UIModal.Push();
    }

    void Close()
    {
        if (!_open) return;
        _open = false;
        UIModal.Pop();
    }

    // ── Restock over time ───────────────────────────────────────────────────

    void Update()
    {
        // Keep facing the player while the shop is open (the interaction system
        // stops feeding attention once a modal window takes the cursor).
        if (_open)
        {
            var p = PlayerManager.Instance?.Player;
            if (p != null) _npc.AttendTo(p.transform);
        }

        foreach (var e in stock)
        {
            if (e.item == null || e.restockSeconds <= 0f) continue;
            if (_stock.Count(e.item) >= e.baselineQuantity) { e.timer = 0f; continue; }
            e.timer += Time.deltaTime;
            while (e.timer >= e.restockSeconds && _stock.Count(e.item) < e.baselineQuantity)
            {
                _stock.Add(e.item, 1);
                e.timer -= e.restockSeconds;
            }
        }

        if (_gold < baselineGold && goldRestockSeconds > 0f)
        {
            _goldTimer += Time.deltaTime;
            while (_goldTimer >= goldRestockSeconds && _gold < baselineGold)
            {
                _gold = Mathf.Min(baselineGold, _gold + goldRestockAmount);
                _goldTimer -= goldRestockSeconds;
            }
        }
    }

    // ── Pricing ─────────────────────────────────────────────────────────────

    float Charisma
    {
        get
        {
            if (_playerStats == null)
                _playerStats = PlayerManager.Instance?.Player != null
                    ? PlayerManager.Instance.Player.GetComponent<PlayerStats>() : null;
            return _playerStats != null ? _playerStats.Charisma : 10f;
        }
    }

    float BuyFactor  => Mathf.Clamp(1f - (Charisma - 10f) * charismaPriceShift, 0.5f, 1.5f);
    float SellFactor => Mathf.Clamp(1f + (Charisma - 10f) * charismaPriceShift, 0.5f, 1.5f);

    int BuyPrice(LootItem it)  => Mathf.Max(1, Mathf.CeilToInt(it.goldValue  * buyMarkup   * BuyFactor));
    int SellPrice(LootItem it) => Mathf.Max(0, Mathf.FloorToInt(it.goldValue * sellMarkdown * SellFactor));

    static bool IsSellable(LootItem it) =>
        it != null && it.itemType != LootItemType.KeyItem && it.itemType != LootItemType.QuestItem;

    // ── Transactions ────────────────────────────────────────────────────────

    void Buy(LootItem it)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || it == null || _stock.Count(it) <= 0) return;

        int price = BuyPrice(it);
        if (inv.Gold < price)          { ScreenNotifier.Show("Not enough gold");  return; }
        if (!inv.HasRoomFor(it, 1))    { ScreenNotifier.Show("Inventory is full"); return; }

        inv.SpendGold(price);
        _gold += price;
        _stock.Remove(it, 1);
        inv.AddLootItem(it, 1);
    }

    void Sell(LootItem it)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || !IsSellable(it) || inv.GetCount(it) <= 0) return;

        int price = SellPrice(it);
        if (_gold < price) { ScreenNotifier.Show("Merchant can't afford that"); return; }

        _gold -= price;
        inv.AddGold(price);
        inv.RemoveLootItem(it, 1);
        _stock.Add(it, 1);
    }

    // ── Shop window ───────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!_open) return;
        if (PauseMenuController.IsPaused || GameUI.IsOpen || _npc.IsDead) { Close(); return; }

        const float PW = 640f, PH = 440f;
        float px = (Screen.width  - PW) * 0.5f;
        float py = (Screen.height - PH) * 0.5f;
        var inv = InventorySystem.Instance;

        GUI.Box(new Rect(px, py, PW, PH), "");

        var titleStyle = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(px + 10f, py + 8f, PW - 50f, 24f), _npc.DisplayName, titleStyle);
        if (GUI.Button(new Rect(px + PW - 36f, py + 6f, 28f, 26f), "X")) { Close(); return; }

        var goldStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        goldStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
        GUI.Label(new Rect(px + 16f,        py + 34f, PW / 2f, 20f), $"Merchant Gold: {_gold}", goldStyle);
        GUI.Label(new Rect(px + PW / 2f,    py + 34f, PW / 2f - 20f, 20f),
            $"Your Gold: {(inv != null ? inv.Gold : 0)}", goldStyle);

        float colW   = (PW - 36f) * 0.5f;
        float listY  = py + 60f;
        float listH  = PH - 76f;
        float leftX  = px + 12f;
        float rightX = px + PW / 2f + 6f;

        // Draw BOTH columns (don't short-circuit), then resolve the hovered item.
        LootItem hovBuy  = DrawBuyColumn(leftX,  listY, colW, listH);
        LootItem hovSell = DrawSellColumn(rightX, listY, colW, listH, inv);

        // Tooltip drawn last so it sits above everything, in screen space.
        LootItem hovered = hovBuy != null ? hovBuy : hovSell;
        if (hovered != null) ItemTooltip.Draw(hovered, Event.current.mousePosition);
    }

    LootItem DrawBuyColumn(float x, float y, float w, float h)
    {
        GUI.Label(new Rect(x, y, w, 20f), "Buying", Header());
        var view = new Rect(x, y + 22f, w, h - 22f);

        var slots = _stock.Slots;
        const float rowH = 34f;
        var content = new Rect(0, 0, w - 18f, Mathf.Max(slots.Count * rowH, h - 22f));
        _buyScroll = GUI.BeginScrollView(view, _buyScroll, content);

        LootItem toBuy = null, hovered = null;
        float iy = 0f;
        foreach (var slot in slots)
        {
            var row = new Rect(0, iy, content.width, rowH - 2f);
            GUI.Box(row, "");
            var nameStyle = new GUIStyle(GUI.skin.label);
            if (slot.item != null) nameStyle.normal.textColor = GameUI.RarityColor(slot.item.rarity);
            GUI.Label(new Rect(6f, iy + 6f, content.width - 110f, 22f),
                $"{slot.ItemName}  ×{slot.count}", nameStyle);
            GUI.Label(new Rect(content.width - 104f, iy + 6f, 48f, 22f),
                $"{BuyPrice(slot.item)}g", GoldRight());
            if (GUI.Button(new Rect(content.width - 52f, iy + 4f, 48f, 24f), "Buy"))
                toBuy = slot.item;
            if (row.Contains(Event.current.mousePosition)) hovered = slot.item;
            iy += rowH;
        }
        if (slots.Count == 0)
            GUI.Label(new Rect(0, 8f, content.width, 22f), "Nothing for sale.", Centered());

        GUI.EndScrollView();
        if (toBuy != null) Buy(toBuy);
        return hovered;
    }

    LootItem DrawSellColumn(float x, float y, float w, float h, InventorySystem inv)
    {
        GUI.Label(new Rect(x, y, w, 20f), "Your Items", Header());
        var view = new Rect(x, y + 22f, w, h - 22f);

        var slots = inv != null ? inv.GetSlots() : null;
        const float rowH = 34f;
        int count = 0;
        if (slots != null) foreach (var s in slots) if (IsSellable(s.item)) count++;

        var content = new Rect(0, 0, w - 18f, Mathf.Max(count * rowH, h - 22f));
        _sellScroll = GUI.BeginScrollView(view, _sellScroll, content);

        LootItem toSell = null, hovered = null;
        float iy = 0f;
        if (slots != null)
            foreach (var slot in slots)
            {
                if (!IsSellable(slot.item)) continue;
                var row = new Rect(0, iy, content.width, rowH - 2f);
                GUI.Box(row, "");
                var nameStyle = new GUIStyle(GUI.skin.label);
                if (slot.item != null) nameStyle.normal.textColor = GameUI.RarityColor(slot.item.rarity);
                GUI.Label(new Rect(6f, iy + 6f, content.width - 110f, 22f),
                    $"{slot.ItemName}  ×{slot.count}", nameStyle);
                GUI.Label(new Rect(content.width - 104f, iy + 6f, 48f, 22f),
                    $"{SellPrice(slot.item)}g", GoldRight());
                if (GUI.Button(new Rect(content.width - 52f, iy + 4f, 48f, 24f), "Sell"))
                    toSell = slot.item;
                if (row.Contains(Event.current.mousePosition)) hovered = slot.item;
                iy += rowH;
            }
        if (count == 0)
            GUI.Label(new Rect(0, 8f, content.width, 22f), "Nothing to sell.", Centered());

        GUI.EndScrollView();
        if (toSell != null) Sell(toSell);
        return hovered;
    }

    static GUIStyle Header()
    {
        var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
        return s;
    }
    static GUIStyle Centered() => new(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
    static GUIStyle GoldRight()
    {
        var s = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 12 };
        s.normal.textColor = new Color(1f, 0.85f, 0.25f);
        return s;
    }
}
