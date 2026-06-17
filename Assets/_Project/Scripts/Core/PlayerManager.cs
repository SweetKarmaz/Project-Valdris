using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;

// Lives in the Persistent scene (DontDestroyOnLoad).
// Single owner of the player GameObject across all scene loads.
//
// Responsibilities:
//   - Spawns the player once on first use; repositions on subsequent scene loads.
//   - Holds which character model prefab is currently active (can change mid-playthrough).
//   - Wires up the camera every scene load.
//   - Grants baseline spells that should always be available.
//
// Scene-local GameManagers (SceneGameManager subclasses) call
// InitializeForScene() from their Start(). SaveSystem then overrides the
// position and model if a save is being loaded.
[DisallowMultipleComponent]
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Character Model")]
    [Tooltip("Default model used when starting a new game.")]
    public GameObject defaultCharacterPrefab;

    [Tooltip("All character models the player can have. Used to resolve a saved prefab name on load.")]
    public List<GameObject> characterPrefabs = new();

    [Header("Base Spells")]
    [Tooltip("Spells the player always starts with, regardless of scene. " +
             "Add scene-specific spells to the SceneGameManager instead.")]
    public List<SpellData> baseStartingSpells = new();

    [Header("Weapon Grip (tune live; applies to all items of each type)")]
    [Tooltip("One-handed weapons: rotation/position offset in the right hand.")]
    public Vector3 oneHandedGripRotation = new Vector3(285f, 0f, 0f);
    public Vector3 oneHandedGripPosition = new Vector3(0f, 0.025f, 0f);
    [Tooltip("Two-handed weapons: rotation/position offset in the right hand " +
             "(the idle pose places the left hand along the same line).")]
    public Vector3 twoHandedGripRotation = new Vector3(285f, -15f, 0f);
    public Vector3 twoHandedGripPosition = new Vector3(-0.1f, -0.02f, 0.3f);
    [Tooltip("Shields (off hand): rotation/position offset in the left hand.")]
    public Vector3 shieldGripRotation = new Vector3(200f, 110f, 50f);
    public Vector3 shieldGripPosition = new Vector3(-0.05f, 0f, 0f);

    [Header("Inventory Pose Clips")]
    [Tooltip("Idle shown in the inventory portrait while a ONE-handed weapon is equipped.")]
    public AnimationClip oneHandedIdlePose;
    [Tooltip("Idle shown in the inventory portrait while a TWO-handed weapon is equipped.")]
    public AnimationClip twoHandedIdlePose;

    [Header("Starting Inventory (new game only)")]
    [Tooltip("Items the player begins a NEW game with. Ignored when loading a save " +
             "(the save's inventory is restored instead). dropChance is unused here.")]
    public List<NpcItem> startingInventory = new();
    public int startingGold;

    [Header("Debug")]
    public bool spawnPlayer = true;

    // ── Runtime state ─────────────────────────────────────────────────────────

    GameObject _activeCharacterPrefab;
    GameObject _player;

    public GameObject Player => _player != null ? _player
        : (_player = GameObject.FindGameObjectWithTag("Player"));

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _activeCharacterPrefab = defaultCharacterPrefab;
        CharacterPreviewCamera.EnsureExists();
    }

    // ── Called by SceneGameManager every scene load ────────────────────────────

    public void InitializeForScene(SceneGameManager scene)
    {
        _player = GameObject.FindGameObjectWithTag("Player");

        if (_player == null)
        {
            if (spawnPlayer)
                _player = CreatePlayer(scene.DefaultSpawnPosition, scene.DefaultSpawnRotation);
        }
        else if (SaveSystem.IsRestoringFromSave && SaveSystem.PendingPlayerPosition.HasValue)
        {
            // Restore saved position. Disabling the CharacterController before the
            // transform write prevents the CC's internal physics capsule from fighting
            // the new position on the next physics step.
            WarpPlayer(_player,
                       SaveSystem.PendingPlayerPosition.Value,
                       SaveSystem.PendingPlayerRotation ?? Quaternion.identity);
            SaveSystem.PendingPlayerPosition = null;
            SaveSystem.PendingPlayerRotation = null;
        }
        else if (!SaveSystem.IsRestoringFromSave)
        {
            // Fresh scene visit — move to this scene's default spawn.
            WarpPlayer(_player, scene.DefaultSpawnPosition, scene.DefaultSpawnRotation);
        }

        // Flag is consumed — clear it so subsequent scene transitions behave normally.
        SaveSystem.IsRestoringFromSave = false;

        if (_player == null) return;

        GrantSpells(baseStartingSpells);
        GrantSpells(scene.SpellsGrantedOnFirstVisit);
        SetupCamera(_player);
    }

    // Public entry point kept for any external callers (cutscenes, teleports, etc.).
    public void RepositionPlayer(Vector3 position, Quaternion rotation)
    {
        _player = Player;
        if (_player != null) WarpPlayer(_player, position, rotation);
    }

    // Disables the CharacterController around the transform write so the CC's
    // internal physics capsule syncs cleanly to the new position. Without this,
    // the CC can fight the new position on the following physics step.
    static void WarpPlayer(GameObject player, Vector3 position, Quaternion rotation)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(position, rotation);
        if (cc != null) cc.enabled = true;
    }

    // Swap the visible character model (e.g. player transforms mid-game).
    // Replaces only the model child; all other components are preserved.
    public void ChangeCharacterModel(GameObject newPrefab)
    {
        if (_player == null || newPrefab == null) return;
        _activeCharacterPrefab = newPrefab;

        var oldAnim = _player.GetComponentInChildren<Animator>();
        if (oldAnim != null) Destroy(oldAnim.gameObject);

        var newModel = AttachModel(_player, newPrefab);
        var appearance = _player.GetComponent<PlayerAppearanceComponent>();
        if (appearance != null && newModel != null)
        {
            appearance.modelRoot = newModel.transform;
            appearance.isFemale  = newModel.transform.Find("Female_03_Torso") != null;
        }

        CharacterPreviewCamera.Instance?.SetTarget(newModel != null
            ? newModel.transform
            : _player.transform);

        // CharacterAnimator caches the child Animator reference — refresh it so
        // it finds the new model. Destroy + re-add is not safe here because
        // Destroy() is deferred and [DisallowMultipleComponent] rejects a second
        // add before the first is gone.
        _player.GetComponent<CharacterAnimator>()?.Refresh();
        _player.GetComponent<InventoryPoser>()?.RefreshAnimator();
    }

    // Returns the asset name of the active prefab so SaveSystem can persist it.
    public string ActiveCharacterPrefabName =>
        _activeCharacterPrefab != null ? _activeCharacterPrefab.name : string.Empty;

    // Finds a character prefab by asset name (used when restoring a save).
    public GameObject FindCharacterByName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;
        foreach (var p in characterPrefabs)
            if (p != null && p.name == assetName) return p;
        if (defaultCharacterPrefab != null && defaultCharacterPrefab.name == assetName)
            return defaultCharacterPrefab;
        return null;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    GameObject CreatePlayer(Vector3 pos, Quaternion rot)
    {
        var player = new GameObject("Player") { tag = "Player" };
        player.transform.SetPositionAndRotation(pos, rot);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.9f, 0);

        var obstacle = player.AddComponent<NavMeshObstacle>();
        obstacle.shape   = NavMeshObstacleShape.Capsule;
        obstacle.radius  = 0.5f;
        obstacle.height  = 1.9f;
        obstacle.carving = false;

        var modelGO = AttachModel(player, _activeCharacterPrefab);

        player.AddComponent<PlayerStats>();
        player.AddComponent<CharacterBuffs>();
        player.AddComponent<CharacterResistances>();
        player.AddComponent<PlayerController>();

        // Ensure the HUD overlay exists (once, persistent across scenes).
        if (HUDController.Instance == null)
        {
            var hudGO = new GameObject("HUDController");
            DontDestroyOnLoad(hudGO);
            hudGO.AddComponent<HUDController>();
        }

        var combat = player.AddComponent<PlayerCombat>();
        // NPCs live on the Default layer; include it plus any future NPC-specific layers.
        combat.enemyLayer = LayerMask.GetMask("Default");

        player.AddComponent<PlayerRanged>();
        player.AddComponent<PlayerThrown>();

        var viewmodel = player.AddComponent<PlayerViewmodel>();
        viewmodel.vmLayer = LayerMask.NameToLayer("WeaponVM");
        player.AddComponent<Spellcaster>();
        player.AddComponent<PlayerMagic>();
        player.AddComponent<PlayerDetection>();
        player.AddComponent<CharacterAnimator>();
        player.AddComponent<PlayerAnimator>();

        var poser = player.AddComponent<InventoryPoser>();
        poser.oneHandedIdlePose = oneHandedIdlePose;
        poser.twoHandedIdlePose = twoHandedIdlePose;

        var appearance = player.AddComponent<PlayerAppearanceComponent>();
        if (modelGO != null)
        {
            appearance.modelRoot = modelGO.transform;
            appearance.isFemale  = modelGO.transform.Find("Female_03_Torso") != null;
        }

        // Wire the inventory portrait camera to the new model root.
        CharacterPreviewCamera.Instance?.SetTarget(modelGO != null
            ? modelGO.transform
            : player.transform);

        // Grant the starting inventory only on a fresh game — when loading a save,
        // the saved inventory is restored instead.
        if (!SaveSystem.IsRestoringFromSave)
            GrantStartingInventory();

        DontDestroyOnLoad(player);
        return player;
    }

    static GameObject AttachModel(GameObject player, GameObject modelPrefab)
    {
        if (modelPrefab != null)
        {
            var model = Instantiate(modelPrefab, player.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var anim = model.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.applyRootMotion = false;
                // The body sits on the UICharacter layer (off the main camera), so
                // keep it animating regardless of main-camera visibility — otherwise
                // the inventory portrait pose/animation can freeze when culled.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // Put the whole model on the UICharacter layer. The main camera
            // excludes this layer (see SetupCamera) so the player never sees
            // their own body in first person, while the inventory portrait
            // camera renders ONLY this layer. includeInactive so mesh variants
            // toggled on later by equipment are already on the right layer.
            int uiLayer = LayerMask.NameToLayer(CharacterPreviewCamera.LayerName);
            if (uiLayer >= 0)
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = uiLayer;
            else
                Debug.LogWarning($"[PlayerManager] Layer '{CharacterPreviewCamera.LayerName}' " +
                    "not found. Open the project once so Editor/UICharacterLayerSetup creates it.");

            // Chr_Npc_Base carries NpcAppearanceComponent, which disables ALL mesh
            // children in Awake() (BuildLists sets each to SetActive(false)) and
            // is supposed to re-enable the correct subset in Start() via Randomize().
            // For the player we don't want randomization, so we:
            //   1. Block Start() from calling Randomize() via randomizeOnAwake = false
            //   2. Re-enable the base variant (index 0) of every body group so the
            //      character is visible; PlayerAppearanceComponent overrides slots
            //      when gear is equipped.
            //   3. Destroy the NpcAppearanceComponent — not needed on the player.
            var npcAppearance = model.GetComponent<NpcAppearanceComponent>();
            if (npcAppearance != null)
            {
                npcAppearance.randomizeOnAwake = false;
                npcAppearance.EnableDefaultMeshes();
                Destroy(npcAppearance);
            }

            return model;
        }
        else
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0, 0.9f, 0);
            visual.GetComponent<Renderer>().material.color = new Color(0.7f, 0.6f, 0.5f);
            return visual;
        }
    }

    void GrantStartingInventory()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;
        foreach (var entry in startingInventory)
            if (entry.lootItem != null)
                inv.AddLootItem(entry.lootItem, Mathf.Max(1, entry.quantity));
        if (startingGold > 0) inv.AddGold(startingGold);
    }

    static void GrantSpells(List<SpellData> spells)
    {
        if (SpellbookSystem.Instance == null || spells == null) return;
        foreach (var spell in spells)
            SpellbookSystem.Instance.LearnSpell(spell);
        if (SpellbookSystem.Instance.GetSlot(0) == null && spells.Count > 0)
            SpellbookSystem.Instance.SetSlot(0, spells[0]);
    }

    static void SetupCamera(GameObject player)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }
        // Remove legacy third-person camera if present.
        var third = cam.GetComponent<ThirdPersonCamera>();
        if (third != null) Destroy(third);

        // Exclude the UICharacter layer so the player never sees their own body
        // in first person. The inventory portrait camera renders that layer.
        int uiLayer = LayerMask.NameToLayer(CharacterPreviewCamera.LayerName);
        if (uiLayer >= 0)
        {
            cam.cullingMask &= ~(1 << uiLayer);

            // Also exclude the portrait's global Volume (which lives on the
            // UICharacter layer with fixed exposure + no sky) from the main
            // camera's volume mask — otherwise it would black out the game view.
            var hdMain = cam.GetComponent<HDAdditionalCameraData>()
                      ?? cam.gameObject.AddComponent<HDAdditionalCameraData>();
            hdMain.volumeLayerMask &= ~(1 << uiLayer);
        }

        var fp = cam.GetComponent<FirstPersonCamera>();
        if (fp == null) fp = cam.gameObject.AddComponent<FirstPersonCamera>();
        fp.SetTarget(player.transform);

        if (cam.GetComponent<InteractionHUD>() == null)
            cam.gameObject.AddComponent<InteractionHUD>();

        // Create (or find) the projectile spawn point — a child of the camera
        // positioned just forward and slightly down from the lens, so arrows
        // appear to nock from the bow rather than shoot from the player's feet.
        Transform spawnPoint = cam.transform.Find("ProjectileSpawnPoint");
        if (spawnPoint == null)
        {
            var spawnGO = new GameObject("ProjectileSpawnPoint");
            spawnGO.transform.SetParent(cam.transform, false);
            spawnGO.transform.localPosition = new Vector3(0f, -0.1f, 0.3f);
            spawnGO.transform.localRotation = Quaternion.identity;
            spawnPoint = spawnGO.transform;
        }

        // Wire the spawn point to both ranged systems.
        var ranged = player.GetComponent<PlayerRanged>();
        if (ranged != null)
            ranged.spawnPoint = spawnPoint;

        var thrown = player.GetComponent<PlayerThrown>();
        if (thrown != null)
            thrown.spawnPoint = spawnPoint;
    }
}
