using UnityEngine;

// Reusable IMGUI "loot window" for any source backed by the shared Inventory
// class (NPC corpses now; spawnable containers later). Handles Take / Take All
// moving items into the player inventory while respecting its capacity — items
// that don't fit stay in the source. The "Inventory is full" toast surfaces
// automatically via InventorySystem.OnInventoryFull → ScreenNotifier.
//
// Usage (from a MonoBehaviour's OnGUI):
//   if (!LootWindowGUI.Draw(title, sourceLoot, ref gold, ref scroll)) Close();
public static class LootWindowGUI
{
    // Returns false when the player clicked Close (caller should stop drawing).
    public static bool Draw(string title, Inventory loot, ref int gold, ref Vector2 scroll)
    {
        const float PW = 420f, PH = 360f;
        float px = (Screen.width  - PW) * 0.5f;
        float py = (Screen.height - PH) * 0.5f;

        var panel = new Rect(px, py, PW, PH);
        // Solid opaque background so the game world never shows through.
        GUI.color = new Color(0.04f, 0.04f, 0.05f, 1f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.y + panel.height - 1f, panel.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 1f, panel.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x + panel.width - 1f, panel.y, 1f, panel.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Box(panel, "");
        GUI.Label(new Rect(px + 10f, py + 8f, PW - 50f, 28f),
            string.IsNullOrEmpty(title) ? "Loot" : title, Centered(17, FontStyle.Bold));
        if (GUI.Button(new Rect(px + PW - 36f, py + 6f, 28f, 26f), "X"))
            return false;

        var slots = loot.Slots;
        float listH  = PH - 100f;
        float rowH   = 34f;
        int   rows   = slots.Count + (gold > 0 ? 1 : 0);
        float totalH = Mathf.Max(rows * rowH, listH);

        var viewport = new Rect(px + 8f, py + 42f, PW - 16f, listH);
        var content  = new Rect(0, 0, PW - 32f, totalH);
        scroll = GUI.BeginScrollView(viewport, scroll, content);

        float iy = 0f;
        LootItem toTake = null;   // applied after the loop (don't mutate while iterating)

        foreach (var slot in slots)
        {
            GUI.Box(new Rect(0, iy, content.width, rowH - 2f), "");
            var nameStyle = new GUIStyle(GUI.skin.label);
            if (slot.item != null) nameStyle.normal.textColor = GameUI.RarityColor(slot.item.rarity);
            GUI.Label(new Rect(8f, iy + 6f, content.width - 80f, 22f),
                $"{slot.ItemName}  ×{slot.count}", nameStyle);
            if (GUI.Button(new Rect(content.width - 72f, iy + 4f, 66f, 24f), "Take"))
                toTake = slot.item;
            iy += rowH;
        }

        bool takeGold = false;
        if (gold > 0)
        {
            GUI.Box(new Rect(0, iy, content.width, rowH - 2f), "");
            GUI.Label(new Rect(8f, iy + 6f, content.width - 80f, 22f), $"Gold  ×{gold}");
            if (GUI.Button(new Rect(content.width - 72f, iy + 4f, 66f, 24f), "Take"))
                takeGold = true;
            iy += rowH;
        }

        if (slots.Count == 0 && gold == 0)
            GUI.Label(new Rect(0, iy + 10f, content.width, 24f), "Empty.", Centered(14, FontStyle.Normal));

        GUI.EndScrollView();

        if (toTake != null) TakeStack(loot, toTake);
        if (takeGold) { InventorySystem.Instance?.AddGold(gold); gold = 0; }

        bool stayOpen = true;
        float btnY = py + PH - 48f;
        GUI.skin.button.fontSize = 14;
        if (GUI.Button(new Rect(px + 12f, btnY, 130f, 36f), "Take All"))
        {
            TakeAll(loot, ref gold);
        }
        if (GUI.Button(new Rect(px + PW - 142f, btnY, 130f, 36f), "Close"))
            stayOpen = false;

        return stayOpen;
    }

    // Moves as much of one item stack into the player inventory as fits; the
    // remainder stays in the source.
    static void TakeStack(Inventory loot, LootItem item)
    {
        if (InventorySystem.Instance == null) return;
        int have     = loot.Count(item);
        int leftover = InventorySystem.Instance.AddLootItem(item, have);
        loot.Remove(item, have - leftover);
    }

    static void TakeAll(Inventory loot, ref int gold)
    {
        // Snapshot the distinct items first — TakeStack mutates the source.
        var items = new System.Collections.Generic.List<LootItem>();
        foreach (var slot in loot.Slots)
            if (slot.item != null && !items.Contains(slot.item)) items.Add(slot.item);
        foreach (var item in items) TakeStack(loot, item);

        if (gold > 0) { InventorySystem.Instance?.AddGold(gold); gold = 0; }
    }

    static GUIStyle Centered(int size, FontStyle style) => new(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontSize = size, fontStyle = style };
}
