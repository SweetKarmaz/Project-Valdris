using System.Text;
using UnityEngine;

// Hover tooltip for a quest in the Quests tab: title, description, objectives
// (with live progress), and rewards. Call near the end of an OnGUI pass in
// screen-GUI space (after any scroll view has ended).
public static class QuestTooltip
{
    public static void Draw(QuestData quest, Vector2 mouse)
    {
        if (quest == null) return;

        string body = BuildBody(quest);

        var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15, wordWrap = true };
        titleStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
        var bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
        bodyStyle.normal.textColor = new Color(0.86f, 0.86f, 0.86f);

        const float w = 300f, pad = 10f;
        float titleH = titleStyle.CalcHeight(new GUIContent(quest.title), w - pad * 2f);
        float bodyH  = bodyStyle.CalcHeight(new GUIContent(body), w - pad * 2f);
        float h = pad * 2f + titleH + 4f + bodyH;

        float x = Mathf.Min(mouse.x + 16f, Screen.width  - w - 4f);
        float y = Mathf.Min(mouse.y + 16f, Screen.height - h - 4f);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.93f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.4f, 0.5f);
        GUI.DrawTexture(new Rect(x, y, w, 2f), Texture2D.whiteTexture);
        GUI.color = prev;

        GUI.Label(new Rect(x + pad, y + pad, w - pad * 2f, titleH), quest.title, titleStyle);
        GUI.Label(new Rect(x + pad, y + pad + titleH + 4f, w - pad * 2f, bodyH), body, bodyStyle);
    }

    static string BuildBody(QuestData q)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(q.description))
            sb.Append("<i>").Append(q.description).Append("</i>\n\n");

        if (q.objectives != null && q.objectives.Count > 0)
        {
            sb.Append("<b>Objectives</b>\n");
            for (int i = 0; i < q.objectives.Count; i++)
            {
                var obj = q.objectives[i];
                int curr = QuestSystem.Instance != null ? QuestSystem.Instance.GetObjectiveCount(q, i) : 0;
                bool done = curr >= obj.requiredCount;
                string check = done ? "[x]" : "[ ]";
                sb.Append(check).Append(' ').Append(obj.description);
                if (obj.requiredCount > 1) sb.Append($"  ({curr}/{obj.requiredCount})");
                sb.Append('\n');
            }
        }

        string rewards = Rewards(q);
        if (!string.IsNullOrEmpty(rewards))
            sb.Append("\n<b>Rewards</b>\n").Append(rewards);

        return sb.ToString().TrimEnd();
    }

    static string Rewards(QuestData q)
    {
        var sb = new StringBuilder();
        if (q.xpReward > 0)        sb.Append($"+{q.xpReward} XP\n");
        if (q.goldReward > 0)      sb.Append($"+{q.goldReward} Gold\n");
        if (q.statPointReward > 0) sb.Append($"+{q.statPointReward} Attribute Point(s)\n");
        if (q.itemRewards != null)
            foreach (var item in q.itemRewards)
                if (item != null) sb.Append(item.ItemName).Append('\n');
        if (q.spellRewards != null)
            foreach (var s in q.spellRewards)
                if (s != null) sb.Append("Spell: ").Append(s.name).Append('\n');
        if (q.skillRewards != null)
            foreach (var sk in q.skillRewards)
                if (sk != null) sb.Append("Skill: ").Append(sk.skillName).Append('\n');
        return sb.ToString().TrimEnd();
    }
}
