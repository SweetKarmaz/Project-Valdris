using UnityEngine;

// Attach to any interactable prop in the scene to offer a quest when the
// player interacts with it. Shows optional dialogue text followed by
// Accept / Not Accept buttons via IMGUI overlay.
public class QuestTriggerProp : MonoBehaviour
{
    [Tooltip("The quest to offer when the player interacts with this prop.")]
    public QuestData questData;

    [TextArea(3, 8)]
    [Tooltip("Story / flavour dialogue shown to the player before the Accept prompt. " +
             "Leave blank to show only the quest description.")]
    public string dialogueText;

    [TextArea(1, 3)]
    [Tooltip("Short question or call-to-action shown just above the buttons, e.g. " +
             "'Will you take on this task?' Leave blank to omit.")]
    public string offerLine;

    bool _offering;
    bool _accepted;

    public bool HasBeenAccepted => _accepted;

    void Update()
    {
        if (_offering && questData != null && !QuestSystem.Instance.CanOffer(questData))
            _offering = false;
    }

    // Called by InteractionHUD when the player presses the interact key.
    public void Interact()
    {
        if (questData == null || _accepted) return;
        if (QuestSystem.Instance == null || !QuestSystem.Instance.CanOffer(questData)) return;
        _offering = true;
    }

    void OnGUI()
    {
        if (!_offering) return;

        // Body text: prefer custom dialogueText, fall back to quest description.
        string body = !string.IsNullOrEmpty(dialogueText) ? dialogueText : questData.description;

        // Panel height grows with body text length.
        float bodyH  = string.IsNullOrEmpty(body) ? 0f : Mathf.Clamp(body.Length * 0.45f, 60f, 200f);
        float offerH = string.IsNullOrEmpty(offerLine) ? 0f : 30f;
        float PW     = 500f;
        float PH     = 60f + bodyH + offerH + 52f; // title + body + offer + buttons + padding

        float px = (Screen.width  - PW) / 2f;
        float py = (Screen.height - PH) / 2f;

        GUI.Box(new Rect(px, py, PW, PH), "");

        float cursor = py + 12f;

        // Title
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle  = FontStyle.Bold,
            fontSize   = 18,
        };
        GUI.Label(new Rect(px + 10f, cursor, PW - 20f, 28f), questData.title, titleStyle);
        cursor += 34f;

        // Dialogue / description body
        if (!string.IsNullOrEmpty(body))
        {
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap  = true,
                fontSize  = 14,
                alignment = TextAnchor.UpperLeft,
            };
            GUI.Label(new Rect(px + 16f, cursor, PW - 32f, bodyH), body, bodyStyle);
            cursor += bodyH + 6f;
        }

        // Optional offer line
        if (!string.IsNullOrEmpty(offerLine))
        {
            var offerStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap  = true,
                fontStyle  = FontStyle.Italic,
                fontSize   = 14,
                alignment  = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(px + 16f, cursor, PW - 32f, offerH), offerLine, offerStyle);
            cursor += offerH + 4f;
        }

        // Accept / Not Accept buttons
        GUI.skin.button.fontSize = 15;
        float btnW  = 130f;
        float btnH  = 38f;
        float btnY  = cursor + 4f;
        float btnX  = px + (PW - (btnW * 2f + 16f)) / 2f;

        if (GUI.Button(new Rect(btnX,            btnY, btnW, btnH), "Accept"))
        {
            QuestSystem.Instance?.AcceptQuest(questData);
            _accepted = true;
            _offering = false;
        }
        if (GUI.Button(new Rect(btnX + btnW + 16f, btnY, btnW, btnH), "Decline"))
        {
            _offering = false;
        }
    }
}
