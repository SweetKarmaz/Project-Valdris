using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Per-scene singleton that owns the two-tier scene state system:
//
//   DefaultState  (ScriptableObject asset, read-only)
//     ↓ used only on the very first visit in a playthrough
//   ActiveState   (JSON file in PersistentDataPath/Scenes/<scene>_scene.json)
//     ↑ written on every explicit save and auto-saved when the scene unloads
//
// NPCs are placed directly in the editor and self-register via NpcController.Start().
// Registration automatically applies any previously saved state for that NPC.
// Removing a character via story beats: call UnregisterNPC() before destroying
// the GameObject — on the next save they won't be in the JSON.
public class SceneStateManager : MonoBehaviour
{
    public static SceneStateManager Instance { get; private set; }

    [Tooltip("Pristine default state asset — created by " +
             "Tools > Valdris > Create Default Scene State. Never assigned at runtime.")]
    public SceneDefaultState defaultState;

    // ── Runtime ───────────────────────────────────────────────────────────────

    SceneStateSave               _active;
    readonly List<NpcController>  _trackedNPCs  = new();
    readonly List<SaveableProp>   _trackedProps = new();
    readonly List<InteractableProp> _trackedLoot = new();
    readonly List<LootContainer>  _trackedContainers = new();

    // ── Paths ─────────────────────────────────────────────────────────────────

    // The LIVE session world. SceneStateManager reads/writes here continuously as
    // the player explores. Save snapshots this folder; Load replaces it with a
    // save's snapshot; New Game wipes it. Exposed for SaveSystem.
    static string ScenesSaveDir =>
        Path.Combine(Application.persistentDataPath, "Session", "Scenes");

    public static string SessionScenesDir => ScenesSaveDir;

    string ActiveSavePath =>
        Path.Combine(ScenesSaveDir, gameObject.scene.name + "_scene.json");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadActiveState();
    }

    void Start()
    {
        // Re-instantiate runtime dropped bags that still held items when last saved.
        RestoreDroppedContainers();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        // Auto-save on in-game scene transitions so NPC positions carry over.
        // Skip when loading a save (IsRestoringFromSave) — the loaded JSON is
        // already correct. Skip when going to the main menu or quitting
        // (SuppressSceneAutoSave) — Unity destroys objects in an undefined
        // order during unload, so a partial snapshot would corrupt the JSON.
        if (!SaveSystem.IsRestoringFromSave
            && !SaveSystem.SuppressSceneAutoSave
            && (_trackedNPCs.Count > 0 || _trackedLoot.Count > 0 || _trackedContainers.Count > 0))
        {
            SaveState();
        }
    }

    // ── State access ──────────────────────────────────────────────────────────

    public bool HasSavedState =>
        _active != null && !_active.firstVisit && _active.npcs != null && _active.npcs.Count > 0;

    // ── NPC registration ──────────────────────────────────────────────────────

    // Called by NpcController.Start(). Adds the NPC to the tracked list and
    // immediately applies its saved state if one exists for this saveId.
    public void RegisterNPC(NpcController npc)
    {
        if (npc == null || _trackedNPCs.Contains(npc)) return;
        _trackedNPCs.Add(npc);

        if (_active == null || _active.firstVisit || string.IsNullOrEmpty(npc.saveId)) return;
        var saved = _active.npcs?.Find(s => s.id == npc.saveId);
        if (saved != null) npc.RestoreFromState(saved);
    }

    // Call before destroying or disabling an NPC due to a story beat.
    // After this the NPC will not appear in the next save and won't be
    // restored on the next visit (effectively removed from the scene).
    public void UnregisterNPC(NpcController npc) => _trackedNPCs.Remove(npc);

    // ── Prop registration ─────────────────────────────────────────────────────

    public void RegisterProp(SaveableProp prop)
    {
        if (prop != null && !_trackedProps.Contains(prop))
            _trackedProps.Add(prop);
    }

    public void UnregisterProp(SaveableProp prop) => _trackedProps.Remove(prop);

    // ── Loot container registration ───────────────────────────────────────────

    // Called by InteractableProp.Start() for LootContainer props that have a propId.
    // Immediately applies any previously saved loot state.
    public void RegisterLootContainer(InteractableProp prop)
    {
        if (prop == null || _trackedLoot.Contains(prop)) return;
        _trackedLoot.Add(prop);

        if (_active == null || _active.firstVisit || string.IsNullOrEmpty(prop.propId)) return;
        var saved = _active.lootContainers?.Find(s => s.propId == prop.propId);
        if (saved != null) prop.RestoreLootState(saved);
    }

    public void UnregisterLootContainer(InteractableProp prop) => _trackedLoot.Remove(prop);

    // ── Door registration ──────────────────────────────────────────────────────

    readonly List<InteractableProp> _trackedDoors = new();

    public void RegisterDoor(InteractableProp prop)
    {
        if (prop == null || _trackedDoors.Contains(prop)) return;
        _trackedDoors.Add(prop);

        if (_active == null || _active.firstVisit || string.IsNullOrEmpty(prop.propId)) return;
        var saved = _active.doors?.Find(s => s.propId == prop.propId);
        if (saved != null) prop.RestoreDoorState(saved);
    }

    public void UnregisterDoor(InteractableProp prop) => _trackedDoors.Remove(prop);

    // ── LootContainer (new Inventory-based system) ─────────────────────────────

    // Called by a placed LootContainer.Start() (and by SpawnDropped / dropped
    // re-spawn). For placed containers it also applies any saved state.
    public void RegisterContainer(LootContainer container)
    {
        if (container == null || _trackedContainers.Contains(container)) return;
        _trackedContainers.Add(container);

        if (container.isDropped) return;   // dropped bags are restored at spawn time
        if (_active == null || _active.firstVisit || string.IsNullOrEmpty(container.containerId)) return;

        var saved = _active.containers?.Find(s => s.containerId == container.containerId);
        if (saved != null)
            container.RestoreState(saved, SaveSystem.Instance?.database?.lootRegistry);
    }

    public void UnregisterContainer(LootContainer container) => _trackedContainers.Remove(container);

    void RestoreDroppedContainers()
    {
        if (_active?.containers == null || _active.firstVisit) return;
        var prefab = SaveSystem.Instance?.database?.droppedBagPrefab;
        if (prefab == null) return;

        foreach (var s in _active.containers)
        {
            if (!s.isDropped) continue;
            var bag = Instantiate(prefab, s.position, Quaternion.Euler(0f, s.yRotation, 0f));
            bag.isDropped     = true;
            bag.emptyBehavior = LootContainer.EmptyBehavior.Destroy;
            bag.containerId   = s.containerId;
            bag.RestoreState(s, SaveSystem.Instance?.database?.lootRegistry);
            RegisterContainer(bag);
        }
    }

    // ── Restoring props (editor-placed) ───────────────────────────────────────

    public void RestoreProps()
    {
        if (_active?.props == null) return;

        foreach (var prop in _trackedProps)
        {
            if (prop == null) continue;
            var saved = _active.props.Find(p => p.id == prop.PropId);
            if (saved != null) prop.ApplyState(saved);
        }
    }

    // ── Saving ────────────────────────────────────────────────────────────────

    public void SaveState()
    {
        if (_active == null) _active = new SceneStateSave();
        _active.firstVisit = false;

        _active.npcs.Clear();
        foreach (var npc in _trackedNPCs)
            if (npc != null) _active.npcs.Add(npc.CaptureState());

        _active.props.Clear();
        foreach (var prop in _trackedProps)
            if (prop != null) _active.props.Add(prop.CaptureState());

        _active.lootContainers.Clear();
        foreach (var loot in _trackedLoot)
            if (loot != null) _active.lootContainers.Add(loot.CaptureLootState());

        _active.doors.Clear();
        foreach (var door in _trackedDoors)
            if (door != null) _active.doors.Add(door.CaptureDoorState());

        _active.containers.Clear();
        foreach (var c in _trackedContainers)
            if (c != null) _active.containers.Add(c.CaptureState());

        Directory.CreateDirectory(ScenesSaveDir);
        File.WriteAllText(ActiveSavePath, JsonUtility.ToJson(_active, prettyPrint: true));
    }

    // ── Exit helpers ──────────────────────────────────────────────────────────

    // Call before any transition that should NOT trigger an auto-save (main menu,
    // quit). Disables all tracked NPC GameObjects so NavMeshAgents release their
    // references before the NavMesh surface is destroyed.
    public static void PrepareForExit()
    {
        SaveSystem.SuppressSceneAutoSave = true;

        if (Instance == null) return;
        foreach (var npc in Instance._trackedNPCs)
            if (npc != null) npc.gameObject.SetActive(false);
    }

    // Call from GameManager.StartNewGame() to wipe all scene states so every
    // scene resets to its default on the new playthrough.
    public static void DeleteAllSceneStates()
    {
        if (Directory.Exists(ScenesSaveDir))
        {
            Directory.Delete(ScenesSaveDir, recursive: true);
            Debug.Log("[SceneStateManager] All scene states wiped for new game.");
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    void LoadActiveState()
    {
        _active = null;

        string path = ActiveSavePath;
        if (File.Exists(path))
        {
            try   { _active = JsonUtility.FromJson<SceneStateSave>(File.ReadAllText(path)); }
            catch { Debug.LogWarning($"[SceneStateManager] Corrupt scene state at {path} — resetting."); }
        }

        if (_active == null)
            _active = new SceneStateSave { firstVisit = true };
    }
}
