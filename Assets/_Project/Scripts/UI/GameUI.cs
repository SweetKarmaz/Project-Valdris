using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tabbed in-game UI: Inventory | Skills | Spells | Quests | Map
//
// Open / close:  TAB (toggles), ESC (closes)
// Jump to tab:   I = Inventory, K = Skills, L = Spells, J = Quests, M = Map
//
// [DefaultExecutionOrder(-10)] runs before PauseMenuController so ESC is
// consumed here first and PauseMenuController skips it.
[DefaultExecutionOrder(-10)]
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    // True for one frame when GameUI consumed the ESC press.
    public static bool EscConsumedByGameUI { get; private set; }

    enum Tab { Inventory, Character, Skills, Spells, Quests, Map }
    enum QuestSubTab { Current, Completed }

    Tab         _tab        = Tab.Inventory;
    QuestSubTab _questSubTab = QuestSubTab.Current;
    Vector2 _scrollInventory;
    Vector2 _scrollCharacter;
    Vector2 _scrollSkills;
    Vector2 _scrollSpells;
    Vector2 _scrollQuests;

    LootItem _selectedInventoryItem; // item currently "held" for equipping

    // Drag-and-drop: item being dragged from the grid, and the equip-slot hit
    // rects collected each frame so a drop can find its target.
    LootItem _dragItem;
    bool     _dragging;
    EquipSlot? _dragFromSlot;   // set when dragging out of an equipment slot
    readonly List<(EquipSlot slot, Rect rect)> _equipSlotRects = new();

    // ── Layout ────────────────────────────────────────────────────────────────

    const float PanelW = 960f;  // 16:9 design canvas
    const float PanelH = 540f;  // 960 × (9/16) = 540
    const float TabH   = 42f;

    const int MaxInventoryStacks = 1000;

    // Display label can differ from the enum name.
    // EquipSlot.Hips drives the hip mesh; the UI calls it "Legs".
    // EquipSlot.Legs drives the leg mesh;  the UI calls it "Boots".
    // The enum values and meshOverrides are never touched — label only.
    static readonly (EquipSlot slot, string label)[] LeftEquipSlots =
    {
        (EquipSlot.Head,     "Head"),
        (EquipSlot.Chest,    "Chest"),
        (EquipSlot.Hips,     "Legs"),
        (EquipSlot.Legs,     "Boots"),
        (EquipSlot.MainHand, "Main Hand"),
        (EquipSlot.Ring1,    "Ring 1"),
        (EquipSlot.Ring2,    "Ring 2"),
    };

    static readonly (EquipSlot slot, string label)[] RightEquipSlots =
    {
        (EquipSlot.Necklace,  "Necklace"),
        (EquipSlot.Back,      "Back"),
        (EquipSlot.Shoulders, "Shoulders"),
        (EquipSlot.Hands,     "Hands"),
        (EquipSlot.OffHand,   "Off Hand"),
        (EquipSlot.Ring3,     "Ring 3"),
        (EquipSlot.Ring4,     "Ring 4"),
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        EscConsumedByGameUI = false;

        if (!InGameplayScene()) return;

        // Only block OPENING while PauseMenuController has paused the game.
        // When GameUI itself is open (and it caused the pause), we must keep
        // processing input — otherwise the UI would be unresponsive.
        if (PauseMenuController.IsPaused && !IsOpen) return;

        // Don't let the player open the tabbed menu (inventory/map/etc.) while a
        // dialogue is running — it would let them break the conversation flow.
        if (!IsOpen && DialogueSystem.Instance != null && DialogueSystem.Instance.IsActive) return;

        // Same while a quest popup (accept/complete) is up.
        if (!IsOpen && QuestPopupSystem.IsOpen) return;

        // TAB — toggle open/close
        if (InputManager.GameMenuPressed)
        {
            IsOpen = !IsOpen;
            if (IsOpen) _tab = Tab.Inventory;
            ApplyCursor();
            return;
        }

        // I / K / L / J / M — open straight to that tab (or close if already on it)
        if (InputManager.OpenInventoryPressed) { OpenTo(Tab.Inventory); return; }
        if (InputManager.OpenCharacterPressed) { OpenTo(Tab.Character); return; }
        if (InputManager.OpenSkillsPressed)    { OpenTo(Tab.Skills);    return; }
        if (InputManager.OpenSpellsPressed)    { OpenTo(Tab.Spells);    return; }
        if (InputManager.OpenQuestsPressed)    { OpenTo(Tab.Quests);    return; }
        if (InputManager.OpenMapPressed)       { OpenTo(Tab.Map);       return; }

        // ESC — close
        if (IsOpen && InputManager.SkipPressed)
        {
            IsOpen = false;
            EscConsumedByGameUI = true;
            _selectedInventoryItem = null;
            ApplyCursor();
        }
    }

    void OpenTo(Tab tab)
    {
        bool alreadyOnTab = IsOpen && _tab == tab;
        if (alreadyOnTab)
        {
            IsOpen = false;
            _selectedInventoryItem = null;
        }
        else
        {
            IsOpen = true;
            _tab = tab;
        }
        ApplyCursor();
    }

    // ── IMGUI ─────────────────────────────────────────────────────────────────

    // Computes a scale and centered position for the panel.
    //
    // The panel is scaled to fill 75% of the screen's *constraining* dimension
    // while preserving the design aspect ratio (PanelW : PanelH). Centering is
    // derived from the actual rendered size (PanelW/H × uiScale) so that the
    // panel always lands in the middle of the screen regardless of resolution.
    //
    //   • Screen wider than the panel ratio: constrain by height → game world
    //     visible as columns left and right of the panel.
    //   • Screen narrower than the panel ratio (e.g. 4:3): constrain by width →
    //     game world visible as strips above and below the panel.
    static void ComputePanelRect(out float px, out float py, out float uiScale)
    {
        const float fill        = 0.75f;
        float       panelAspect = PanelW / PanelH;
        float       screenAspect = (float)Screen.width / Screen.height;

        float renderedW, renderedH;
        if (screenAspect >= panelAspect)
        {
            // Wider screen — fit to 75 % of height, derive width from panel ratio.
            renderedH = Screen.height * fill;
            renderedW = renderedH * panelAspect;
        }
        else
        {
            // Narrower screen — fit to 75 % of width, derive height from panel ratio.
            renderedW = Screen.width * fill;
            renderedH = renderedW / panelAspect;
        }

        uiScale = renderedW / PanelW;           // same as renderedH / PanelH
        px      = (Screen.width  - renderedW) * 0.5f;
        py      = (Screen.height - renderedH) * 0.5f;
    }

    void OnGUI()
    {
        if (!IsOpen) return;

        GUI.skin.button.fontSize = 15;
        GUI.skin.label.fontSize  = 14;

        // Dim background — drawn in raw screen space before the scale matrix.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        ComputePanelRect(out float px, out float py, out float uiScale);
        var   oldMatrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(uiScale, uiScale), new Vector2(px, py));

        // Solid opaque panel background so the game world never shows through.
        var panelRect = new Rect(px, py, PanelW, PanelH);
        GUI.color = new Color(0.04f, 0.04f, 0.05f, 1f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, panelRect.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y + panelRect.height - 1f, panelRect.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, 1f, panelRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panelRect.x + panelRect.width - 1f, panelRect.y, 1f, panelRect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Box(panelRect, "");

        DrawTabs(px, py);
        DrawCloseButton(px, py);
        DrawContent(px, py);

        GUI.matrix = oldMatrix;
    }

    void DrawTabs(float px, float py)
    {
        float tabW = 120f;
        Tab[]    tabs   = { Tab.Inventory, Tab.Character, Tab.Skills, Tab.Spells, Tab.Quests, Tab.Map };
        string[] labels = { "Inventory [I]", "Character [O]", "Skills [K]", "Spells [L]", "Quests [J]", "Map [M]" };

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = tabs[i] == _tab;
            var style   = active
                ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold }
                : GUI.skin.button;

            if (GUI.Button(new Rect(px + i * (tabW + 4f), py, tabW, TabH), labels[i], style))
                _tab = tabs[i];
        }
    }

    void DrawCloseButton(float px, float py)
    {
        if (GUI.Button(new Rect(px + PanelW - 38f, py + 5f, 32f, 32f), "X"))
        {
            IsOpen = false;
            _selectedInventoryItem = null;
            ApplyCursor();
        }
    }

    void DrawContent(float px, float py)
    {
        float contentY = py + TabH + 6f;
        float contentH = PanelH - TabH - 14f;
        float contentX = px + 8f;
        float contentW = PanelW - 16f;

        switch (_tab)
        {
            case Tab.Inventory: DrawInventory(contentX, contentY, contentW, contentH); break;
            case Tab.Character: DrawCharacter(contentX, contentY, contentW, contentH); break;
            case Tab.Skills:    DrawSkills(contentX, contentY, contentW, contentH);    break;
            case Tab.Spells:    DrawSpells(contentX, contentY, contentW, contentH);    break;
            case Tab.Quests:    DrawQuests(contentX, contentY, contentW, contentH);    break;
            case Tab.Map:       DrawMap(contentX, contentY, contentW, contentH);       break;
        }
    }

    // ── Inventory ─────────────────────────────────────────────────────────────
    // Layout (all coords in 960×540 design space, scaled at runtime):
    //
    //  ┌──────────────────────────────┬──────────────────────────────┐
    //  │  Left half (50%)             │  Right half (50%)            │
    //  │  ┌──────┬──────────┬──────┐  │  ┌──────────────────────┐   │
    //  │  │Equip │Character │Equip │  │  │  Inventory grid      │   │
    //  │  │ col  │ preview  │ col  │  │  │  5 cols × N rows     │   │
    //  │  │(25%) │  (50%)   │(25%) │  │  │  2/3 of height       │   │
    //  │  │      │  + gold  │      │  │  ├──────────────────────┤   │
    //  │  └──────┴──────────┴──────┘  │  │  Item detail pane    │   │
    //  │                              │  │  1/3 of height       │   │
    //  └──────────────────────────────┴──┴──────────────────────┘   │

    void DrawInventory(float x, float y, float w, float h)
    {
        _equipSlotRects.Clear();
        _invHover = null;
        float halfW = w * 0.5f;
        DrawInventoryLeft(x,          y, halfW, h);
        DrawInventoryRight(x + halfW, y, halfW, h);
        HandleDragAndDrop();

        // Rarity-coloured name tooltip for the hovered slot (hidden while dragging).
        if (_invHover != null && !_dragging) DrawItemNameTooltip(_invHover);
    }

    // The icon to show for an item: its assigned Sprite, else (in the editor) a
    // live prefab preview, else null (caller falls back to the name).
    static Texture ItemIcon(LootItem item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon.texture;
#if UNITY_EDITOR
        // Generated items are scene-instance clones; AssetPreview only works on
        // assets, so preview the base prefab they were cloned from instead.
        GameObject previewGo = item.gameObject;
        if (item.IsGenerated && item.runtimeRoll != null)
        {
            var basePrefab = SaveSystem.Instance?.database?.lootRegistry?
                .FindByName(item.runtimeRoll.basePrefabName);
            if (basePrefab != null) previewGo = basePrefab.gameObject;
        }
        return UnityEditor.AssetPreview.GetAssetPreview(previewGo);
#else
        return null;
#endif
    }

    // Item currently hovered in the inventory/equipment slots (name tooltip).
    LootItem _invHover;

    // Subtle dark slot background carrying a hint of the item's rarity colour.
    public static Color RaritySlotTint(ItemRarity r)
    {
        Color c = RarityColor(r);
        return new Color(0.10f + c.r * 0.10f, 0.10f + c.g * 0.10f, 0.12f + c.b * 0.10f, 0.9f);
    }

    // A small floating label showing an item's name in its rarity colour.
    void DrawItemNameTooltip(LootItem item)
    {
        if (item == null) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = false };
        style.normal.textColor = RarityColor(item.rarity);
        var content = new GUIContent(item.ItemName);
        Vector2 sz = style.CalcSize(content);
        const float pad = 6f;
        var m = Event.current.mousePosition;
        float tw = sz.x + pad * 2f, th = sz.y + pad;
        float tx = m.x + 14f, ty = m.y + 14f;
        var box = new Rect(tx, ty, tw, th);
        GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.95f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(RarityColor(item.rarity).r, RarityColor(item.rarity).g, RarityColor(item.rarity).b, 0.8f);
        GUI.DrawTexture(new Rect(tx, ty, tw, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(tx + pad, ty + pad * 0.5f, sz.x, sz.y), content, style);
    }

    // Quality-tier colour for item names.
    public static Color RarityColor(ItemRarity r) => r switch
    {
        ItemRarity.Uncommon  => new Color(0.45f, 0.95f, 0.45f),  // green
        ItemRarity.Rare      => new Color(0.40f, 0.65f, 1f),     // blue
        ItemRarity.Epic      => new Color(0.75f, 0.45f, 0.95f),  // purple
        ItemRarity.Legendary => new Color(1f,    0.6f,  0.2f),   // orange
        _                    => new Color(0.85f, 0.85f, 0.85f),  // common: gray
    };

    // A readable label for a stat, including resistance wording.
    public static string StatLabel(StatType s) => s switch
    {
        StatType.MaxHealth          => "Health",
        StatType.MaxMana            => "Mana",
        StatType.AttackDamage       => "Attack Damage",
        StatType.AttackSpeed        => "Attack Speed",
        StatType.Defense            => "Defense",
        StatType.MoveSpeed          => "Move Speed",
        StatType.PhysicalCritChance => "Physical Crit",
        StatType.SpellCritChance    => "Spell Crit",
        StatType.CritDamage         => "Crit Damage",
        StatType.FireResist         => "Fire resistance",
        StatType.IceResist          => "Frost resistance",
        StatType.LightningResist    => "Storm resistance",
        StatType.HolyResist         => "Holy resistance",
        StatType.CorruptionResist   => "Corruption resistance",
        _                           => s.ToString(),
    };

    // Multi-line breakdown of an item's actual stats (damage/defense, each
    // modifier, each elemental rider). Empty for plain items with no stats.
    static string BuildItemStats(LootItem item)
    {
        var lines = new List<string>();

        if (item.IsWeapon && item.weaponDamage != 0f)
            lines.Add($"{item.weaponDamage:0} physical damage" + (item.isTwoHanded ? "  (two-handed)" : ""));
        else if (!item.IsWeapon && item.armorValue != 0f)
            lines.Add($"+{item.armorValue:0} armor");

        if (item.statModifiers != null)
            foreach (var m in item.statModifiers)
            {
                string sign = m.amount >= 0 ? "+" : "";
                string val  = m.mode == ModifierMode.Percent ? $"{sign}{m.amount:0}%" : $"{sign}{m.amount:0}";
                lines.Add($"{val} {StatLabel(m.stat)}");
            }

        if (item.onHitEffects != null)
            foreach (var e in item.onHitEffects)
                if (e.damage > 0f) lines.Add($"+{e.damage:0} {e.type} damage");

        return string.Join("\n", lines);
    }

    // Draws the dragged item under the cursor and, on release, equips it if it
    // was dropped on a compatible equipment slot.
    void HandleDragAndDrop()
    {
        if (!_dragging || _dragItem == null) { _dragging = false; return; }

        var e = Event.current;

        const float s = 50f;
        var r = new Rect(e.mousePosition.x - s * 0.5f, e.mousePosition.y - s * 0.5f, s, s);
        Texture icon = ItemIcon(_dragItem);
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
        else GUI.Box(r, Truncate(_dragItem.ItemName, 8));
        GUI.color = Color.white;

        if (e.type == EventType.MouseUp)
        {
            if (_dragFromSlot.HasValue)
            {
                // Dragged out of an equipment slot → return it to the inventory,
                // wherever it was released.
                UnequipSlot(_dragFromSlot.Value);
            }
            else
            {
                // Dragged from the inventory grid → equip if dropped on a valid slot.
                foreach (var (slot, rect) in _equipSlotRects)
                    if (rect.Contains(e.mousePosition) && InventorySystem.CanEquipToSlot(_dragItem, slot))
                    {
                        if (InventorySystem.Instance?.EquipLootItem(_dragItem, slot) == true)
                            _selectedInventoryItem = null;
                        break;
                    }
            }

            _dragging     = false;
            _dragItem     = null;
            _dragFromSlot = null;
            e.Use();
        }
    }

    // Unequips a slot, handling the special thrown-weapon case for Main Hand.
    static void UnequipSlot(EquipSlot slot)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;
        if (slot == EquipSlot.MainHand && inv.GetEquippedLoot(slot) == null
            && inv.GetEquippedThrown() != null)
            inv.UnequipThrown();
        else
            inv.UnequipLootSlot(slot);
    }

    // ── Left half: paperdoll ──────────────────────────────────────────────────

    void DrawInventoryLeft(float x, float y, float w, float h)
    {
        float colW     = w * 0.25f;
        float previewW = w * 0.50f;

        DrawEquipmentColumn(x,                    y, colW,     h, LeftEquipSlots);
        DrawCharacterPreview(x + colW,            y, previewW, h);
        DrawEquipmentColumn(x + colW + previewW,  y, colW,     h, RightEquipSlots);
    }

    void DrawEquipmentColumn(float x, float y, float w, float h,
        (EquipSlot slot, string label)[] slots)
    {
        const float slotSize = 46f;
        const float labelH   = 12f;
        const float entryH   = slotSize + labelH + 6f;   // 64 px per entry

        float totalH  = slots.Length * entryH;
        float startY  = y + Mathf.Max(0f, (h - totalH) * 0.5f);  // vertically centred
        float centreX = x + (w - slotSize) * 0.5f;

        var labelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 9, alignment = TextAnchor.UpperCenter };
        var itemStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 8, wordWrap = true, alignment = TextAnchor.MiddleCenter };

        // The item currently "held" (selected or being dragged) — used to know
        // which slot(s) to highlight as a valid destination.
        LootItem holding = _dragItem ?? _selectedInventoryItem;

        var e = Event.current;

        for (int i = 0; i < slots.Length; i++)
        {
            var (equip, label) = slots[i];
            float sy      = startY + i * entryH;
            var   slotRect = new Rect(centreX, sy, slotSize, slotSize);

            _equipSlotRects.Add((equip, slotRect));

            var occupied = InventorySystem.Instance?.GetEquippedLoot(equip);
            // The Main Hand also displays a thrown weapon held there.
            if (occupied == null && equip == EquipSlot.MainHand)
                occupied = InventorySystem.Instance?.GetEquippedThrown();

            // Background: subtle rarity tint when occupied, dark otherwise.
            GUI.color = occupied != null
                ? RaritySlotTint(occupied.rarity)
                : new Color(0.12f, 0.12f, 0.12f, 0.9f);
            GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (occupied != null && slotRect.Contains(e.mousePosition)) _invHover = occupied;

            // Border: gold ONLY on the slot the held item can go into.
            bool validTarget = holding != null && InventorySystem.CanEquipToSlot(holding, equip);
            GUI.color = validTarget
                ? new Color(1f, 0.85f, 0.3f, 0.9f)
                : new Color(1f, 1f, 1f, 0.30f);
            DrawBorder(slotRect, validTarget ? 2f : 1f);
            GUI.color = Color.white;

            // Equipped item: icon if available, else its (truncated) name.
            if (occupied != null)
            {
                Texture icon = ItemIcon(occupied);
                if (icon != null)
                    GUI.DrawTexture(new Rect(centreX + 2f, sy + 2f, slotSize - 4f, slotSize - 4f),
                        icon, ScaleMode.ScaleToFit);
                else
                    GUI.Label(new Rect(centreX + 1f, sy + 1f, slotSize - 2f, slotSize - 2f),
                        Truncate(occupied.ItemName, 9), itemStyle);
            }

            // MouseDown: if occupied, begin a drag-out (release anywhere returns
            // it to the inventory); if empty, equip the selected item when valid.
            if (!_dragging && e.type == EventType.MouseDown && e.button == 0
                && slotRect.Contains(e.mousePosition))
            {
                if (occupied != null)
                {
                    _dragItem     = occupied;
                    _dragging     = true;
                    _dragFromSlot = equip;
                }
                else if (_selectedInventoryItem != null
                         && InventorySystem.CanEquipToSlot(_selectedInventoryItem, equip))
                {
                    if (InventorySystem.Instance?.EquipLootItem(_selectedInventoryItem, equip) == true)
                        _selectedInventoryItem = null;
                }
                e.Use();
            }

            GUI.Label(new Rect(x, sy + slotSize + 1f, w, labelH), label, labelStyle);
        }
    }

    void DrawCharacterPreview(float x, float y, float w, float h)
    {
        const float goldH  = 26f;
        const float margin = 6f;

        float imgX = x + margin;
        float imgY = y + margin;
        float imgW = w - margin * 2f;
        float imgH = h - margin * 2f - goldH;

        // Dark studio background.
        GUI.color = new Color(0.09f, 0.10f, 0.12f, 1f);
        GUI.DrawTexture(new Rect(imgX, imgY, imgW, imgH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        Texture previewTex = CharacterPreviewCamera.Instance?.PreviewTexture;
        if (previewTex != null)
        {
            GUI.DrawTexture(new Rect(imgX, imgY, imgW, imgH), previewTex, ScaleMode.ScaleToFit);
        }
        else
        {
            var ph = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 10, wordWrap = true };
            ph.normal.textColor = new Color(0.35f, 0.35f, 0.35f);
            GUI.Label(new Rect(imgX, imgY, imgW, imgH),
                "Character Preview\n(Add CharacterPreviewCamera\nto Persistent scene)", ph);
        }

        // Gold strip below the preview image.
        float goldY = imgY + imgH + 2f;
        int   gold  = InventorySystem.Instance?.Gold ?? 0;

        var goldLbl = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
        goldLbl.normal.textColor = new Color(1f, 0.85f, 0.25f);
        GUI.Label(new Rect(imgX, goldY + 4f, 36f, 18f), "Gold", goldLbl);

        var goldVal = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        goldVal.normal.textColor = Color.white;
        GUI.Label(new Rect(imgX + 40f, goldY + 4f, imgW - 40f, 18f), gold.ToString("N0"), goldVal);
    }

    // ── Right half: inventory grid + detail pane ──────────────────────────────

    void DrawInventoryRight(float x, float y, float w, float h)
    {
        const float sortH = 26f;
        DrawSortBar(x, y, w, sortH);

        float gridH   = (h - sortH) * 2f / 3f;
        float detailH = h - sortH - gridH;
        DrawInventoryGrid(x, y + sortH,          w, gridH);
        DrawItemDetail   (x, y + sortH + gridH,  w, detailH);
    }

    void DrawSortBar(float x, float y, float w, float h)
    {
        var lbl = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
        lbl.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, y, 36f, h), "Sort:", lbl);

        var modes = new[] { InventorySortMode.Name, InventorySortMode.Rarity,
                            InventorySortMode.Value, InventorySortMode.Type };
        float bx = x + 38f;
        float bw = Mathf.Min(70f, (w - 38f) / modes.Length - 4f);
        foreach (var m in modes)
        {
            if (GUI.Button(new Rect(bx, y + 2f, bw, h - 4f), m.ToString()))
                InventorySystem.Instance?.SortInventory(m);
            bx += bw + 4f;
        }
    }

    void DrawInventoryGrid(float x, float y, float w, float h)
    {
        const float slotSize   = 72f;
        const float gap        = 8f;
        const int   cols       = 5;
        const float scrollbarW = 16f;

        // Content width is narrower than the viewport so the scrollbar sits in
        // the remaining space and never overlaps the slot squares.
        float contentW  = w - scrollbarW;

        // Centre the grid horizontally within the content area.
        float gridWidth = cols * slotSize + (cols - 1) * gap;
        float padLeft   = Mathf.Max(0f, (contentW - gridWidth) * 0.5f);

        var  inv   = InventorySystem.Instance;
        var  slots = inv?.GetSlots();
        int  count = Mathf.Min(slots?.Count ?? 0, MaxInventoryStacks);
        int  rows  = Mathf.Max(4, Mathf.CeilToInt((float)count / cols));
        float rowH  = slotSize + gap;

        var viewport = new Rect(x, y, w, h);
        var content  = new Rect(0, 0, contentW, Mathf.Max(rows * rowH + gap, h));
        _scrollInventory = GUI.BeginScrollView(viewport, _scrollInventory, content);

        for (int i = 0; i < rows * cols; i++)
        {
            int   col = i % cols;
            int   row = i / cols;
            float sx  = padLeft + col * (slotSize + gap);
            float sy  = gap * 0.5f + row * rowH;
            var   sr  = new Rect(sx, sy, slotSize, slotSize);

            bool hasItem = i < count && slots != null && slots[i].item != null;

            // Slot background — subtle rarity tint behind an item, plain dark when empty.
            GUI.color = hasItem ? RaritySlotTint(slots[i].item.rarity)
                                : new Color(0.10f, 0.10f, 0.10f, 0.85f);
            GUI.DrawTexture(sr, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (i < count && slots != null)
            {
                var  invSlot  = slots[i];
                bool selected = invSlot.item == _selectedInventoryItem;

                // Border: green when selected, rarity color when occupied, dim white when empty.
                GUI.color = selected
                    ? new Color(0.25f, 0.95f, 0.25f, 1f)
                    : (invSlot.item != null ? RarityColor(invSlot.item.rarity) : new Color(1f, 1f, 1f, 0.22f));
                DrawBorder(sr, 2f);
                GUI.color = Color.white;

                // Prefab image if available, otherwise the (truncated) name.
                Texture icon = ItemIcon(invSlot.item);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(sx + 3f, sy + 3f, slotSize - 6f, slotSize - 6f),
                        icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    var nameStyle = new GUIStyle(GUI.skin.label)
                        { fontSize = 9, wordWrap = true, alignment = TextAnchor.LowerCenter };
                    nameStyle.normal.textColor = selected
                        ? new Color(0.5f, 1f, 0.5f)
                        : new Color(0.82f, 0.82f, 0.82f);
                    GUI.Label(new Rect(sx + 2f, sy + 2f, slotSize - 4f, slotSize - 4f),
                        Truncate(invSlot.ItemName, 11), nameStyle);
                }

                // Stack count badge (bottom-right corner).
                if (invSlot.count > 1)
                {
                    var cntStyle = new GUIStyle(GUI.skin.label)
                        { fontSize = 11, fontStyle = FontStyle.Bold,
                          alignment = TextAnchor.LowerRight };
                    cntStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(sx, sy, slotSize - 3f, slotSize - 2f),
                        invSlot.count.ToString(), cntStyle);
                }

                // Gold-value overlay (bottom-left, on a small dark pill for legibility).
                if (invSlot.item.goldValue > 0)
                {
                    string vtxt = $"{invSlot.item.goldValue}g";
                    var valStyle = new GUIStyle(GUI.skin.label)
                        { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
                    valStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
                    float vw = valStyle.CalcSize(new GUIContent(vtxt)).x + 4f;
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(new Rect(sx + 2f, sy + slotSize - 14f, vw, 12f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(sx + 4f, sy + slotSize - 15f, vw, 12f), vtxt, valStyle);
                }

                if (sr.Contains(Event.current.mousePosition)) _invHover = invSlot.item;

                // Mouse down selects the item and begins a potential drag.
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && sr.Contains(e.mousePosition))
                {
                    _selectedInventoryItem = selected ? null : invSlot.item;
                    _dragItem = invSlot.item;
                    _dragging = true;
                    e.Use();
                }
            }
            else
            {
                // Empty slot — faint border only.
                GUI.color = new Color(1f, 1f, 1f, 0.10f);
                DrawBorder(sr, 1f);
                GUI.color = Color.white;
            }
        }

        GUI.EndScrollView();
    }

    void DrawItemDetail(float x, float y, float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        DrawBorder(new Rect(x, y, w, h), 1f);
        GUI.color = Color.white;

        if (_selectedInventoryItem != null)
        {
            var item = _selectedInventoryItem;
            var nameStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, fontSize = 15 };
            nameStyle.normal.textColor = RarityColor(item.rarity);
            GUI.Label(new Rect(x + 10f, y + 8f, w - 20f, 24f), item.ItemName, nameStyle);

            // Where it equips (helps spot mis-tagged items, e.g. a bracelet on Chest).
            var slot = InventorySystem.GetEquipSlot(item);
            string equipWhere = InventorySystem.IsRing(item) ? "Ring (any slot)"
                              : slot.HasValue ? slot.Value.ToString() : null;
            string equipLine = $"{item.rarity}  •  {item.itemType}"
                + (equipWhere != null ? $"  •  {equipWhere}" : "")
                + $"  •  {item.goldValue} gold";

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            subStyle.normal.textColor = new Color(0.7f, 0.8f, 0.95f);
            GUI.Label(new Rect(x + 10f, y + 32f, w - 20f, 20f), equipLine, subStyle);

            // Explicit stat block: base damage/defense, each modifier, each rider.
            var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            statStyle.normal.textColor = new Color(0.75f, 0.95f, 0.75f);
            string stats = BuildItemStats(item);
            float statsBottom = y + 52f;
            if (stats.Length > 0)
            {
                float sh = statStyle.CalcHeight(new GUIContent(stats), w - 20f);
                GUI.Label(new Rect(x + 10f, y + 52f, w - 20f, sh), stats, statStyle);
                statsBottom += sh + 4f;
            }

            var bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12, richText = true };
            bodyStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            if (!string.IsNullOrWhiteSpace(item.flavorText))
                GUI.Label(new Rect(x + 10f, statsBottom, w - 20f, h - (statsBottom - y) - 8f),
                    $"<i>{item.flavorText}</i>", bodyStyle);
        }
        else
        {
            var ph = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Italic };
            ph.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUI.Label(new Rect(x, y, w, h), "Select an item to view details", ph);
        }
    }

    // ── Character ───────────────────────────────────────────────────────────────
    // Attributes (base → effective, what each affects) on the left; resources and
    // combat-derived stats on the right. Each stat shows its modifiers and the
    // source (gear / buff / skill) where any apply.

    // Source breakdown captured while a stat row is hovered (drawn after the
    // scroll view, where mouse coords are back in screen space).
    string _statHover;

    void DrawCharacter(float x, float y, float w, float h)
    {
        _statHover = null;

        // Header: character name and level, each centred on a third of the width
        // so they sit equidistant from one another and the panel edges.
        var header = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
        int level = LevelSystem.Instance != null ? LevelSystem.Instance.CurrentLevel : 1;
        float third = w / 3f;
        GUI.Label(new Rect(x + third * 0.5f, y + 2f, third, 26f), "Cael Varen", header);
        GUI.Label(new Rect(x + third * 1.5f, y + 2f, third, 26f), $"Level: {level}", header);

        var player = PlayerManager.Instance?.Player;
        var stats  = player != null ? player.GetComponent<PlayerStats>()   : null;
        var buffs  = player != null ? player.GetComponent<CharacterBuffs>() : null;

        float listY = y + 30f, listH = h - 30f;
        if (stats == null)
        {
            var empty = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, fontSize = 14 };
            empty.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            GUI.Label(new Rect(x, listY, w, listH), "No character data.", empty);
            return;
        }

        const float colGap = 16f;
        float colW  = (w - colGap * 2f) / 3f;
        float midX0 = colW + colGap;            // center column
        float rightX0 = (colW + colGap) * 2f;   // right column

        // Reserve strips at the bottom for the Corruption + XP bars (outside the scroll).
        const float corruptStripH = 48f;
        const float xpStripH      = 46f;
        float scrollH = Mathf.Max(80f, listH - corruptStripH - xpStripH);

        var viewport = new Rect(x, listY, w, scrollH);
        var content  = new Rect(0, 0, w - 20f, Mathf.Max(scrollH, 520f));
        _scrollCharacter = GUI.BeginScrollView(viewport, _scrollCharacter, content);

        // ── Left: attributes ──
        float cy = 0f;
        int attrPts = LevelSystem.Instance != null ? LevelSystem.Instance.UnspentAttributePoints : 0;
        StatSection(0f, ref cy, colW, attrPts > 0 ? $"Attributes  ({attrPts} pt)" : "Attributes");
        AttrRow(0f, ref cy, colW, "Strength",     stats.strength,     stats.Strength,     StatType.Strength,     buffs, "Melee / physical damage, defense");
        AttrRow(0f, ref cy, colW, "Dexterity",    stats.dexterity,    stats.Dexterity,    StatType.Dexterity,    buffs, "Attack speed, physical crit");
        AttrRow(0f, ref cy, colW, "Constitution", stats.constitution, stats.Constitution, StatType.Constitution, buffs, "Maximum health");
        AttrRow(0f, ref cy, colW, "Intelligence", stats.intelligence, stats.Intelligence, StatType.Intelligence, buffs, "Maximum mana");
        AttrRow(0f, ref cy, colW, "Wisdom",       stats.wisdom,       stats.Wisdom,       StatType.Wisdom,       buffs, "Mana; slows corruption gain");
        AttrRow(0f, ref cy, colW, "Charisma",     stats.charisma,     stats.Charisma,     StatType.Charisma,     buffs, "Vendor prices, dialogue");
        AttrRow(0f, ref cy, colW, "Spell Acuity", stats.spellAcuity,  stats.SpellAcuity,  StatType.SpellAcuity,  buffs, "Spell power, spell crit");

        // ── Center: vitals + combat ──
        cy = 0f;
        StatSection(midX0, ref cy, colW, "Vitals");
        PlainRow(midX0, ref cy, colW, "Health", $"{stats.CurrentHealth:0} / {stats.MaxHealth:0}", StatType.MaxHealth, buffs);
        PlainRow(midX0, ref cy, colW, "Mana",   $"{stats.CurrentMana:0} / {stats.MaxMana:0}",     StatType.MaxMana,   buffs);
        DerivedRow(midX0, ref cy, colW, "Health Regen", stats.HealthRegen, "0.0", StatType.HealthRegen, buffs, "/5s");
        DerivedRow(midX0, ref cy, colW, "Mana Regen",   stats.ManaRegen,   "0.0", StatType.ManaRegen,   buffs, "/5s");

        StatSection(midX0, ref cy, colW, "Combat");
        DerivedRow(midX0, ref cy, colW, "Attack Damage",  stats.AttackDamage,            "0.#", StatType.AttackDamage,      buffs);
        DerivedRow(midX0, ref cy, colW, "Attack Speed",   stats.AttackSpeed,             "0.##", StatType.AttackSpeed,      buffs);
        DerivedRow(midX0, ref cy, colW, "Defense",        stats.Defense,                 "0.#", StatType.Defense,           buffs);
        DerivedRow(midX0, ref cy, colW, "Physical Crit",  stats.PhysicalCritChance,      "0.#", StatType.PhysicalCritChance, buffs, "%");
        DerivedRow(midX0, ref cy, colW, "Spell Crit",     stats.SpellCritChance,         "0.#", StatType.SpellCritChance,   buffs, "%");
        DerivedRow(midX0, ref cy, colW, "Crit Damage",    stats.CritMultiplier,          "0.##", StatType.CritDamage,       buffs, "x");
        // Acuity-driven value (no direct modifier source to list).
        PlainRow(midX0, ref cy, colW, "Spell Power",      $"{stats.SpellPowerMultiplier:0.##}x",            StatType.SpellAcuity, null);

        // ── Right: resistances (percent of incoming damage reduced) ──
        cy = 0f;
        StatSection(rightX0, ref cy, colW, "Resistances");
        PlainRow(rightX0, ref cy, colW, "Fire",       $"{stats.ResistancePercent(DamageType.Fire):0}%",       StatType.FireResist,       buffs);
        PlainRow(rightX0, ref cy, colW, "Ice",        $"{stats.ResistancePercent(DamageType.Ice):0}%",        StatType.IceResist,        buffs);
        PlainRow(rightX0, ref cy, colW, "Lightning",  $"{stats.ResistancePercent(DamageType.Lightning):0}%",  StatType.LightningResist,  buffs);
        PlainRow(rightX0, ref cy, colW, "Holy",       $"{stats.ResistancePercent(DamageType.Holy):0}%",       StatType.HolyResist,       buffs);
        PlainRow(rightX0, ref cy, colW, "Corruption", $"{stats.ResistancePercent(DamageType.Corruption):0}%", StatType.CorruptionResist, buffs);

        GUI.EndScrollView();

        DrawCorruptionBar(x, listY + scrollH + 4f, w, corruptStripH - 6f);
        DrawXpBar(x, listY + scrollH + corruptStripH + 4f, w, xpStripH - 6f);

        // Stat-source tooltip (captured during row drawing inside the scroll).
        if (_statHover != null) DrawStatHoverTooltip(_statHover, x, w);
    }

    // XP progress toward the next level, with a hover showing current/required XP.
    void DrawXpBar(float x, float y, float w, float h)
    {
        var ls = LevelSystem.Instance;
        if (ls == null) return;

        bool  max  = ls.IsMaxLevel;
        long  into = ls.XpIntoCurrentLevel;
        long  need = ls.XpForCurrentLevel;
        float norm = max ? 1f : ls.LevelProgress;

        var lbl = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
        lbl.normal.textColor = new Color(0.55f, 0.85f, 1f);
        GUI.Label(new Rect(x, y, w, 16f),
            max ? "Max Level" : $"XP to next level: {ls.XpToNextLevel:N0}", lbl);

        float by = y + 17f, bh = h - 17f;
        var barRect = new Rect(x, by, w, bh);

        GUI.color = new Color(0.08f, 0.11f, 0.15f, 1f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.55f, 0.95f, 1f);
        GUI.DrawTexture(new Rect(x, by, w * norm, bh), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.20f);
        GUI.DrawTexture(new Rect(x, by, w, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Hover anywhere over the label + bar shows "current / required" for this level.
        if (!max && barRect.Contains(Event.current.mousePosition))
        {
            string text = $"{into:N0} XP out of {need:N0} XP";
            var ts = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            ts.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            const float tw = 240f, pad = 8f;
            float th = 20f;
            float tx = Mathf.Clamp(Event.current.mousePosition.x - tw * 0.5f, x, x + w - tw);
            float ty = y - th - pad * 2f - 4f;
            var box = new Rect(tx, ty, tw, th + pad * 2f);
            GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.55f, 0.95f, 0.9f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(tx + pad, ty + pad, tw - pad * 2f, th), text, ts);
        }
    }

    void DrawStatHoverTooltip(string text, float clampX, float clampW)
    {
        var ts = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        ts.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        const float tw = 240f, pad = 7f;
        float th = ts.CalcHeight(new GUIContent(text), tw - pad * 2f);
        var m = Event.current.mousePosition;
        float tx = Mathf.Clamp(m.x + 14f, clampX, clampX + clampW - tw);
        float ty = m.y + 14f;
        var box = new Rect(tx, ty, tw, th + pad * 2f);
        GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.97f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.6f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(tx + pad, ty + pad, tw - pad * 2f, th), text, ts);
    }

    // Records (for the post-scroll tooltip) the named per-source breakdown of a
    // stat when its row is hovered. Mouse coords here are scroll-content space.
    void CaptureStatHover(float x, float top, float w, float bottom, StatType stat, CharacterBuffs buffs)
    {
        if (!new Rect(x, top, w, bottom - top).Contains(Event.current.mousePosition)) return;
        string bd = BuildSourceBreakdown(stat, buffs);
        if (bd.Length > 0) _statHover = bd;
    }

    // Names each individual contributor (equipped item / buff / skills) to a stat.
    static string BuildSourceBreakdown(StatType stat, CharacterBuffs buffs)
    {
        var lines = new List<string>();
        string Mod(StatModifier m) =>
            (m.amount >= 0 ? "+" : "") + (m.mode == ModifierMode.Percent ? $"{m.amount:0.#}%" : $"{m.amount:0.#}");

        var inv = InventorySystem.Instance;
        if (inv != null)
            foreach (var kv in inv.GetAllEquippedLoot())
            {
                var it = kv.Value;
                if (it == null) continue;
                if (stat == StatType.Defense && it.armorValue != 0f)
                    lines.Add($"{it.ItemName}: +{it.armorValue:0}");
                if (it.statModifiers != null)
                    foreach (var m in it.statModifiers)
                        if (m.stat == stat) lines.Add($"{it.ItemName}: {Mod(m)}");
            }

        if (buffs != null)
            foreach (var ab in buffs.Active)
            {
                if (ab?.data?.modifiers == null) continue;
                if (ab.data.isHidden && !ab.revealed) continue;
                foreach (var m in ab.data.modifiers)
                    if (m.stat == stat) lines.Add($"{ab.data.buffName}: {Mod(m)}");
            }

        var sk = SkillSystem.Instance;
        if (sk != null)
        {
            float f = sk.TotalFlat(stat), p = sk.TotalPercent(stat);
            if (Mathf.Abs(f) > 0.01f) lines.Add($"Skills: {(f >= 0 ? "+" : "")}{f:0.#}");
            if (Mathf.Abs(p) > 0.01f) lines.Add($"Skills: {(p >= 0 ? "+" : "")}{p:0.#}%");
        }

        return string.Join("\n", lines);
    }

    const string CorruptionHelp =
        "Causing Corruption damage and certain tainted zones increase your Corruption " +
        "level – you must visit a shrine (or cleansing ground) to lower it. Some NPCs " +
        "won't deal with you, or will attack on sight, if it climbs too high. High " +
        "Wisdom slows how fast it fills.";

    void DrawCorruptionBar(float x, float y, float w, float h)
    {
        var ct   = CorruptionTracker.Instance;
        float pct  = ct != null ? Mathf.Clamp(ct.Percent, 0f, 100f) : 0f;
        float norm = ct != null ? Mathf.Clamp01(ct.Normalized)      : 0f;

        var lbl = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
        lbl.normal.textColor = new Color(0.78f, 0.55f, 0.95f);
        GUI.Label(new Rect(x, y, w, 16f), $"Corruption: {pct:0}%", lbl);

        float by = y + 17f, bh = h - 17f;
        var barRect = new Rect(x, by, w, bh);

        GUI.color = new Color(0.12f, 0.09f, 0.15f, 1f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.55f, 0.18f, 0.80f, 1f);
        GUI.DrawTexture(new Rect(x, by, w * norm, bh), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.20f);
        GUI.DrawTexture(new Rect(x, by, w, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Hover anywhere over the label + bar shows the explanatory tooltip (above the bar).
        var hover = new Rect(x, y, w, h);
        if (hover.Contains(Event.current.mousePosition))
        {
            var ts = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            ts.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            const float tw = 300f, pad = 8f;
            float th = ts.CalcHeight(new GUIContent(CorruptionHelp), tw - pad * 2f);
            float tx = Mathf.Clamp(Event.current.mousePosition.x, x, x + w - tw);
            float ty = y - th - pad * 2f - 4f;
            var box = new Rect(tx, ty, tw, th + pad * 2f);
            GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.18f, 0.80f, 0.9f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(tx + pad, ty + pad, tw - pad * 2f, th), CorruptionHelp, ts);
        }
    }

    void StatSection(float x, ref float cy, float w, string title)
    {
        cy += 4f;
        var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        s.normal.textColor = new Color(1f, 0.85f, 0.4f);
        GUI.Label(new Rect(x, cy, w, 20f), title, s);
        cy += 22f;
    }

    void AttrRow(float x, ref float cy, float w, string label, float baseVal, float effVal,
        StatType stat, CharacterBuffs buffs, string affects)
    {
        float top = cy;
        var name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
        GUI.Label(new Rect(x, cy, w, 18f), $"{label}: {effVal:0.#}", name);

        // "+" to spend an attribute point on this attribute (when any are unspent).
        if (LevelSystem.Instance != null && LevelSystem.Instance.UnspentAttributePoints > 0
            && GUI.Button(new Rect(x + w - 22f, cy - 1f, 20f, 18f), "+"))
        {
            var ps = PlayerManager.Instance?.Player != null
                ? PlayerManager.Instance.Player.GetComponent<PlayerStats>() : null;
            if (ps != null && LevelSystem.Instance.SpendAttributePoints(1)) ps.RaiseAttribute(stat);
        }
        cy += 17f;

        string mods = BuildModString(stat, buffs);
        bool changed = Mathf.Abs(effVal - baseVal) > 0.01f;
        if (changed || mods.Length > 0)
        {
            var small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            small.normal.textColor = new Color(0.7f, 0.8f, 0.95f);
            string detail = $"base {baseVal:0.#}" + (mods.Length > 0 ? $"  ({mods})" : "");
            GUI.Label(new Rect(x + 8f, cy, w - 8f, 16f), detail, small);
            cy += 15f;
        }

        var aff = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic };
        aff.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
        GUI.Label(new Rect(x + 8f, cy, w - 8f, 16f), "Affects: " + affects, aff);
        cy += 20f;
        CaptureStatHover(x, top, w, cy, stat, buffs);
    }

    void DerivedRow(float x, ref float cy, float w, string label, float value, string fmt,
        StatType stat, CharacterBuffs buffs, string suffix = "")
    {
        float top = cy;
        var name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
        string shown = suffix == "x" ? $"{value.ToString(fmt)}x" : $"{value.ToString(fmt)}{suffix}";
        GUI.Label(new Rect(x, cy, w, 18f), $"{label}: {shown}", name);
        cy += 17f;

        string mods = BuildModString(stat, buffs);
        if (mods.Length > 0)
        {
            var small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            small.normal.textColor = new Color(0.7f, 0.8f, 0.95f);
            GUI.Label(new Rect(x + 8f, cy, w - 8f, 16f), "(" + mods + ")", small);
            cy += 15f;
        }
        cy += 5f;
        CaptureStatHover(x, top, w, cy, stat, buffs);
    }

    void PlainRow(float x, ref float cy, float w, string label, string value,
        StatType stat, CharacterBuffs buffs)
    {
        float top = cy;
        var name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
        GUI.Label(new Rect(x, cy, w, 18f), $"{label}: {value}", name);
        cy += 17f;

        if (buffs != null)
        {
            string mods = BuildModString(stat, buffs);
            if (mods.Length > 0)
            {
                var small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                small.normal.textColor = new Color(0.7f, 0.8f, 0.95f);
                GUI.Label(new Rect(x + 8f, cy, w - 8f, 16f), "(" + mods + ")", small);
                cy += 15f;
            }
        }
        cy += 5f;
        CaptureStatHover(x, top, w, cy, stat, buffs);
    }

    // Builds a "+5 gear, +10% buff, …" string of the modifiers acting on a stat,
    // by source. Empty if nothing modifies it.
    static string BuildModString(StatType stat, CharacterBuffs buffs)
    {
        var parts = new List<string>();
        void Add(string src, float flat, float pct)
        {
            if (Mathf.Abs(flat) > 0.001f) parts.Add($"{(flat >= 0 ? "+" : "")}{flat:0.#} {src}");
            if (Mathf.Abs(pct)  > 0.001f) parts.Add($"{(pct  >= 0 ? "+" : "")}{pct:0.#}% {src}");
        }

        var inv = InventorySystem.Instance;
        Add("gear",  inv != null ? inv.GearFlat(stat) : 0f,  inv != null ? inv.GearPercent(stat) : 0f);
        Add("buff",  buffs != null ? buffs.TotalFlat(stat) : 0f, buffs != null ? buffs.TotalPercent(stat) : 0f);
        Add("skill", SkillSystem.Instance != null ? SkillSystem.Instance.TotalFlat(stat) : 0f,
                     SkillSystem.Instance != null ? SkillSystem.Instance.TotalPercent(stat) : 0f);

        return string.Join(", ", parts);
    }

    // ── Skills ────────────────────────────────────────────────────────────────
    // Shows every skill in the game. Unlocked skills appear at full brightness;
    // locked skills are grayed out so players can plan ahead.
    // Skill unlock selection happens in a separate level-up popup (LevelUpUI),
    // not here — this panel is a reference view only.

    void DrawSkills(float x, float y, float w, float h)
    {
        var header = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
        int points = LevelSystem.Instance != null ? LevelSystem.Instance.UnspentSkillPoints : 0;
        GUI.Label(new Rect(x, y + 2f, w, 26f), $"Skills — {points} point{(points == 1 ? "" : "s")} available", header);

        float listY = y + 30f, listH = h - 30f;

        var skills = SaveSystem.Instance?.database?.skills;
        if (skills == null || skills.Length == 0)
        {
            var empty = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Italic };
            empty.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            GUI.Label(new Rect(x, listY, w, listH), "No skills registered (populate GameDatabase.skills).", empty);
            return;
        }

        var ss = SkillSystem.Instance;
        const float rowH = 56f;
        var viewport = new Rect(x, listY, w, listH);
        var content  = new Rect(0, 0, w - 20f, Mathf.Max(skills.Length * rowH, listH));
        _scrollSkills = GUI.BeginScrollView(viewport, _scrollSkills, content);

        var nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        descStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        for (int i = 0; i < skills.Length; i++)
        {
            var skill = skills[i];
            if (skill == null) continue;
            float iy = i * rowH;

            int rank = ss != null ? ss.GetRank(skill) : 0;
            int max  = Mathf.Max(1, skill.maxRank);
            GUI.Label(new Rect(0, iy, content.width - 90f, 20f),
                $"{skill.skillName}  ({rank}/{max})", nameStyle);
            if (!string.IsNullOrEmpty(skill.description))
                GUI.Label(new Rect(8f, iy + 20f, content.width - 100f, 30f), skill.description, descStyle);

            bool can = ss != null && ss.CanUnlock(skill);
            GUI.enabled = can;
            string btn = (ss != null && ss.IsMaxed(skill)) ? "Max"
                       : skill.pointCost > 1 ? $"+ ({skill.pointCost})" : "+";
            if (GUI.Button(new Rect(content.width - 84f, iy + 10f, 78f, 30f), btn) && can)
                ss.UnlockSkill(skill);
            GUI.enabled = true;

            GUI.Box(new Rect(0, iy + rowH - 4f, content.width, 1f), "");
        }

        GUI.EndScrollView();
    }

    // ── Spells ────────────────────────────────────────────────────────────────
    // Shows only the spells the player has already unlocked (learned).
    // Locked spells are hidden entirely — the mystery of what might be learnable
    // is intentional for spells, unlike skills which show a full road-map.

    void DrawSpells(float x, float y, float w, float h)
    {
        var headerStyle = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
        GUI.Label(new Rect(x, y + 2f, w, 26f), "Spells", headerStyle);

        float listY = y + 30f;
        float listH = h - 30f;

        var spells = SpellbookSystem.Instance?.GetSpells();

        if (spells == null || spells.Count == 0)
        {
            var empty = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Italic };
            empty.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            GUI.Label(new Rect(x, listY, w, listH), "No spells learned yet.", empty);
            return;
        }

        const float rowH = 52f;
        float totalH = Mathf.Max(spells.Count * rowH, listH);
        var viewport = new Rect(x, listY, w, listH);
        var content  = new Rect(0, 0, w - 20f, totalH);
        _scrollSpells = GUI.BeginScrollView(viewport, _scrollSpells, content);

        var nameStyle = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, fontSize = 15 };
        var descStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 12, wordWrap = true };
        descStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

        // Slot assignments — which hotbar slots hold which spells.
        SpellData[] equippedSlots = new SpellData[SpellbookSystem.SlotCount];
        if (SpellbookSystem.Instance != null)
            for (int s = 0; s < SpellbookSystem.SlotCount; s++)
                equippedSlots[s] = SpellbookSystem.Instance.GetSlot(s);

        for (int i = 0; i < spells.Count; i++)
        {
            var    spell = spells[i];
            float  iy    = i * rowH;
            float  cw    = content.width;

            // Row background
            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(new Rect(0, iy, cw, rowH - 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(8f, iy + 6f, cw - 100f, 22f), spell.spellName, nameStyle);

            if (!string.IsNullOrEmpty(spell.description))
                GUI.Label(new Rect(8f, iy + 26f, cw - 100f, 20f), spell.description, descStyle);

            // Show which hotbar slot this spell is on (1-4), if any.
            string slotHint = "";
            for (int s = 0; s < equippedSlots.Length; s++)
                if (equippedSlots[s] == spell) { slotHint = $"[{s + 1}]"; break; }

            if (!string.IsNullOrEmpty(slotHint))
            {
                var slotStyle = new GUIStyle(GUI.skin.label)
                    { alignment = TextAnchor.MiddleRight, fontSize = 13, fontStyle = FontStyle.Bold };
                slotStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(cw - 96f, iy + 6f, 88f, 22f), $"Slot {slotHint}", slotStyle);
            }
        }

        GUI.EndScrollView();
    }

    // ── Quests ────────────────────────────────────────────────────────────────

    void DrawQuests(float x, float y, float w, float h)
    {
        float subTabW = 120f;
        QuestSubTab[] subTabs  = { QuestSubTab.Current, QuestSubTab.Completed };
        string[]      subLabels = { "Current", "Completed" };

        for (int i = 0; i < subTabs.Length; i++)
        {
            var style = subTabs[i] == _questSubTab
                ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold }
                : GUI.skin.button;
            if (GUI.Button(new Rect(x + i * (subTabW + 4f), y, subTabW, 32f), subLabels[i], style))
                _questSubTab = subTabs[i];
        }

        float listY = y + 38f;
        float listH = h - 38f;

        if (_questSubTab == QuestSubTab.Current)
            DrawCurrentQuests(x, listY, w, listH);
        else
            DrawCompletedQuests(x, listY, w, listH);
    }

    void DrawCurrentQuests(float x, float y, float w, float h)
    {
        if (QuestSystem.Instance == null) return;
        var quests = QuestSystem.Instance.GetActiveQuests();

        if (quests.Count == 0)
        {
            GUI.Label(new Rect(x, y + 20f, w, 30f), "No active quests.",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            return;
        }

        Vector2 screenMouse = Event.current.mousePosition;   // captured before the scroll offsets it
        QuestData hover = null;

        float rowH = 0f;
        foreach (var state in quests)
        {
            rowH += 56f;
            int objCount = state.data.objectives != null ? state.data.objectives.Count : 0;
            rowH += objCount * 24f + 10f;
        }

        var viewport = new Rect(x, y, w, h);
        var content  = new Rect(0, 0, w - 20f, Mathf.Max(rowH, h));
        _scrollQuests = GUI.BeginScrollView(viewport, _scrollQuests, content);

        float iy = 0f;
        var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
        var descStyle  = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13 };
        var objStyle   = new GUIStyle(GUI.skin.label) { fontSize = 13 };

        foreach (var state in quests)
        {
            string badge = state.status == QuestStatus.ReadyToTurnIn ? "  ✓ Ready to turn in" : "";
            var titleRect = new Rect(0, iy, content.width, 26f);
            GUI.Label(titleRect, state.data.title + badge, titleStyle);
            if (titleRect.Contains(Event.current.mousePosition)) hover = state.data;  // local space inside scroll
            iy += 26f;

            if (!string.IsNullOrEmpty(state.data.description))
            {
                GUI.Label(new Rect(8f, iy, content.width - 8f, 24f), state.data.description, descStyle);
                iy += 24f;
            }

            if (state.data.objectives != null)
            {
                foreach (var obj in state.data.objectives)
                {
                    int idx   = state.data.objectives.IndexOf(obj);
                    int curr  = QuestSystem.Instance.GetObjectiveCount(state.data, idx);
                    bool done = curr >= obj.requiredCount;

                    string check = done ? "[✓]" : "[ ]";
                    string line  = obj.requiredCount > 1
                        ? $"{check} {obj.description}  ({curr}/{obj.requiredCount})"
                        : $"{check} {obj.description}";

                    GUI.Label(new Rect(16f, iy, content.width - 16f, 22f), line, objStyle);
                    iy += 22f;
                }
            }

            iy += 12f;
            GUI.Box(new Rect(0, iy - 6f, content.width, 1f), "");
        }

        GUI.EndScrollView();

        if (hover != null) QuestTooltip.Draw(hover, screenMouse);
    }

    void DrawCompletedQuests(float x, float y, float w, float h)
    {
        if (QuestSystem.Instance == null) return;
        var quests = QuestSystem.Instance.GetCompletedQuests();

        if (quests.Count == 0)
        {
            GUI.Label(new Rect(x, y + 20f, w, 30f), "No completed quests yet.",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            return;
        }

        var viewport = new Rect(x, y, w, h);
        var content  = new Rect(0, 0, w - 20f, Mathf.Max(quests.Count * 42f, h));
        _scrollQuests = GUI.BeginScrollView(viewport, _scrollQuests, content);

        var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
        var subStyle   = new GUIStyle(GUI.skin.label) { fontSize = 13 };

        for (int i = 0; i < quests.Count; i++)
        {
            float iy    = i * 42f;
            string label = quests[i].status == QuestStatus.Cancelled ? "(Cancelled)" : "Completed";
            GUI.Label(new Rect(0, iy,        content.width, 22f), quests[i].data.title, titleStyle);
            GUI.Label(new Rect(8f, iy + 20f, content.width - 8f, 18f), label, subStyle);
        }

        GUI.EndScrollView();
    }

    // ── Map ───────────────────────────────────────────────────────────────────

    void DrawMap(float x, float y, float w, float h)
    {
        // Blank map placeholder — leave a margin on all sides and draw a dark square.
        const float margin = 12f;
        float mapX = x + margin;
        float mapY = y + margin;
        float mapW = w - margin * 2f;
        float mapH = h - margin * 2f;

        // Dark background
        GUI.color = new Color(0.08f, 0.10f, 0.08f, 1f);
        GUI.DrawTexture(new Rect(mapX, mapY, mapW, mapH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Border
        float bt = 2f;
        GUI.color = new Color(0.4f, 0.42f, 0.38f, 1f);
        GUI.DrawTexture(new Rect(mapX,             mapY,             mapW, bt),   Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(mapX,             mapY + mapH - bt, mapW, bt),   Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(mapX,             mapY,             bt,   mapH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(mapX + mapW - bt, mapY,             bt,   mapH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Placeholder label
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 16,
            fontStyle = FontStyle.Italic,
        };
        style.normal.textColor = new Color(0.35f, 0.38f, 0.33f, 1f);
        GUI.Label(new Rect(mapX, mapY, mapW, mapH), "Map — coming soon", style);

        // Day / hour readout, top-right of the map.
        if (GameClock.Instance != null)
        {
            var clock = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
            };
            clock.normal.textColor = new Color(0.92f, 0.9f, 0.78f, 1f);
            GUI.Label(new Rect(mapX, mapY + 8f, mapW - 12f, 24f),
                      $"Day {GameClock.Instance.Day} – {GameClock.Instance.Hour:00}:00", clock);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void DrawBorder(Rect r, float t)
    {
        GUI.DrawTexture(new Rect(r.x,              r.y,              r.width,  t),       Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,              r.y + r.height - t, r.width, t),     Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,              r.y,              t,        r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + r.width - t, r.y,            t,        r.height), Texture2D.whiteTexture);
    }

    static void ApplyCursor()
    {
        if (IsOpen)
        {
            Time.timeScale      = 0f;
            AudioListener.pause = true;
            Cursor.lockState    = CursorLockMode.None;
            Cursor.visible      = true;
        }
        else
        {
            // Only restore time if PauseMenuController isn't independently paused.
            if (!PauseMenuController.IsPaused)
            {
                Time.timeScale      = 1f;
                AudioListener.pause = false;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    static bool InGameplayScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene != GameManager.MainMenuScene && scene != GameManager.IntroScene && scene != "Persistent";
    }

    static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
