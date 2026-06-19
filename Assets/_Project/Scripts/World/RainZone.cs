using UnityEngine;

// A trigger volume that turns on the screen-rain overlay while the player is
// inside it. Drop it (with a trigger collider) anywhere rain should fall — the
// VaelCrossing corruption builder adds one over the southern band.
[RequireComponent(typeof(Collider))]
public class RainZone : MonoBehaviour
{
    [Range(0f, 1f)] public float intensity = 1f;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other)) ScreenRainOverlay.EnsureExists().TargetIntensity = intensity;
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other) && ScreenRainOverlay.Instance != null)
            ScreenRainOverlay.Instance.TargetIntensity = 0f;
    }

    static bool IsPlayer(Collider other) =>
        other.CompareTag("Player") || other.GetComponentInParent<PlayerStats>() != null;
}
