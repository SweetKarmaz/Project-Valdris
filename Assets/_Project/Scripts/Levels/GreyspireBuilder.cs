using UnityEngine;

// Greyspire-specific scene manager.
// Player spawning, camera setup, and spell grants are all handled by the
// base class (SceneGameManager) via PlayerManager. NPCs are placed directly
// in the scene in the editor and managed through SceneStateManager.
public class GreyspireBuilder : SceneGameManager
{
}

// Subtle firelight flicker using layered Perlin noise.
public class TorchFlicker : MonoBehaviour
{
    public float amount = 0.25f;
    public float speed  = 7f;

    Light _light;
    float _baseIntensity, _seed;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(_seed, Time.time * speed) - 0.5f;
        _light.intensity = _baseIntensity * (1f + n * 2f * amount);
    }
}
