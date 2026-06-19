using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Tools > Synty > Fix Foliage Materials (HDRP)
//
// After the HDRP upgrade, Synty foliage materials render WHITE because their
// albedo lived under a custom shader property (_Main_Texture) that the generic
// upgrader didn't map into HDRP's _BaseColorMap. This pulls the orphaned albedo
// and normal (still serialized in the .mat) into the correct HDRP slots, and
// restores alpha-cutout + double-sided rendering for leaf/plant cards.
//
// Safe + idempotent: only touches HDRP/Lit Synty materials that are missing a
// base map; re-running does nothing further.
public static class SyntyFoliageMaterialFixer
{
    static readonly string[] AlbedoProps =
        { "_Main_Texture", "_MainTex", "_Texture_Map", "_Albedo_Map", "_Albedo", "_BaseMap", "_Diffuse" };
    static readonly string[] NormalProps =
        { "_Normal_Map", "_BumpMap", "_Normal", "_NormalTexture" };
    static readonly string[] FoliageHints =
        { "Leaves", "Leaf", "Plant", "Grass", "Flower", "Fern", "Bush", "Foliage" };

    [MenuItem("Tools/Synty/Fix Foliage Materials (HDRP)")]
    public static void Fix()
    {
        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null) { EditorUtility.DisplayDialog("Fix Foliage", "HDRP/Lit shader not found.", "OK"); return; }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Synty" });
        int fixedAlbedo = 0, fixedAlpha = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Fixing Synty Foliage", System.IO.Path.GetFileName(path),
                    (float)i / guids.Length);

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader != hdrpLit) continue;

                bool changed = false;
                var so = new SerializedObject(mat);

                // 1) Recover a missing albedo (white materials) from the orphaned property.
                if (mat.GetTexture("_BaseColorMap") == null)
                {
                    Texture albedo = GetOrphan(so, AlbedoProps);
                    if (albedo != null)
                    {
                        mat.SetTexture("_BaseColorMap", albedo);
                        mat.SetColor("_BaseColor", Color.white);
                        Texture normal = GetOrphan(so, NormalProps);
                        if (normal != null && mat.GetTexture("_NormalMap") == null)
                            mat.SetTexture("_NormalMap", normal);
                        changed = true;
                        fixedAlbedo++;
                    }
                }

                // 2) Foliage cards need alpha cutout + double-sided so the transparent
                //    parts of the texture don't render as solid (white) quads. Runs even
                //    when the base map was already present.
                //    "Patch" materials are excluded — their alpha isn't a clean cutout
                //    mask, so cutting them makes them vanish; keep them opaque (and heal
                //    any we previously cut).
                bool foliage = FoliageHints.Any(hint => path.Contains(hint) || mat.name.Contains(hint));
                bool patch   = path.Contains("Patch") || mat.name.Contains("Patch");
                if (foliage && !patch && mat.HasProperty("_AlphaCutoffEnable") && mat.GetFloat("_AlphaCutoffEnable") < 0.5f)
                {
                    mat.SetFloat("_AlphaCutoffEnable", 1f);
                    mat.SetFloat("_AlphaCutoff", 0.5f);
                    mat.SetFloat("_DoubleSidedEnable", 1f);
                    mat.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
                    changed = true;
                    fixedAlpha++;
                }
                else if (patch && mat.HasProperty("_AlphaCutoffEnable") && mat.GetFloat("_AlphaCutoffEnable") > 0.5f)
                {
                    mat.SetFloat("_AlphaCutoffEnable", 0f);   // heal: revert to opaque
                    changed = true;
                }

                if (!changed) continue;
                HDMaterial.ValidateMaterial(mat);   // reconfigure HDRP keywords/passes/render state
                EditorUtility.SetDirty(mat);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Fix Foliage Materials",
            $"Restored albedo on {fixedAlbedo} material(s).\n" +
            $"Alpha-cutout/double-sided on {fixedAlpha} foliage material(s).", "OK");
    }

    // Reads a texture from a serialized property name even if the current shader
    // no longer exposes it (orphaned in m_TexEnvs after the shader swap).
    static Texture GetOrphan(SerializedObject so, string[] names)
    {
        var arr = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (arr == null) return null;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            string n = el.FindPropertyRelative("first").stringValue;
            if (!names.Contains(n)) continue;
            var tex = el.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (tex != null) return tex;
        }
        return null;
    }
}
