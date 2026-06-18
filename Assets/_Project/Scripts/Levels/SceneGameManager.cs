using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Base class for every scene's local GameManager.
//
// Subclass this for each scene (e.g. GreyspireBuilder) and override
// OnSceneReady() to add NPC spawning, triggers, cutscenes, etc.
//
// Spawn position:
//   defaultSpawnPosition / defaultSpawnRotationEuler are used on first visit
//   or when no saved position exists. SaveSystem overrides them after this
//   runs when loading a save.
//
// This replaces the PlayerSpawnPoint GameObject — the position lives here
// in the inspector instead.
public class SceneGameManager : MonoBehaviour
{
    [Header("Player Spawn — Default")]
    [Tooltip("Where the player appears when visiting this scene for the first time " +
             "or when starting a new game. SaveSystem overrides this when loading a save.")]
    public Vector3 defaultSpawnPosition;

    [Tooltip("Euler angles for the player's starting rotation.")]
    public Vector3 defaultSpawnRotationEuler;

    [Header("Spells Granted on Entry")]
    [Tooltip("Spells the player learns when entering this scene. " +
             "SpellbookSystem deduplicates, so revisiting never double-grants.")]
    public List<SpellData> spellsGrantedOnFirstVisit = new();

    // ── Accessors used by PlayerManager ──────────────────────────────────────

    public Vector3    DefaultSpawnPosition => defaultSpawnPosition;
    public Quaternion DefaultSpawnRotation => Quaternion.Euler(defaultSpawnRotationEuler);
    public List<SpellData> SpellsGrantedOnFirstVisit => spellsGrantedOnFirstVisit;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        // Ensure the Persistent scene's singleton systems exist even if this
        // scene was opened directly in the editor. LoadScene(Additive) queues
        // the load for end-of-frame, so PlayerManager may not exist yet — the
        // coroutine below waits one or more frames until it does.
        GameBootstrap.EnsureSystems();
        StartCoroutine(InitWhenReady());
    }

    IEnumerator InitWhenReady()
    {
        while (PlayerManager.Instance == null)
            yield return null;

        PlayerManager.Instance.InitializeForScene(this);
        OnSceneReady();

        // Tell any in-flight Gateway transition the scene is built so its loading
        // overlay can come down.
        SceneTransition.NotifySceneReady();
    }

    // Override in subclasses to spawn NPCs, configure scene state, etc.
    protected virtual void OnSceneReady() { }
}
