using System.Collections.Generic;
using UnityEngine;

public enum PropInteractionType
{
    QuestStarter,    // Offers a quest with optional dialogue text
    LootContainer,   // Chest, barrel, sack — contains items and gold
    Readable,        // Book, note, sign — shows scrollable text
    Usable,          // Lever, switch, button — fires world flags
    Door,            // Opens / closes, may require a key or world flag
    RestPoint,       // Campfire, bed — restores health/mana
    CraftingStation, // Anvil, alchemy table — opens crafting UI (placeholder)
    Vendor,          // Market stall, trader — opens shop UI (placeholder)
}

public enum PropHighlightState { None, Discovered, Active }

// Single interactable component for scene props. Pick an interaction type
// and fill in the relevant header section. The prop registers in the static
// All list so InteractionHUD can scan it without FindObjectsByType.
public class InteractableProp : MonoBehaviour
{
    public static readonly List<InteractableProp> All = new();

    [Header("Interaction")]
    public PropInteractionType interactionType;
    [Tooltip("Label shown in the reticle tooltip. Leave blank for the type default.")]
    public string interactionLabel;
    [Tooltip("How close the player must be for the reticle to activate.")]
    public float interactionRange = 2.5f;
    [Tooltip("Distance at which a silver discovery highlight appears.")]
    public float discoveryRange = 10f;

    [Header("Save")]
    [Tooltip("Unique ID for this prop within the scene. Required for LootContainers to persist loot state.")]
    public string propId;

    // ── Quest Starter ─────────────────────────────────────────────────────────

    [Header("Quest Starter")]
    [Tooltip("Drag a QuestData asset here to link it to this prop.")]
    public QuestData questData;
    [TextArea(3, 8)]
    [Tooltip("Story / flavour text shown before the Accept prompt. " +
             "Falls back to questData.description if blank.")]
    public string questDialogueText;
    [TextArea(1, 3)]
    [Tooltip("Short offer line shown just above the Accept / Decline buttons.")]
    public string questOfferLine;
    [Tooltip("Destroy this GameObject when the quest is accepted.")]
    public bool destroyOnAccept;
    [Tooltip("Add an item to the player's inventory when the quest is accepted " +
             "(e.g. the letter or sword that started the quest).")]
    public bool addToInventoryOnAccept;
    [Tooltip("The LootItem prefab added to inventory on accept. Only used when addToInventoryOnAccept is true.")]
    public LootItem questInventoryItem;

    // ── Loot Container ────────────────────────────────────────────────────────

    [Header("Loot Container — Fixed")]
    [Tooltip("Items always present in this container (no drop-chance roll).")]
    public List<NpcItem> fixedLoot = new();

    [Header("Loot Container — Random")]
    [Tooltip("Items rolled against their dropChance when the container is first opened. " +
             "Loot is generated once and never re-rolled.")]
    public List<NpcItem> randomLoot = new();
    public int goldMin;
    public int goldMax;
    [Tooltip("Hides the GameObject once all loot has been taken.")]
    public bool hideWhenEmptied;

    // ── Readable ──────────────────────────────────────────────────────────────

    [Header("Readable")]
    public string readableTitle;
    [TextArea(6, 20)]
    public string readableText;

    // ── Usable / Switch ───────────────────────────────────────────────────────

    [Header("Usable")]
    public string[] setsWorldFlagsOnUse;
    [Tooltip("If true, the prop can only be activated once.")]
    public bool usableOneShot = true;
    [Tooltip("Interaction label shown after the prop has been used.")]
    public string usedLabel = "Used";

    // ── Door ──────────────────────────────────────────────────────────────────

    [Header("Door")]
    [Tooltip("Item asset name that must be in the player's inventory to open. Leave blank if unlocked.")]
    public string requiredKeyItemName;
    [Tooltip("World flags that must all be true to open. Leave empty if unlocked.")]
    public string[] doorRequiredWorldFlags;
    public string lockedMessage = "It's locked.";
    [Tooltip("Shown once when the door is unlocked.")]
    public string unlockedMessage = "Unlocked.";
    [Tooltip("Optional bar/prop shown while locked; hidden once the door is unlocked.")]
    public GameObject lockBlocker;
    [Tooltip("Optional Gateway (on this object) that only fires once the door is unlocked and opened.")]
    public Gateway linkedGateway;

    bool _doorUnlocked;
    DoorController _doorCache;
    DoorController Door => _doorCache != null ? _doorCache : (_doorCache = GetComponentInChildren<DoorController>());
    Gateway LinkedGateway => linkedGateway != null ? linkedGateway : GetComponent<Gateway>();

    // ── Rest Point ────────────────────────────────────────────────────────────

    [Header("Rest Point")]
    [Range(0f, 1f)] public float healthRestorePercent = 1f;
    [Range(0f, 1f)] public float manaRestorePercent   = 1f;
    public bool savesGameOnRest = true;

    // ── Runtime state ─────────────────────────────────────────────────────────

    bool _questAccepted;
    bool _questOffering;

    bool _lootGenerated;
    bool _looted;
    bool _lootOpen;
    readonly List<LootEntry> _availableLoot = new();
    int _availableGold;
    Vector2 _lootScroll;

    bool _readOpen;
    Vector2 _readScroll;

    bool _used;
    bool _restConfirming;

    // ── Highlight ─────────────────────────────────────────────────────────────

    Renderer[]          _renderers;
    MaterialPropertyBlock _mpb;
    PropHighlightState  _highlightState = PropHighlightState.None;

    static readonly Color ColorDiscovered = new Color(0.7f, 0.75f, 0.95f) * 0.9f;  // silver-blue
    static readonly Color ColorActive     = new Color(1f,   0.82f, 0.1f)  * 2.5f;  // warm gold (HDR)

    // ── Label / interactability ───────────────────────────────────────────────

    public string Label
    {
        get
        {
            if (!string.IsNullOrEmpty(interactionLabel)) return interactionLabel;
            return interactionType switch
            {
                PropInteractionType.QuestStarter    => "Examine",
                PropInteractionType.LootContainer   => _looted ? "Empty" : "Search",
                PropInteractionType.Readable        => "Read",
                PropInteractionType.Usable          => (usableOneShot && _used) ? usedLabel : "Use",
                PropInteractionType.Door            => DoorLocked ? "Unlock" : (Door != null && Door.IsOpen ? "Close" : "Open"),
                PropInteractionType.RestPoint       => "Rest",
                PropInteractionType.CraftingStation => "Craft",
                PropInteractionType.Vendor          => "Shop",
                _                                   => "Interact",
            };
        }
    }

    public bool IsInteractable =>
        interactionType switch
        {
            PropInteractionType.QuestStarter    => !_questAccepted && questData != null
                                                   && (QuestSystem.Instance?.CanOffer(questData) ?? false),
            PropInteractionType.LootContainer   => !_looted,
            PropInteractionType.Readable        => true,
            PropInteractionType.Usable          => !(usableOneShot && _used),
            PropInteractionType.Door            => true,
            PropInteractionType.RestPoint       => true,
            PropInteractionType.CraftingStation => true,
            PropInteractionType.Vendor          => true,
            _                                   => true,
        };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        All.Add(this);
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb       = new MaterialPropertyBlock();
    }

    // The point used for proximity targeting/highlighting — the centre of the
    // prop's rendered bounds rather than its pivot. Matters for off-pivot props
    // like a door hinged on one edge: this keeps the clickable zone on the door
    // itself instead of bunched around the hinge.
    public Vector3 InteractionPoint
    {
        get
        {
            if (_renderers == null || _renderers.Length == 0) return transform.position;
            Bounds b = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++) b.Encapsulate(_renderers[i].bounds);
            return b.center;
        }
    }

    void Start()
    {
        if (interactionType == PropInteractionType.LootContainer && !string.IsNullOrEmpty(propId))
            SceneStateManager.Instance?.RegisterLootContainer(this);
        if (interactionType == PropInteractionType.Door && !string.IsNullOrEmpty(propId))
            SceneStateManager.Instance?.RegisterDoor(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (interactionType == PropInteractionType.LootContainer && !string.IsNullOrEmpty(propId))
            SceneStateManager.Instance?.UnregisterLootContainer(this);
        if (interactionType == PropInteractionType.Door && !string.IsNullOrEmpty(propId))
            SceneStateManager.Instance?.UnregisterDoor(this);
    }

    // ── Highlight (called by InteractionHUD each frame) ───────────────────────

    public void SetHighlight(PropHighlightState state)
    {
        if (_highlightState == state) return;
        _highlightState = state;

        _mpb.Clear();
        if (state != PropHighlightState.None)
        {
            Color c = state == PropHighlightState.Active ? ColorActive : ColorDiscovered;
            _mpb.SetColor("_EmissiveColor", c);
        }
        foreach (var r in _renderers)
            if (r != null) r.SetPropertyBlock(_mpb);
    }

    // ── Called by InteractionHUD on left-click ────────────────────────────────

    public void Interact()
    {
        if (!string.IsNullOrEmpty(propId))
            QuestSystem.Instance?.ReportPropInteracted(propId);   // progress InteractWithProp objectives

        switch (interactionType)
        {
            case PropInteractionType.QuestStarter:    HandleQuestStarter();    break;
            case PropInteractionType.LootContainer:   HandleLootContainer();   break;
            case PropInteractionType.Readable:        HandleReadable();        break;
            case PropInteractionType.Usable:          HandleUsable();          break;
            case PropInteractionType.Door:            HandleDoor();            break;
            case PropInteractionType.RestPoint:       HandleRestPoint();       break;
            case PropInteractionType.CraftingStation: HandleCraftingStation(); break;
            case PropInteractionType.Vendor:          HandleVendor();          break;
        }
    }

    // ── Quest Starter ─────────────────────────────────────────────────────────

    void HandleQuestStarter()
    {
        if (_questAccepted || questData == null) return;
        if (QuestSystem.Instance == null || !QuestSystem.Instance.CanOffer(questData)) return;
        _questOffering = true;
    }

    void AcceptQuest()
    {
        QuestSystem.Instance?.AcceptQuest(questData);
        _questAccepted = true;
        _questOffering = false;

        if (addToInventoryOnAccept && questInventoryItem != null)
            InventorySystem.Instance?.AddLootItem(questInventoryItem, 1);

        if (destroyOnAccept)
            Destroy(gameObject);
    }

    // ── Loot Container ────────────────────────────────────────────────────────

    void HandleLootContainer()
    {
        if (_looted) return;
        if (!_lootGenerated) GenerateLoot();
        _lootOpen = true;
    }

    void GenerateLoot()
    {
        _lootGenerated = true;
        _availableLoot.Clear();

        // Fixed loot — always included.
        foreach (var item in fixedLoot)
            if (!string.IsNullOrEmpty(item.ItemName))
                AddOrStack(item.ItemName, item.quantity);

        // Random loot — rolled once.
        foreach (var item in randomLoot)
            if (!string.IsNullOrEmpty(item.ItemName) && Random.value <= item.dropChance)
                AddOrStack(item.ItemName, item.quantity);

        _availableGold = goldMax > goldMin ? Random.Range(goldMin, goldMax + 1) : goldMin;

        // Save immediately so the rolled state persists even if the player never opens it.
        if (!string.IsNullOrEmpty(propId))
            SceneStateManager.Instance?.SaveState();
    }

    void AddOrStack(string itemName, int quantity)
    {
        var existing = _availableLoot.Find(e => e.itemName == itemName);
        if (existing != null) existing.quantity += quantity;
        else _availableLoot.Add(new LootEntry { itemName = itemName, quantity = quantity });
    }

    void TakeAll()
    {
        // Take as much as fits; anything that doesn't fit stays in the container.
        for (int i = _availableLoot.Count - 1; i >= 0; i--)
        {
            var entry = _availableLoot[i];
            int taken = TryGiveItem(entry.itemName, entry.quantity);
            entry.quantity -= taken;
            if (entry.quantity <= 0) _availableLoot.RemoveAt(i);
        }

        if (_availableGold > 0)
        {
            InventorySystem.Instance?.AddGold(_availableGold);
            _availableGold = 0;
        }

        FinishTakeOrNotify();
    }

    void TakeItem(LootEntry entry)
    {
        int taken = TryGiveItem(entry.itemName, entry.quantity);
        entry.quantity -= taken;
        if (entry.quantity <= 0) _availableLoot.Remove(entry);

        FinishTakeOrNotify();
    }

    // Marks the container emptied (and hides it) when nothing remains. A real
    // overflow surfaces the "Inventory is full" toast automatically via
    // InventorySystem.OnInventoryFull → ScreenNotifier, so we don't notify here.
    void FinishTakeOrNotify()
    {
        if (_availableLoot.Count == 0 && _availableGold == 0)
        {
            _looted   = true;
            _lootOpen = false;
            if (hideWhenEmptied) gameObject.SetActive(false);
        }

        // Persist the container's remaining contents immediately.
        if (!string.IsNullOrEmpty(propId))
            SceneStateManager.Instance?.SaveState();
    }

    // Tries to move `quantity` of an item into the player inventory.
    // Returns how many were actually taken (the rest didn't fit and should stay
    // in the container).
    static int TryGiveItem(string itemName, int quantity)
    {
        var db = SaveSystem.Instance?.database;
        var item = db != null ? db.FindLootItem(itemName) : null;
        if (item == null) { Debug.LogWarning($"[InteractableProp] LootItem '{itemName}' not found in LootRegistry."); return 0; }
        if (InventorySystem.Instance == null) return 0;

        int leftover = InventorySystem.Instance.AddLootItem(item, quantity);
        return quantity - leftover;
    }

    // ── Loot save / restore ───────────────────────────────────────────────────

    public SavedLootState CaptureLootState()
    {
        var state = new SavedLootState
        {
            propId        = propId,
            lootGenerated = _lootGenerated,
            remainingGold = _availableGold,
        };
        foreach (var entry in _availableLoot)
        {
            state.itemNames.Add(entry.itemName);
            state.itemQuantities.Add(entry.quantity);
        }
        return state;
    }

    public void RestoreLootState(SavedLootState state)
    {
        _lootGenerated = state.lootGenerated;
        _availableGold = state.remainingGold;
        _availableLoot.Clear();

        for (int i = 0; i < state.itemNames.Count && i < state.itemQuantities.Count; i++)
            _availableLoot.Add(new LootEntry
            {
                itemName = state.itemNames[i],
                quantity = state.itemQuantities[i],
            });

        _looted = _lootGenerated && _availableLoot.Count == 0 && _availableGold == 0;
    }

    // ── Readable ──────────────────────────────────────────────────────────────

    void HandleReadable() => _readOpen = !_readOpen;

    // ── Usable ────────────────────────────────────────────────────────────────

    void HandleUsable()
    {
        if (usableOneShot && _used) return;
        _used = true;
        if (setsWorldFlagsOnUse != null)
            foreach (string flag in setsWorldFlagsOnUse)
                WorldStateSystem.Instance?.SetFlag(flag, true);
    }

    // ── Door ──────────────────────────────────────────────────────────────────

    // A key door stays "locked" until unlocked; a world-flag door is locked until
    // its flags are all set.
    bool DoorLocked
    {
        get
        {
            if (_doorUnlocked) return false;
            if (doorRequiredWorldFlags != null)
                foreach (string flag in doorRequiredWorldFlags)
                    if (WorldStateSystem.Instance == null || !WorldStateSystem.Instance.GetFlag(flag)) return true;
            return !string.IsNullOrEmpty(requiredKeyItemName);
        }
    }

    void HandleDoor()
    {
        bool hasLock = !string.IsNullOrEmpty(requiredKeyItemName)
                    || (doorRequiredWorldFlags != null && doorRequiredWorldFlags.Length > 0);

        if (!_doorUnlocked && hasLock)
        {
            // World-flag gate (needs the flag set — can't be opened with a key).
            if (doorRequiredWorldFlags != null)
                foreach (string flag in doorRequiredWorldFlags)
                    if (WorldStateSystem.Instance == null || !WorldStateSystem.Instance.GetFlag(flag))
                    { ScreenNotifier.Show(lockedMessage); return; }

            // Key gate — checked against the Keyring; keys are never consumed.
            if (!string.IsNullOrEmpty(requiredKeyItemName) &&
                (Keyring.Instance == null || !Keyring.Instance.Has(requiredKeyItemName)))
            { ScreenNotifier.Show(lockedMessage); return; }

            Unlock();
        }

        // Swing if there's a DoorController; otherwise fall back to hiding it.
        if (Door != null) Door.Toggle();
        else gameObject.SetActive(false);

        // Gateway-door: only transition once it's unlocked and open.
        if (LinkedGateway != null && (Door == null || Door.IsOpen)) LinkedGateway.Activate();

        if (!string.IsNullOrEmpty(propId)) SceneStateManager.Instance?.SaveState();  // persist open/unlocked
    }

    void Unlock()
    {
        _doorUnlocked = true;
        if (lockBlocker != null) lockBlocker.SetActive(false);   // remove the bar
        if (!string.IsNullOrEmpty(unlockedMessage)) ScreenNotifier.Show(unlockedMessage);
    }

    // ── Door save / restore ───────────────────────────────────────────────────

    public SavedDoorState CaptureDoorState() => new SavedDoorState
    {
        propId   = propId,
        unlocked = _doorUnlocked,
        open     = Door != null && Door.IsOpen,
    };

    public void RestoreDoorState(SavedDoorState s)
    {
        _doorUnlocked = s.unlocked;
        if (s.unlocked && lockBlocker != null) lockBlocker.SetActive(false);   // bar stays gone
        if (Door != null) Door.SetOpenInstant(s.open);
        else if (s.open) gameObject.SetActive(false);
    }

    // ── Rest Point ────────────────────────────────────────────────────────────

    void HandleRestPoint() => _restConfirming = !_restConfirming;

    void DoRest()
    {
        _restConfirming = false;
        Debug.Log($"[RestPoint] Player rested at {gameObject.name}.");
        if (savesGameOnRest) SaveSystem.Instance?.Save();
    }

    // ── Crafting / Vendor (placeholders) ─────────────────────────────────────

    void HandleCraftingStation() => Debug.Log("[InteractableProp] Crafting station — UI not yet implemented.");
    void HandleVendor()          => Debug.Log("[InteractableProp] Vendor — shop UI not yet implemented.");

    // ── IMGUI ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (GameUI.IsOpen || PauseMenuController.IsPaused) return;
        if (_questOffering)  DrawQuestOffer();
        if (_lootOpen)       DrawLootWindow();
        if (_readOpen)       DrawReadableWindow();
        if (_restConfirming) DrawRestConfirm();
    }

    // ── Quest offer ───────────────────────────────────────────────────────────

    void DrawQuestOffer()
    {
        if (questData == null) { _questOffering = false; return; }

        string body  = !string.IsNullOrEmpty(questDialogueText) ? questDialogueText : questData.description;
        float  bodyH = string.IsNullOrEmpty(body) ? 0f : Mathf.Clamp(body.Length * 0.45f, 60f, 200f);
        float  offH  = string.IsNullOrEmpty(questOfferLine) ? 0f : 30f;
        const float PW = 500f;
        float  PH    = 60f + bodyH + offH + 52f;
        float  px    = (Screen.width  - PW) / 2f;
        float  py    = (Screen.height - PH) / 2f;

        GUI.Box(new Rect(px, py, PW, PH), "");
        float cur = py + 12f;

        GUI.Label(new Rect(px + 10f, cur, PW - 20f, 28f),
            questData.title, Centered(18, FontStyle.Bold));
        cur += 34f;

        if (!string.IsNullOrEmpty(body))
        {
            GUI.Label(new Rect(px + 16f, cur, PW - 32f, bodyH), body, BodyStyle());
            cur += bodyH + 6f;
        }

        if (!string.IsNullOrEmpty(questOfferLine))
        {
            GUI.Label(new Rect(px + 16f, cur, PW - 32f, offH),
                questOfferLine, ItalicCentered(14));
            cur += offH + 4f;
        }

        GUI.skin.button.fontSize = 15;
        const float btnW = 130f, btnH = 38f;
        float btnY = cur + 4f;
        float btnX = px + (PW - (btnW * 2f + 16f)) / 2f;

        if (GUI.Button(new Rect(btnX,             btnY, btnW, btnH), "Accept"))  AcceptQuest();
        if (GUI.Button(new Rect(btnX + btnW + 16f, btnY, btnW, btnH), "Decline")) _questOffering = false;
    }

    // ── Loot window ───────────────────────────────────────────────────────────

    void DrawLootWindow()
    {
        const float PW = 420f, PH = 360f;
        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;

        GUI.Box(new Rect(px, py, PW, PH), "");
        GUI.Label(new Rect(px + 10f, py + 8f, PW - 50f, 28f),
            string.IsNullOrEmpty(interactionLabel) ? "Container" : interactionLabel,
            Centered(17, FontStyle.Bold));
        if (GUI.Button(new Rect(px + PW - 36f, py + 6f, 28f, 26f), "X"))
        { _lootOpen = false; return; }

        float listH  = PH - 100f;
        float rowH   = 34f;
        float totalH = Mathf.Max((_availableLoot.Count + (_availableGold > 0 ? 1 : 0)) * rowH, listH);

        var viewport = new Rect(px + 8f, py + 42f, PW - 16f, listH);
        var content  = new Rect(0, 0, PW - 32f, totalH);
        _lootScroll  = GUI.BeginScrollView(viewport, _lootScroll, content);

        float iy    = 0f;
        LootEntry toTake = null;

        foreach (var entry in _availableLoot)
        {
            GUI.Box(new Rect(0, iy, content.width, rowH - 2f), "");
            GUI.Label(new Rect(8f, iy + 6f, content.width - 80f, 22f),
                $"{entry.itemName}  ×{entry.quantity}");
            if (GUI.Button(new Rect(content.width - 72f, iy + 4f, 66f, 24f), "Take"))
                toTake = entry;
            iy += rowH;
        }

        if (_availableGold > 0)
        {
            GUI.Box(new Rect(0, iy, content.width, rowH - 2f), "");
            GUI.Label(new Rect(8f, iy + 6f, content.width - 80f, 22f), $"Gold  ×{_availableGold}");
            iy += rowH;
        }

        if (_availableLoot.Count == 0 && _availableGold == 0)
            GUI.Label(new Rect(0, iy + 10f, content.width, 24f), "Empty.",
                Centered(14, FontStyle.Normal));

        GUI.EndScrollView();

        if (toTake != null) TakeItem(toTake);

        float btnY = py + PH - 48f;
        GUI.skin.button.fontSize = 14;
        if (GUI.Button(new Rect(px + 12f,         btnY, 130f, 36f), "Take All")) TakeAll();
        if (GUI.Button(new Rect(px + PW - 142f,   btnY, 130f, 36f), "Close"))    _lootOpen = false;
    }

    // ── Readable window ───────────────────────────────────────────────────────

    void DrawReadableWindow()
    {
        const float PW = 520f, PH = 420f;
        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;

        GUI.Box(new Rect(px, py, PW, PH), "");
        if (!string.IsNullOrEmpty(readableTitle))
            GUI.Label(new Rect(px + 10f, py + 8f, PW - 50f, 28f),
                readableTitle, Centered(17, FontStyle.Bold));
        if (GUI.Button(new Rect(px + PW - 36f, py + 6f, 28f, 26f), "X"))
        { _readOpen = false; return; }

        var textStyle = new GUIStyle(GUI.skin.label)
            { wordWrap = true, fontSize = 14, alignment = TextAnchor.UpperLeft };
        float textH = textStyle.CalcHeight(new GUIContent(readableText), PW - 48f);

        var viewport = new Rect(px + 12f, py + 44f, PW - 24f, PH - 56f);
        var content  = new Rect(0, 0, PW - 44f, Mathf.Max(textH, PH - 56f));
        _readScroll  = GUI.BeginScrollView(viewport, _readScroll, content);
        GUI.Label(new Rect(0, 0, content.width, content.height), readableText, textStyle);
        GUI.EndScrollView();
    }

    // ── Rest confirm ──────────────────────────────────────────────────────────

    void DrawRestConfirm()
    {
        const float PW = 340f, PH = 130f;
        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;

        GUI.Box(new Rect(px, py, PW, PH), "");
        GUI.Label(new Rect(px + 10f, py + 12f, PW - 20f, 30f),
            "Rest here?", Centered(18, FontStyle.Bold));

        GUI.skin.button.fontSize = 15;
        float btnY = py + PH - 50f;
        float btnX = px + (PW - 264f) / 2f;
        if (GUI.Button(new Rect(btnX,        btnY, 120f, 38f), "Rest"))   DoRest();
        if (GUI.Button(new Rect(btnX + 144f, btnY, 120f, 38f), "Cancel")) _restConfirming = false;
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    static GUIStyle Centered(int size, FontStyle style) => new(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontSize = size, fontStyle = style };
    static GUIStyle BodyStyle() => new(GUI.skin.label)
        { wordWrap = true, fontSize = 14, alignment = TextAnchor.UpperLeft };
    static GUIStyle ItalicCentered(int size) => new(GUI.skin.label)
        { wordWrap = true, fontStyle = FontStyle.Italic, fontSize = size, alignment = TextAnchor.MiddleCenter };

    // ── Inner type ────────────────────────────────────────────────────────────

    class LootEntry
    {
        public string itemName;
        public int    quantity;
    }
}
