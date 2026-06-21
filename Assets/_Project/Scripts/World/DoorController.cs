using UnityEngine;

// Swings a door open/closed about a hinge. Put this on the door object together
// with an InteractableProp (type = Door) — the prop handles the lock/key check
// and calls Toggle() when the player opens it.
//
// The door rotates `hinge` (defaults to this transform) by `openAngle` about
// `axis`. For a believable swing the pivot should be at the hinged edge: either
// author the prefab that way, or drop the door mesh under an empty placed at the
// hinge and assign that empty here.
public class DoorController : MonoBehaviour
{
    public Transform hinge;
    public float openAngle = 95f;
    public float speed = 240f;          // degrees / second
    public Vector3 axis = Vector3.up;

    bool _open;
    Quaternion _closedRot, _openRot;

    public bool IsOpen => _open;

    void Awake()
    {
        if (hinge == null) hinge = transform;
        _closedRot = hinge.localRotation;
        _openRot   = _closedRot * Quaternion.AngleAxis(openAngle, axis);
    }

    public void Toggle() => _open = !_open;
    public void Open()   => _open = true;
    public void Close()  => _open = false;

    // Snap to a state with no animation (used when restoring saved state).
    public void SetOpenInstant(bool open)
    {
        if (hinge == null) hinge = transform;
        _open = open;
        hinge.localRotation = open ? _openRot : _closedRot;
    }

    void Update()
    {
        var target = _open ? _openRot : _closedRot;
        hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, target, speed * Time.deltaTime);
    }
}
