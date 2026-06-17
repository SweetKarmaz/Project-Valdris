using UnityEngine;
using UnityEditor;
using System.IO;

// One-click tool: upgrades all Synty materials under Assets/Synty from the
// PolygonGeneric Built-in/URP shader to HDRP/Lit, remapping textures and
// color properties so they render correctly in HDRP.
//
// Run via: Tools > Synty > Upgrade Materials to HDRP
public static class SyntyHDRPUpgrader
{
    [MenuItem("Tools/Synty/Upgrade Materials to HDRP")]
    public static void UpgradeMaterials()
    {
        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null)
        {
            EditorUtility.DisplayDialog("Synty HDRP Upgrader",
                "Could not find HDRP/Lit shader. Make sure HDRP is installed.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Synty" });
        int upgraded = 0;
        int skipped = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Upgrading Synty Materials",
                    Path.GetFileName(path), (float)i / guids.Length);

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // Skip if already on HDRP/Lit
                if (mat.shader == hdrpLit) { skipped++; continue; }

                // Capture properties before shader swap
                Texture albedo   = GetTex(mat, "_Albedo_Map", "_Texture_Map", "_BaseColorMap", "_MainTex");
                Texture normal   = GetTex(mat, "_Normal_Map", "_NormalMap", "_BumpMap");
                Texture emission = GetTex(mat, "_Emission_Map", "_EmissionMap");
                Color   baseCol  = GetCol(mat, "_BaseColor", "_Color");
                float   smooth   = GetFloat(mat, "_Smoothness", "_Glossiness", 0.2f);
                float   metallic = GetFloat(mat, "_Metallic", 0f);
                bool    alphaCut = mat.IsKeywordEnabled("_ALPHATEST_ON")
                                || GetFloat(mat, "_AlphaClip", 0f) > 0.5f;
                float   cutoff   = GetFloat(mat, "_Alpha_Clip_Threshold", "_Cutoff", 0.5f);

                mat.shader = hdrpLit;

                // Remap to HDRP/Lit property names
                if (albedo  != null) mat.SetTexture("_BaseColorMap", albedo);
                if (normal  != null) mat.SetTexture("_NormalMap", normal);
                if (emission!= null) mat.SetTexture("_EmissionColorMap", emission);
                mat.SetColor("_BaseColor", baseCol);
                mat.SetFloat("_Smoothness", smooth);
                mat.SetFloat("_Metallic", metallic);

                if (alphaCut)
                {
                    mat.SetFloat("_AlphaCutoffEnable", 1f);
                    mat.SetFloat("_AlphaCutoff", cutoff);
                    mat.EnableKeyword("_ALPHATEST_ON");
                }

                EditorUtility.SetDirty(mat);
                upgraded++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Synty HDRP Upgrader",
            $"Done.\nUpgraded: {upgraded}\nAlready HDRP: {skipped}", "OK");
    }

    private static Texture GetTex(Material m, params string[] names)
    {
        foreach (string n in names)
            if (m.HasProperty(n)) { var t = m.GetTexture(n); if (t) return t; }
        return null;
    }

    private static Color GetCol(Material m, params string[] names)
    {
        foreach (string n in names)
            if (m.HasProperty(n)) return m.GetColor(n);
        return Color.white;
    }

    private static float GetFloat(Material m, string name, float fallback = 0f)
    {
        return m.HasProperty(name) ? m.GetFloat(name) : fallback;
    }

    private static float GetFloat(Material m, string name, string alt, float fallback = 0f)
    {
        if (m.HasProperty(name)) return m.GetFloat(name);
        if (m.HasProperty(alt))  return m.GetFloat(alt);
        return fallback;
    }
}
