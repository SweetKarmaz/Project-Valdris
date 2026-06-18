using System.Collections.Generic;
using UnityEngine;

public enum GatewayTrigger { Interact, EnterZone }
public enum GatewayAction  { LoadScene, TeleportInScene }

// Attach to a door/prop (Interact) or an invisible trigger volume (EnterZone)
// to move the player to another scene or to a SpawnPoint in the current scene.
//
//  • Interact   — InteractionHUD shows a reticle prompt; left-click activates.
//  • EnterZone  — needs a Collider with "Is Trigger" enabled; activates when the
//                 player walks into it.
//
// Destinations are addressed by SpawnPoint.spawnId. Place a SpawnPoint in the
// target scene (LoadScene) or the current scene (TeleportInScene) and reference
// its id here.
[DisallowMultipleComponent]
public class Gateway : MonoBehaviour
{
    public static readonly List<Gateway> All = new();

    [Header("Activation")]
    public GatewayTrigger trigger = GatewayTrigger.Interact;
    [Tooltip("Interact mode: how close the player must be (metres) for the prompt to appear.")]
    public float interactionRange = 3f;
    [Tooltip("Interact mode: text shown in the reticle prompt.")]
    public string interactionLabel = "Enter";

    [Header("Action")]
    public GatewayAction action = GatewayAction.LoadScene;
    [Tooltip("LoadScene: name of the scene to load. It must be added to Build Settings.")]
    public string targetSceneName;
    [Tooltip("Id of the SpawnPoint to place the player at — in the destination scene " +
             "(LoadScene) or this scene (TeleportInScene).")]
    public string destinationSpawnId;
    [Tooltip("Name shown on the loading screen. The text reads 'Loading {this}…' " +
             "(e.g. 'the City of Zarn'). Ignored for TeleportInScene.")]
    public string destinationDisplayName;

    [Header("Confirmation (optional)")]
    [Tooltip("Show a Stay / Continue prompt before activating (e.g. a point of no return).")]
    public bool requireConfirmation;
    public string confirmTitle = "Leave this area?";
    [TextArea] public string confirmMessage = "If you continue you will not be able to return.";
    public string stayLabel     = "Stay";
    public string continueLabel = "Continue";

    bool _confirming;

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    void Awake() => SceneTransition.EnsureExists();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }

    void OnDisable()
    {
        All.Remove(this);
        if (_confirming) { _confirming = false; UIModal.Pop(); }
    }

    // Label surfaced to InteractionHUD for the reticle prompt.
    public string Label => string.IsNullOrEmpty(interactionLabel) ? "Enter" : interactionLabel;

    // ── Activation ──────────────────────────────────────────────────────────────

    // Called by InteractionHUD (Interact mode) or OnTriggerEnter (EnterZone mode).
    public void Activate()
    {
        if (_confirming) return;
        if (requireConfirmation) { _confirming = true; UIModal.Push(); }
        else Execute();
    }

    void Execute()
    {
        switch (action)
        {
            case GatewayAction.LoadScene:
                SceneTransition.Instance?.LoadScene(targetSceneName, destinationSpawnId, destinationDisplayName);
                break;
            case GatewayAction.TeleportInScene:
                SceneTransition.Instance?.TeleportInScene(destinationSpawnId);
                break;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (trigger != GatewayTrigger.EnterZone) return;
        bool isPlayer = other.CompareTag("Player")
                        || other.GetComponentInParent<PlayerStats>() != null;
        if (isPlayer) Activate();
    }

    // ── Confirmation dialog (IMGUI) ─────────────────────────────────────────────

    void OnGUI()
    {
        if (!_confirming) return;
        if (GameUI.IsOpen || PauseMenuController.IsPaused) return;

        const float PW = 460f, PH = 210f;
        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;

        // Solid opaque panel.
        GUI.color = new Color(0.04f, 0.04f, 0.05f, 1f);
        GUI.DrawTexture(new Rect(px, py, PW, PH), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(new Rect(px, py, PW, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(px, py + PH - 1f, PW, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(px, py, 1f, PH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(px + PW - 1f, py, 1f, PH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var title = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(px + 10f, py + 16f, PW - 20f, 30f), confirmTitle, title);

        var body = new GUIStyle(GUI.skin.label)
            { wordWrap = true, alignment = TextAnchor.UpperCenter, fontSize = 14 };
        body.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        GUI.Label(new Rect(px + 24f, py + 56f, PW - 48f, 86f), confirmMessage, body);

        GUI.skin.button.fontSize = 15;
        float bw = 150f, bh = 42f, gap = 20f;
        float by = py + PH - bh - 18f;
        float bx = px + (PW - (bw * 2f + gap)) / 2f;

        if (GUI.Button(new Rect(bx, by, bw, bh), stayLabel))
        {
            _confirming = false;
            UIModal.Pop();
        }
        if (GUI.Button(new Rect(bx + bw + gap, by, bw, bh), continueLabel))
        {
            _confirming = false;
            UIModal.Pop();
            Execute();
        }
    }
}
