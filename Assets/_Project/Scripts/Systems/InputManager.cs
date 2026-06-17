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

        foreach (InputAction action in AllActions()) action.Enable();
    }

    private System.Collections.Generic.IEnumerable<InputAction> AllActions()
    {
        yield return _move; yield return _look; yield return _jump; yield return _sprint;
        yield return _attack; yield return _cast; yield return _detect; yield return _skip;
        yield return _gameMenu; yield return _quickSave; yield return _quickLoad;
        yield return _openInventory; yield return _openCharacter; yield return _openSkills;
        yield return _openSpells; yield return _openQuests; yield return _openMap;
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
}
