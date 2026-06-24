using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Everything a save file records. Asset references are stored by asset name
// and resolved through the GameDatabase on load.
[Serializable]
public class SaveData
{
    // Progression
    public int playerLevel;
    public int unspentSkillPoints;
    public int unspentAttributePoints;
    public long currentXP;
    public PlayerStats.AttributeSave attributes;
    public List<string> unlockedSkills;
    public List<string> knownSpells;
    public List<string> spellSlots;

    // Possessions
    public List<InventorySlotSave> inventorySlots;  // LootItem stacking inventory
    public List<EquippedLootSave>  equippedLoot;    // equipped armor (LootItem) per slot
    public int gold;

    // Active effects
    public List<SavedBuff> appliedBuffs;

    // Location
    public string currentScene;
    public Vector3 playerPosition;
    public Vector3 playerRotationEuler;
    public float   cameraPitch;

    // Character
    public string playerCharacterPrefabName; // asset name of the active model prefab

    // Story
    public List<QuestRuntimeSave> questStates;
    public float corruptionLevel;

    // World clock
    public int   dayNumber;
    public float timeOfDay;

    // Collected key names (the Keyring)
    public List<string> keyRing;

    // Binary world choices: who was saved, which side was taken, crypt
    // cleared, etc. Other systems read these to vary the world.
    public List<WorldFlag> worldStateFlags;

    public string savedAtUtc;
}

// Lightweight info for save-list UI, parsed from each save file.
public class SaveSummary
{
    public string fileName;
    public string sceneName;
    public int playerLevel;
    public DateTime savedAtUtc;
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    // Set to true before loading a scene so systems know to skip their default
    // initialisation (e.g. SceneGameManager.InitializeForScene won't reposition
    // the player). Cleared by PlayerManager.InitializeForScene after it runs.
    public static bool IsRestoringFromSave { get; set; }

    // Set to true before any non-gameplay scene transition (main menu, quit).
    // SceneStateManager.OnDestroy() skips the auto-save while this is true so
    // the scene JSON is never corrupted by a mid-unload partial snapshot.
    // Cleared at the start of LoadGame() so the next gameplay load is clean.
    public static bool SuppressSceneAutoSave { get; set; }

    // Written in RestorePlayerOnSceneLoaded; read and cleared by
    // PlayerManager.InitializeForScene so the saved position is applied at the
    // correct point in the initialisation chain (after model swap, before camera).
    // Stored separately from RepositionPlayer so the CharacterController can be
    // safely disabled/re-enabled around the transform write.
    public static Vector3?    PendingPlayerPosition { get; set; }
    public static Quaternion? PendingPlayerRotation { get; set; }

    // Written in RestorePlayerOnSceneLoaded; read and cleared by
    // FirstPersonCamera.SetTarget so the saved pitch is applied after the camera
    // mounts to the player.
    public static float? PendingCameraPitch { get; set; }

    public GameDatabase database;

    private static string SavesDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    // Companion folder holding the scene-state snapshot for a given save file.
    // e.g. save_20260618_120000.json -> Saves/save_20260618_120000_scenes/
    private static string SceneSnapshotDir(string saveFileName) =>
        Path.Combine(SavesDirectory, Path.GetFileNameWithoutExtension(saveFileName) + "_scenes");

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(SavesDirectory);
    }

    // ---- Save-slot management ----

    public List<SaveSummary> ListSaves()
    {
        var summaries = new List<SaveSummary>();
        foreach (string path in Directory.GetFiles(SavesDirectory, "*.json"))
        {
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                summaries.Add(new SaveSummary
                {
                    fileName = Path.GetFileName(path),
                    sceneName = data.currentScene,
                    playerLevel = data.playerLevel,
                    savedAtUtc = DateTime.TryParse(data.savedAtUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : DateTime.MinValue
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unreadable save file {path}: {e.Message}");
            }
        }
        return summaries.OrderByDescending(s => s.savedAtUtc).ToList();
    }

    public SaveSummary GetLatestSave() => ListSaves().FirstOrDefault();

    public bool HasAnySave => ListSaves().Count > 0;

    public void DeleteSave(string fileName)
    {
        string path = Path.Combine(SavesDirectory, fileName);
        if (File.Exists(path)) File.Delete(path);

        // Remove the save's scene-state snapshot too.
        string snapshot = SceneSnapshotDir(fileName);
        if (Directory.Exists(snapshot)) Directory.Delete(snapshot, recursive: true);
    }

    // ---- Saving ----

    // Each save is its own timestamped file (its "persistent data");
    // deleting the file deletes everything about that save.
    public void Save()
    {
        // Flush the current scene into the live session folder, then write the
        // save file and freeze a snapshot of the whole session world alongside it.
        SceneStateManager.Instance?.SaveState();
        SaveData data = CaptureState();
        string fileName = $"save_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        File.WriteAllText(Path.Combine(SavesDirectory, fileName), JsonUtility.ToJson(data, prettyPrint: true));

        // Snapshot the live session scene-states so loading this save reverts the
        // world (visited zones, dead NPCs, looted containers) to this moment.
        CopyDirectory(SceneStateManager.SessionScenesDir, SceneSnapshotDir(fileName));
        Debug.Log($"Game saved: {fileName}");
    }

    // ---- Loading ----

    // Restores all persistent-system state, loads the saved scene, then
    // restores player-local state once the scene is ready.
    public void LoadGame(string fileName)
    {
        string path = Path.Combine(SavesDirectory, fileName);
        if (!File.Exists(path)) { Debug.LogWarning($"Save not found: {fileName}"); return; }
        if (database == null) { Debug.LogError("SaveSystem has no GameDatabase assigned; cannot load."); return; }

        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        RestoreGlobalState(data);

        // Replace the live session world with this save's frozen snapshot so every
        // scene reverts to its state at save time (zones the player hadn't visited
        // become first-visit/pristine again).
        RestoreSessionScenes(fileName);

        SuppressSceneAutoSave = false; // safe to auto-save again in gameplay
        IsRestoringFromSave   = true;
        SceneManager.sceneLoaded += RestorePlayerOnSceneLoaded;
        _pendingPlayerState = data;
        SceneManager.LoadScene(data.currentScene);
    }

    private SaveData _pendingPlayerState;

    private void RestorePlayerOnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= RestorePlayerOnSceneLoaded;
        SaveData data = _pendingPlayerState;
        _pendingPlayerState = null;
        if (data == null) return;

        // Restore character model first (PlayerManager may need to swap it before
        // position is applied so the new model is in the right place).
        if (PlayerManager.Instance != null && !string.IsNullOrEmpty(data.playerCharacterPrefabName))
        {
            var prefab = PlayerManager.Instance.FindCharacterByName(data.playerCharacterPrefabName);
            if (prefab != null) PlayerManager.Instance.ChangeCharacterModel(prefab);
        }

        // Store position and pitch as pending values so PlayerManager.InitializeForScene
        // can apply them at the right time (after model swap, with CC safely disabled).
        PendingPlayerPosition = data.playerPosition;
        PendingPlayerRotation = Quaternion.Euler(data.playerRotationEuler);
        PendingCameraPitch    = data.cameraPitch;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogWarning("Loaded scene has no Player to restore."); return; }
        player.GetComponent<CharacterBuffs>()?.RestoreState(data.appliedBuffs, database);
        // Restore allocated attributes BEFORE reviving, so max health/mana reflect them.
        player.GetComponent<PlayerStats>()?.RestoreAttributes(data.attributes);
        // Vitals aren't persisted — restore the player to full health/mana so a save
        // made (or loaded) after death doesn't spawn the player dead.
        player.GetComponent<PlayerStats>()?.ReviveFull();
    }

    // Wipes the live session scene-states and replaces them with the given save's
    // snapshot. If the save has no snapshot (e.g. an old save from before this
    // system), the session is simply cleared so every scene starts pristine.
    private void RestoreSessionScenes(string saveFileName)
    {
        string sessionDir = SceneStateManager.SessionScenesDir;
        if (Directory.Exists(sessionDir)) Directory.Delete(sessionDir, recursive: true);

        string snapshot = SceneSnapshotDir(saveFileName);
        if (Directory.Exists(snapshot)) CopyDirectory(snapshot, sessionDir);
        else Directory.CreateDirectory(sessionDir);
    }

    // Flat copy of the scene-state folder (it contains only *_scene.json files).
    private static void CopyDirectory(string source, string dest)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    }

    // ---- State capture/restore ----

    private SaveData CaptureState()
    {
        var data = new SaveData
        {
            playerLevel = LevelSystem.Instance != null ? LevelSystem.Instance.CurrentLevel : 1,
            unspentSkillPoints = LevelSystem.Instance != null ? LevelSystem.Instance.UnspentSkillPoints : 0,
            unspentAttributePoints = LevelSystem.Instance != null ? LevelSystem.Instance.UnspentAttributePoints : 0,
            currentXP = XPSystem.Instance != null ? XPSystem.Instance.CurrentXP : 0,
            unlockedSkills = SkillSystem.Instance?.CaptureState() ?? new List<string>(),
            knownSpells = SpellbookSystem.Instance?.CaptureKnown() ?? new List<string>(),
            spellSlots = SpellbookSystem.Instance?.CaptureSlots() ?? new List<string>(),
            inventorySlots = InventorySystem.Instance?.CaptureState() ?? new List<InventorySlotSave>(),
            equippedLoot   = InventorySystem.Instance?.CaptureEquippedLoot() ?? new List<EquippedLootSave>(),
            gold           = InventorySystem.Instance?.Gold ?? 0,
            questStates = QuestSystem.Instance?.CaptureState() ?? new List<QuestRuntimeSave>(),
            corruptionLevel = CorruptionTracker.Instance != null ? CorruptionTracker.Instance.corruptionLevel : 0f,
            dayNumber = GameClock.Instance != null ? GameClock.Instance.Day : 1,
            timeOfDay = GameClock.Instance != null ? GameClock.Instance.TimeOfDay : 8f,
            keyRing = Keyring.Instance != null ? Keyring.Instance.Capture() : new List<string>(),
            worldStateFlags = WorldStateSystem.Instance?.CaptureState() ?? new List<WorldFlag>(),
            currentScene = SceneManager.GetActiveScene().name,
            savedAtUtc = DateTime.UtcNow.ToString("o")
        };

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerPosition      = player.transform.position;
            data.playerRotationEuler = player.transform.eulerAngles;
            data.appliedBuffs        = player.GetComponent<CharacterBuffs>()?.CaptureState() ?? new List<SavedBuff>();
            data.attributes          = player.GetComponent<PlayerStats>()?.CaptureAttributes();
        }
        var fp = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
        if (fp != null) data.cameraPitch = fp.Pitch;
        if (PlayerManager.Instance != null)
            data.playerCharacterPrefabName = PlayerManager.Instance.ActiveCharacterPrefabName;

        return data;
    }

    private void RestoreGlobalState(SaveData data)
    {
        LevelSystem.Instance?.RestoreState(data.playerLevel, data.unspentSkillPoints, data.unspentAttributePoints);
        XPSystem.Instance?.RestoreState(data.currentXP);
        SkillSystem.Instance?.RestoreState(data.unlockedSkills, database);
        SpellbookSystem.Instance?.RestoreState(data.knownSpells, data.spellSlots, database);
        InventorySystem.Instance?.RestoreState(data.inventorySlots, database);
        InventorySystem.Instance?.RestoreEquippedLoot(data.equippedLoot,
            database != null ? database.lootRegistry : null);
        if (InventorySystem.Instance != null) InventorySystem.Instance.AddGold(data.gold);
        QuestSystem.Instance?.RestoreState(data.questStates, database);
        WorldStateSystem.Instance?.RestoreState(data.worldStateFlags);
        if (CorruptionTracker.Instance != null)
            CorruptionTracker.Instance.corruptionLevel = data.corruptionLevel;

        // Restore the clock (old saves have dayNumber 0 → default to Day 1, 08:00).
        GameClock.EnsureExists();
        if (data.dayNumber > 0) GameClock.Instance.SetTime(data.dayNumber, data.timeOfDay);
        else                    GameClock.Instance.ResetClock();

        Keyring.EnsureExists();
        Keyring.Instance.Restore(data.keyRing);
    }
}
