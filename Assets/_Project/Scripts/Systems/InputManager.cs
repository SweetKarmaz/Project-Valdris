using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Central input for the whole game, built on the Input System package.
// Actions are defined in code with keyboard+mouse and gamepad bindings.
// All gameplay scripts read from here — nothing touches UnityEngine.Input.
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private InputAction _move, _look, _jump, _sprint;
    private InputAction _attack, _cast, _detect, _skip, _gameMenu;
    private InputAction _quickSave, _quickLoad;
    private InputAction _openInventory, _openCharacter, _openSkills, _openSpells, _openQuests, _openMap;
    private InputAction _quickUse1, _quickUse2;
    private readonly InputAction[] _spellSlots = new InputAction[4];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateActions();
    }

    private void CreateActions()
    {
        _move = new InputAction("Move", InputActionType.Value);
        _move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        _move.AddBinding("<Gamepad>/leftStick");

        _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        _look.AddBinding("<Gamepad>/rightStick");

        _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        _jump.AddBinding("<Gamepad>/buttonSouth");

        _sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        _sprint.AddBinding("<Gamepad>/leftStickPress");

        _attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
        _attack.AddBinding("<Gamepad>/rightTrigger");

        _cast = new InputAction("CastSpell", InputActionType.Button, "<Mouse>/rightButton");
        _cast.AddBinding("<Gamepad>/leftTrigger");

        _detect = new InputAction("Detect", InputActionType.Button, "<Keyboard>/q");
        _detect.AddBinding("<Gamepad>/buttonNorth");

        _skip = new InputAction("Skip", InputActionType.Button, "<Keyboard>/escape");
        _skip.AddBinding("<Gamepad>/start");

        _gameMenu = new InputAction("GameMenu", InputActionType.Button, "<Keyboard>/tab");
        _gameMenu.AddBinding("<Gamepad>/select");

        _quickSave = new InputAction("QuickSave", InputActionType.Button, "<Keyboard>/f5");
        _quickLoad = new InputAction("QuickLoad", InputActionType.Button, "<Keyboard>/f9");

        _openInventory = new InputAction("OpenInventory", InputActionType.Button, "<Keyboard>/i");
        _openCharacter = new InputAction("OpenCharacter", InputActionType.Button, "<Keyboard>/o");
        _openSkills    = new InputAction("OpenSkills",    InputActionType.Button, "<Keyboard>/k");
        _openSpells    = new InputAction("OpenSpells",    InputActionType.Button, "<Keyboard>/l");
        _openQuests    = new InputAction("OpenQuests",    InputActionType.Button, "<Keyboard>/j");
        _openMap       = new InputAction("OpenMap",       InputActionType.Button, "<Keyboard>/m");

        for (int i = 0; i < 4; i++)
            _spellSlots[i] = new InputAction($"SpellSlot{i + 1}", InputActionType.Button, $"<Keyboard>/{i + 1}");

        _quickUse1 = new InputAction("QuickUse1", InputActionType.Button, "<Keyboard>/9");
        _quickUse2 = new InputAction("QuickUse2", InputActionType.Button, "<Keyboard>/0");

        BuildRebindables();
        LoadOverrides();

        foreach (InputAction action in AllActions()) action.Enable();
    }

    private System.Collections.Generic.IEnumerable<InputAction> AllActions()
    {
        yield return _move; yield return _look; yield return _jump; yield return _sprint;
        yield return _attack; yield return _cast; yield return _detect; yield return _skip;
        yield return _gameMenu; yield return _quickSave; yield return _quickLoad;
        yield return _openInventory; yield return _openCharacter; yield return _openSkills;
        yield return _openSpells; yield return _openQuests; yield return _openMap;
        yield return _quickUse1; yield return _quickUse2;
        foreach (InputAction slot in _spellSlots) yield return slot;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        foreach (InputAction action in AllActions()) action.Disable();
    }

    // ---- Static, null-safe accessors ----

    public static Vector2 Move => Instance != null ? Instance._move.ReadValue<Vector2>() : Vector2.zero;
    public static Vector2 Look => Instance != null ? Instance._look.ReadValue<Vector2>() : Vector2.zero;
    public static bool JumpPressed => Instance != null && Instance._jump.WasPressedThisFrame();
    public static bool SprintHeld => Instance != null && Instance._sprint.IsPressed();
    public static bool AttackPressed => Instance != null && Instance._attack.WasPressedThisFrame();
    public static bool CastPressed => Instance != null && Instance._cast.WasPressedThisFrame();
    public static bool DetectPressed => Instance != null && Instance._detect.WasPressedThisFrame();
    public static bool SkipPressed => Instance != null && Instance._skip.WasPressedThisFrame();
    public static bool GameMenuPressed  => Instance != null && Instance._gameMenu.WasPressedThisFrame();
    public static bool QuickSavePressed   => Instance != null && Instance._quickSave.WasPressedThisFrame();
    public static bool QuickLoadPressed   => Instance != null && Instance._quickLoad.WasPressedThisFrame();
    public static bool OpenInventoryPressed => Instance != null && Instance._openInventory.WasPressedThisFrame();
    public static bool OpenCharacterPressed => Instance != null && Instance._openCharacter.WasPressedThisFrame();
    public static bool OpenSkillsPressed    => Instance != null && Instance._openSkills.WasPressedThisFrame();
    public static bool OpenSpellsPressed    => Instance != null && Instance._openSpells.WasPressedThisFrame();
    public static bool OpenQuestsPressed    => Instance != null && Instance._openQuests.WasPressedThisFrame();
    public static bool OpenMapPressed       => Instance != null && Instance._openMap.WasPressedThisFrame();

    public static bool QuickUse1Pressed => Instance != null && Instance._quickUse1.WasPressedThisFrame();
    public static bool QuickUse2Pressed => Instance != null && Instance._quickUse2.WasPressedThisFrame();

    // Returns the spell slot index pressed this frame, or -1.
    public static int SpellSlotPressed
    {
        get
        {
            if (Instance == null) return -1;
            for (int i = 0; i < 4; i++)
                if (Instance._spellSlots[i].WasPressedThisFrame()) return i;
            return -1;
        }
    }

    // ── Rebinding ───────────────────────────────────────────────────────────────
    //
    // Only the player-facing gameplay keys are rebindable. The menu/system keys
    // (Esc, Tab, the tab-shortcut letters, quick save/load) are fixed AND blocked
    // as rebind targets so the player can't shadow them.

    public class Rebindable
    {
        public string Label;
        public InputAction Action;
        public int BindingIndex;   // which binding on the action is the keyboard one
    }

    List<Rebindable> _rebindables;
    public IReadOnlyList<Rebindable> Rebindables => _rebindables;

    public bool IsRebinding { get; private set; }
    InputActionRebindingExtensions.RebindingOperation _rebindOp;

    // Keys that may never be assigned to a gameplay action.
    static readonly string[] BlockedControls =
    {
        "<Keyboard>/escape", "<Keyboard>/tab",
        "<Keyboard>/i", "<Keyboard>/o", "<Keyboard>/k", "<Keyboard>/l", "<Keyboard>/j", "<Keyboard>/m",
        "<Keyboard>/f5", "<Keyboard>/f9",
        "<Mouse>/delta", "<Mouse>/position", "<Mouse>/scroll",
    };

    void BuildRebindables()
    {
        _rebindables = new List<Rebindable>
        {
            new() { Label = "Move Forward", Action = _move, BindingIndex = 1 }, // composite parts 1..4
            new() { Label = "Move Back",    Action = _move, BindingIndex = 2 },
            new() { Label = "Move Left",    Action = _move, BindingIndex = 3 },
            new() { Label = "Move Right",   Action = _move, BindingIndex = 4 },
            new() { Label = "Jump",            Action = _jump,   BindingIndex = 0 },
            new() { Label = "Sprint",          Action = _sprint, BindingIndex = 0 },
            new() { Label = "Attack",          Action = _attack, BindingIndex = 0 },
            new() { Label = "Cast Spell",      Action = _cast,   BindingIndex = 0 },
            new() { Label = "Detect Corruption", Action = _detect, BindingIndex = 0 },
            new() { Label = "Spell Slot 1", Action = _spellSlots[0], BindingIndex = 0 },
            new() { Label = "Spell Slot 2", Action = _spellSlots[1], BindingIndex = 0 },
            new() { Label = "Spell Slot 3", Action = _spellSlots[2], BindingIndex = 0 },
            new() { Label = "Spell Slot 4", Action = _spellSlots[3], BindingIndex = 0 },
            new() { Label = "Quick Use 1", Action = _quickUse1, BindingIndex = 0 },
            new() { Label = "Quick Use 2", Action = _quickUse2, BindingIndex = 0 },
        };
    }

    public static string DisplayFor(Rebindable r) =>
        r?.Action != null
            ? r.Action.GetBindingDisplayString(r.BindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames)
            : "";

    // Begin an interactive rebind. onDone(true) on success, (false) if cancelled.
    public void StartRebind(Rebindable r, Action<bool> onDone)
    {
        if (IsRebinding || r?.Action == null) return;
        IsRebinding = true;

        var action = r.Action;
        action.Disable();   // required before an interactive rebind

        var op = action.PerformInteractiveRebinding(r.BindingIndex)
            .WithCancelingThrough("<Keyboard>/escape");
        foreach (string blocked in BlockedControls) op = op.WithControlsExcluding(blocked);

        _rebindOp = op
            .OnComplete(_ => FinishRebind(r, onDone, true))
            .OnCancel(_   => FinishRebind(r, onDone, false))
            .Start();
    }

    void FinishRebind(Rebindable r, Action<bool> onDone, bool success)
    {
        _rebindOp?.Dispose();
        _rebindOp = null;
        r.Action.Enable();
        IsRebinding = false;
        if (success) SaveOverride(r.Action);
        onDone?.Invoke(success);
    }

    public void CancelRebind()
    {
        if (!IsRebinding) return;
        _rebindOp?.Cancel();
    }

    public void ResetBindingsToDefault()
    {
        foreach (var r in _rebindables)
        {
            r.Action.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefKey(r.Action));
        }
        PlayerPrefs.Save();
    }

    // ── Override persistence (PlayerPrefs, one entry per action) ────────────────
    static string PrefKey(InputAction a) => $"keybind_{a.name}";

    void SaveOverride(InputAction action)
    {
        PlayerPrefs.SetString(PrefKey(action), action.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    void LoadOverrides()
    {
        var seen = new HashSet<InputAction>();
        foreach (var r in _rebindables)
        {
            if (!seen.Add(r.Action)) continue;   // _move appears 4× — load once
            string json = PlayerPrefs.GetString(PrefKey(r.Action), "");
            if (!string.IsNullOrEmpty(json)) r.Action.LoadBindingOverridesFromJson(json);
        }
    }
}
