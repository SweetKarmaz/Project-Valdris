using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Reusable IMGUI hover tooltip for a LootItem: name (rarity-coloured), flavour
// text, and stat/effect lines (armor, stat modifiers, weapon stats, consumable
// restores). Call Draw() near the end of an OnGUI pass, in screen-GUI space.
public static class ItemTooltip
{
    public static void Draw(LootItem item, Vector2 mouse)
    {
        if (item == null) return;

        string body = BuildBody(item);

        var nameStyle = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, fontSize = 14, wordWrap = true };
        nameStyle.normal.textColor = RarityColor(item.rarity);

        var bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
        bodyStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        const float w = 250f, pad = 8f;
        float nameH = nameStyle.CalcHeight(new GUIContent(item.ItemName), w - pad * 2f);
        float bodyH = string.IsNullOrEmpty(body) ? 0f
            : bodyStyle.CalcHeight(new GUIContent(body), w - pad * 2f);
        float h = pad * 2f + nameH + (bodyH > 0f ? bodyH + 4f : 0f);

        // Position near the cursor, clamped to the screen.
        float x = Mathf.Min(mouse.x + 16f, Screen.width  - w - 4f);
        float y = Mathf.Min(mouse.y + 16f, Screen.height - h - 4f);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.92f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        DrawBorder(new Rect(x, y, w, h));
        GUI.color = prev;

        GUI.Label(new Rect(x + pad, y + pad, w - pad * 2f, nameH), item.ItemName, nameStyle);
        if (bodyH > 0f)
            GUI.Label(new Rect(x + pad, y + pad + nameH + 4f, w - pad * 2f, bodyH), body, bodyStyle);
    }

    static string BuildBody(LootItem item)
    {
        var lines = new List<string>();

        switch (item.itemType)
        {
            case LootItemType.Armor:
                if (item.armorValue != 0f) lines.Add($"Armor +{item.armorValue:0.#}");
                break;
            case LootItemType.Weapon:
                if (item.weaponDamage != 0f) lines.Add($"Damage +{item.weaponDamage:0.#}");
                lines.Add($"Range {item.weaponRange:0.#}");
                break;
            case LootItemType.Projectile:
                if (item.projectileDamage != 0f) lines.Add($"Projectile Damage {item.projectileDamage:0.#}");
                break;
            case LootItemType.Consumable:
                if (item.restoresHealth)
                    lines.Add(item.healthIsPercent
                        ? $"Restores {item.healthAmount * 100f:0}% Health"
                        : $"Restores {item.healthAmount:0} Health");
                if (item.restoresMana)
                    lines.Add(item.manaIsPercent
                        ? $"Restores {item.manaAmount * 100f:0}% Mana"
                        : $"Restores {item.manaAmount:0} Mana");
                break;
        }

        // Elemental riders (bonus typed damage on weapons).
        if (item.onHitEffects != null)
            foreach (var e in item.onHitEffects)
                if (e.damage > 0f) lines.Add($"+{e.damage:0.#} {e.type} damage");

        // Equip stat modifiers (any item type can carry them).
        if (item.statModifiers != null)
            foreach (var mod in item.statModifiers)
            {
                string sign = mod.amount >= 0f ? "+" : "";
                lines.Add(mod.mode == ModifierMode.Percent
                    ? $"{sign}{mod.amount:0.#}% {Nicify(mod.stat)}"
                    : $"{sign}{mod.amount:0.#} {Nicify(mod.stat)}");
            }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(item.flavorText))
        {
            sb.Append("<i>").Append(item.flavorText).Append("</i>");
            if (lines.Count > 0) sb.Append('\n').Append('\n');
        }
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append(lines[i]);
            if (i < lines.Count - 1) sb.Append('\n');
        }
        if (item.goldValue > 0)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"Value: {item.goldValue} g");
        }
        return sb.ToString();
    }

    static string Nicify(StatType stat)
    {
        // Insert spaces before capitals: "MaxHealth" → "Max Health".
        string s = stat.ToString();
        var sb = new StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    static Color RarityColor(ItemRarity r) => r switch
    {
        ItemRarity.Uncommon  => new Color(0.4f, 1f, 0.4f),
        ItemRarity.Rare      => new Color(0.4f, 0.6f, 1f),
        ItemRarity.Epic      => new Color(0.75f, 0.4f, 1f),
        ItemRarity.Legendary => new Color(1f, 0.65f, 0.2f),
        _                    => Color.white,
    };

    static void DrawBorder(Rect r)
    {
        const float t = 1f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + r.width - t, r.y, t, r.height), Texture2D.whiteTexture);
    }
}
