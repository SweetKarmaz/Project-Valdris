using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Drives a directional light from the GameClock: arcs the sun across the sky,
// scales intensity/colour through dawn→noon→dusk, and drops to faint cool
// moonlight at night (sun fully set). With the default 60-min day and the
// 3:00–21:00 daylight window, night (sun set) lasts ~15 real minutes.
//
// Runs in edit mode too (ExecuteAlways): when not playing it uses a fixed
// preview hour so the scene is lit while you work.
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class DayNightSun : MonoBehaviour
{
    [Tooltip("Hours the sun is above the horizon. Outside this = night (sun set).")]
    public float dayStartHour = 3f;
    public float dayEndHour   = 21f;
    public float maxElevation = 85f;
    public float dayIntensityLux   = 100000f;
    public float nightIntensityLux = 500f;
    [Tooltip("Hour used to light the scene in the editor when not in play mode.")]
    public float editorPreviewHour = 10f;

    Light _light;
    HDAdditionalLightData _hd;

    void OnEnable()
    {
        _light = GetComponent<Light>();
        _hd = GetComponent<HDAdditionalLightData>();
    }

    void Update()
    {
        if (_light == null) return;
        float hour = (Application.isPlaying && GameClock.Instance != null)
            ? GameClock.Instance.TimeOfDay : editorPreviewHour;

        bool day = hour >= dayStartHour && hour <= dayEndHour;
        if (day)
        {
            float t  = Mathf.InverseLerp(dayStartHour, dayEndHour, hour); // 0 dawn → 1 dusk
            float up = Mathf.Sin(t * Mathf.PI);                           // 0..1 height
            transform.rotation = Quaternion.Euler(Mathf.Max(1f, up * maxElevation),
                                                  Mathf.Lerp(80f, -80f, t), 0f);
            SetIntensity(up * dayIntensityLux);
            _light.color   = Color.Lerp(new Color(1f, 0.68f, 0.42f), new Color(1f, 0.96f, 0.88f), up);
            _light.shadows = LightShadows.Soft;
            if (_hd != null) _hd.interactsWithSky = true;
        }
        else
        {
            // Night: faint cool moonlight from above; no sun disk in the sky.
            transform.rotation = Quaternion.Euler(60f, 200f, 0f);
            SetIntensity(nightIntensityLux);
            _light.color   = new Color(0.6f, 0.7f, 1f);
            _light.shadows = LightShadows.None;
            if (_hd != null) _hd.interactsWithSky = false;
        }
    }

    void SetIntensity(float lux)
    {
        // HDRP (2023.3+) drives intensity through Light.intensity, in the light's
        // unit — Lux for a directional light.
        _light.intensity = lux;
    }
}
