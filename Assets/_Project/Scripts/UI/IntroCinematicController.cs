using UnityEngine;

// Placeholder intro cinematic: fades through title cards for a fixed
// duration. Escape (or the on-screen Skip button) bypasses it. When a real
// cinematic exists (Timeline/video), replace the OnGUI body and keep the
// Finish() call — Act 1 always starts from here.
public class IntroCinematicController : MonoBehaviour
{
    [TextArea]
    public string[] cards =
    {
        "The corruption came to Valdris quietly,\nlike frost in the first weeks of autumn.",
        "Greyspire endured. It always had.\nThe mountain folk are slow to kneel.",
        "But what sleeps beneath the crypt\ndoes not care who kneels."
    };
    public float secondsPerCard = 5f;

    private float _elapsed;
    private bool _finished;

    private void Awake()
    {
        GameBootstrap.EnsureSystems();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (InputManager.SkipPressed) Finish();
        else if (_elapsed >= secondsPerCard * cards.Length) Finish();
    }

    private void Finish()
    {
        if (_finished) return; // guard against double scene loads
        _finished = true;
        GameManager.Instance?.BeginAct1();
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

        int index = Mathf.Min((int)(_elapsed / secondsPerCard), cards.Length - 1);
        float cardTime = _elapsed - index * secondsPerCard;
        // Fade in/out within each card.
        float alpha = Mathf.Clamp01(Mathf.Min(cardTime, secondsPerCard - cardTime) * 2f);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            richText = true
        };
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), cards[index], style);

        GUI.color = Color.white;
        if (GUI.Button(new Rect(Screen.width - 170, Screen.height - 70, 150, 46), "Skip (Esc)"))
            Finish();
    }
}
