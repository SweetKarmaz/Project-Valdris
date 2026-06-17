using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Tools → Valdris → Fix Loot Container Materials (HDRP)
//
// Synty container prefabs ship with Built-in/URP materials, which render magenta
// under HDRP. This converts every material used by prefabs in the LootContainers
// and source container folders to HDRP/Lit, carrying over the base map/colour and
// normal map, then runs HDRP's material validation so they render correctly.
// Materials already on an HDRP shader are skipped. Because the copied prefabs
// share the same .mat assets as the originals, this fixes both.
public static class HdrpMaterialFixer
{
    static readonly string[] Folders =
    {
        "Assets/_Project/Prefabs/LootContainers",
        "Assets/_Project/Prefabs/Items/Furniture/Storage",
        "Assets/_Project/Prefabs/Items/Containers",
    };

    [MenuItem("Tools/Valdris/Log Container Materials")]
    static void LogMaterials()
    {
        foreach (string folder in Folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;

                var sb = new System.Text.StringBuilder();
                sb.Append($"[Mat] {System.IO.Path.GetFileName(path)}: ");
                foreach (var r in renderers)
                    foreach (var m in r.sharedMaterials)
                        sb.Append(m == null ? "<MISSING>" : $"{m.name}({(m.shader != null ? m.shader.name : "no-shader")})")
                          .Append("  ");
                Debug.Log(sb.ToString(), prefab);
            }
        }
    }

    [MenuItem("Tools/Valdris/Fix Missing Container Materials")]
    static void FixMissing()
    {
        var fallback = FindFallbackMaterial();
        if (fallback == null) { Debug.LogError("[HdrpMaterialFixer] No fallback material found."); return; }

        int fixedRenderers = 0, fixedPrefabs = 0;
        foreach (string folder in Folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;

                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool slotChanged = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == null) { mats[i] = fallback; slotChanged = true; }
                    if (slotChanged) { r.sharedMaterials = mats; changed = true; fixedRenderers++; }
                }

                if (changed) { PrefabUtility.SaveAsPrefabAsset(go, path); fixedPrefabs++; }
                PrefabUtility.UnloadPrefabContents(go);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[HdrpMaterialFixer] Assigned '{fallback.name}' to {fixedRenderers} renderer(s) " +
                  $"across {fixedPrefabs} prefab(s) with missing materials.");
    }

    // Prefers PolygonFantasyKingdom_Mat_01_A; otherwise the first non-null HDRP
    // material already used by a container prefab.
    static Material FindFallbackMaterial()
    {
        var guids = AssetDatabase.FindAssets("PolygonFantasyKingdom_Mat_01_A t:Material");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));

        foreach (string folder in Folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                if (prefab == null) continue;
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null) return m;
            }
        }
        return null;
    }

    [MenuItem("Tools/Valdris/Fix Loot Container Materials (HDRP)")]
    static void Fix()
    {
        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null) { Debug.LogError("[HdrpMaterialFixer] HDRP/Lit shader not found."); return; }

        var mats = new HashSet<Material>();
        foreach (string folder in Folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null) mats.Add(m);
            }
        }

        int converted = 0, skipped = 0;
        foreach (var mat in mats)
        {
            if (mat.shader != null && mat.shader.name.StartsWith("HDRP")) { skipped++; continue; }

            // Capture common Built-in/URP properties before swapping the shader.
            Texture baseMap   = GetTex(mat, "_BaseMap", "_MainTex");
            Color   baseColor = GetCol(mat, "_BaseColor", "_Color");
            Texture normalMap = GetTex(mat, "_BumpMap", "_NormalMap");

            mat.shader = hdrpLit;

            if (baseMap != null) mat.SetTexture("_BaseColorMap", baseMap);
            mat.SetColor("_BaseColor", baseColor);
            if (normalMap != null)
            {
                mat.SetTexture("_NormalMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            HDMaterial.ValidateMaterial(mat);   // sets up keywords / render queue
            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[HdrpMaterialFixer] Converted {converted} material(s) to HDRP/Lit, skipped {skipped} already-HDRP.");
    }

    static Texture GetTex(Material m, params string[] names)
    {
        foreach (var n in names) if (m.HasProperty(n)) { var t = m.GetTexture(n); if (t != null) return t; }
        return null;
    }

    static Color GetCol(Material m, params string[] names)
    {
        foreach (var n in names) if (m.HasProperty(n)) return m.GetColor(n);
        return Color.white;
    }
}
