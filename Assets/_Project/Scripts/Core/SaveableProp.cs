using UnityEngine;

// Attach to any GameObject that should have its position, rotation, and active
// state persisted by the scene save system.
//
// Set propId in the Inspector to a value that is UNIQUE within the scene and
// STABLE across builds (don't use auto-generated names that can change).
// Good format: "<scene>_<category>_<name>", e.g. "greyspire_chest_guardroom_01".
//
// The SceneStateManager picks this up automatically on Start. If the prop is
// spawned via Instantiate at runtime, the same auto-registration happens.
// To permanently remove a prop via a story beat, call UnregisterAndDestroy().
[DisallowMultipleComponent]
public class SaveableProp : MonoBehaviour
{
    [Tooltip("Globally unique ID for this prop. Must be stable — do not change after a save exists.")]
    public string propId;

    public string PropId => propId;

    void Start()
    {
        if (string.IsNullOrEmpty(propId))
        {
            Debug.LogWarning($"[SaveableProp] '{name}' has no propId — it will not be saved. Set one in the Inspector.");
            return;
        }
        SceneStateManager.Instance?.RegisterProp(this);
    }

    void OnDestroy()
    {
        SceneStateManager.Instance?.UnregisterProp(this);
    }

    // ── Save / restore ────────────────────────────────────────────────────────

    public SavedPropState CaptureState() => new SavedPropState
    {
        id            = propId,
        isActive      = gameObject.activeSelf,
        position      = transform.position,
        rotationEuler = transform.eulerAngles,
        localScale    = transform.localScale,
    };

    public void ApplyState(SavedPropState state)
    {
        gameObject.SetActive(state.isActive);
        transform.SetPositionAndRotation(state.position, Quaternion.Euler(state.rotationEuler));
        transform.localScale = state.localScale;
    }

    // Call this (instead of Destroy) when a story beat permanently removes a prop.
    // The prop is unregistered first so it's absent from the next save and
    // therefore won't be restored on the next scene visit.
    public void UnregisterAndDestroy()
    {
        SceneStateManager.Instance?.UnregisterProp(this);
        Destroy(gameObject);
    }
}
