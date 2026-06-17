using UnityEngine;
using TMPro;

// A single piece of floating world-space text (e.g. "Resisted!"). Rises,
// fades, faces the camera, then destroys itself. Spawned by CombatTextSystem.
[RequireComponent(typeof(TextMeshPro))]
public class FloatingCombatText : MonoBehaviour
{
    public float riseSpeed = 1.5f;
    public float lifetime = 1.5f;

    private TextMeshPro _text;
    private float _age;
    private Color _startColor;

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
        _startColor = _text.color;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= lifetime) { Destroy(gameObject); return; }

        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // Fade out over the second half of the lifetime.
        float fade = Mathf.Clamp01(2f - 2f * _age / lifetime);
        _text.color = new Color(_startColor.r, _startColor.g, _startColor.b, _startColor.a * fade);

        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
