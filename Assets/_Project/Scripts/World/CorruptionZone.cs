using UnityEngine;

// A trigger volume that changes the player's Corruption while they're inside.
// Positive corruptionPerSecond = a tainted area (crypts, blighted ground);
// negative = a cleansing area (holy ground around a shrine). Set onEnterOnce for
// a one-shot hit instead of a continuous drip.
//
// Requires a Collider set to "Is Trigger".
[RequireComponent(typeof(Collider))]
public class CorruptionZone : MonoBehaviour
{
    [Tooltip("Corruption added per second while the player is inside. " +
             "Negative cleanses (shrine / holy ground).")]
    public float corruptionPerSecond = 2f;

    [Tooltip("Apply a single amount on entry instead of a continuous drip.")]
    public bool onEnterOnce;
    [Tooltip("One-shot amount when onEnterOnce is set (negative cleanses).")]
    public float onEnterAmount = 10f;

    bool _playerInside;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (onEnterOnce) Apply(onEnterAmount);
        else _playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _playerInside = false;
    }

    void Update()
    {
        if (_playerInside && !onEnterOnce)
            Apply(corruptionPerSecond * Time.deltaTime);
    }

    static void Apply(float amount)
    {
        var ct = CorruptionTracker.Instance;
        if (ct == null || Mathf.Approximately(amount, 0f)) return;
        if (amount > 0f) ct.AddCorruption(amount);
        else             ct.ReduceCorruption(-amount);
    }
}
