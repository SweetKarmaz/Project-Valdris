using UnityEngine;
using UnityEngine.UI;

// Full-screen rain effect drawn as a UI overlay (reliable in HDRP, unlike world
// particles). Two scrolling streak layers give depth; intensity is set by zones
// (RainZone) and gusts in and out over time. Persistent singleton.
public class ScreenRainOverlay : MonoBehaviour
{
    public static ScreenRainOverlay Instance { get; private set; }

    [Tooltip("0..1 target set by whatever zone the player is in.")]
    public float TargetIntensity;

    public float fadeSpeed = 0.7f;
    public float gustSpeed = 0.12f;

    float _intensity;
    RawImage _near, _far;
    Canvas _canvas;

    public static ScreenRainOverlay EnsureExists()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ScreenRainOverlay");
        go.AddComponent<ScreenRainOverlay>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;        // above HUD-less world, below pause menus if needed
        gameObject.AddComponent<CanvasScaler>();

        var tex = MakeRainTexture(256, 256);
        _far  = MakeLayer("RainFar",  tex, 3.5f);
        _near = MakeLayer("RainNear", tex, 6f);
    }

    RawImage MakeLayer(string name, Texture tex, float tiling)
    {
        var g = new GameObject(name);
        g.transform.SetParent(transform, false);
        var ri = g.AddComponent<RawImage>();
        ri.texture = tex;
        ri.raycastTarget = false;
        var rt = ri.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        ri.uvRect = new Rect(0f, 0f, tiling, tiling * 1.5f);
        ri.color = new Color(1f, 1f, 1f, 0f);
        return ri;
    }

    void Update()
    {
        _intensity = Mathf.MoveTowards(_intensity, Mathf.Clamp01(TargetIntensity), Time.deltaTime * fadeSpeed);
        bool visible = _intensity > 0.01f;
        if (_canvas.enabled != visible) _canvas.enabled = visible;
        if (!visible) return;

        float gust = Mathf.Lerp(0.45f, 1f, Mathf.PerlinNoise(Time.time * gustSpeed, 0.3f));
        float a = _intensity * gust;
        Scroll(_far,  Time.time * 0.25f, a * 0.5f);
        Scroll(_near, Time.time * 0.5f,  a * 0.75f);
    }

    static void Scroll(RawImage ri, float t, float alpha)
    {
        var r = ri.uvRect; r.y = -t; ri.uvRect = r;
        var c = ri.color;  c.a = alpha; ri.color = c;
    }

    // Procedural tiling rain-streak texture (transparent with faint vertical streaks).
    static Texture2D MakeRainTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

        var rnd = new System.Random(8123);
        int streaks = w / 3;
        for (int s = 0; s < streaks; s++)
        {
            int x = rnd.Next(w);
            int len = rnd.Next(h / 8, h / 2);
            int y0 = rnd.Next(h);
            byte a = (byte)rnd.Next(35, 110);
            for (int l = 0; l < len; l++)
            {
                int y = (y0 + l) % h;
                px[y * w + x] = new Color32(205, 215, 235, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
