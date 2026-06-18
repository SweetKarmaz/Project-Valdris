using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools > Valdris > Build NPC Prefab Variants
//
// One-shot setup tool:
//   1. Copies Chr_FantasyHero_Preset_1 to Randomized/ as Chr_Npc_Base
//   2. Adds NpcAppearanceComponent to the base prefab
//   3. Assigns the CharacterAnimator controller to its Animator
//   4. Creates one Prefab Variant per NpcAppearanceClass asset,
//      each pre-wired to its class and named Chr_Npc_{ClassName}
//
// Safe to re-run: existing assets are skipped.
public static class NpcVariantBuilder
{
    const string SourcePrefab    = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets/Chr_FantasyHero_Preset_1.prefab";
    const string BaseDest        = "Assets/_Project/Prefabs/Characters/Randomized/Chr_Npc_Base.prefab";
    const string VariantFolder   = "Assets/_Project/Prefabs/Characters/Randomized";
    const string ClassFolder     = "Assets/_Project/ScriptableObjects/NpcClasses";
    const string AnimController  = "Assets/_Project/Animation/CharacterAnimator.controller";

    [MenuItem("Tools/Valdris/NPC/Build Prefab Variants")]
    public static void Run()
    {
        // ── Load dependencies ─────────────────────────────────────────────────

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimController);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("NPC Variant Builder",
                $"AnimatorController not found at:\n{AnimController}", "OK");
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab))
        {
            EditorUtility.DisplayDialog("NPC Variant Builder",
                $"Source prefab not found at:\n{SourcePrefab}", "OK");
            return;
        }

        EnsureFolder(VariantFolder);

        // ── Step 1: copy base prefab and configure it ─────────────────────────

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseDest);
        if (basePrefab == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePrefab, BaseDest))
            {
                EditorUtility.DisplayDialog("NPC Variant Builder",
                    $"Failed to copy prefab to:\n{BaseDest}", "OK");
                return;
            }
            AssetDatabase.Refresh();
            basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseDest);
        }

        // Open the base prefab for editing.
        var baseContents = PrefabUtility.LoadPrefabContents(BaseDest);

        // Add NpcAppearanceComponent if missing.
        var appearance = baseContents.GetComponent<NpcAppearanceComponent>();
        if (appearance == null)
            appearance = baseContents.AddComponent<NpcAppearanceComponent>();

        appearance.randomizeOnAwake = true;
        appearance.appearanceClass  = null; // class assigned per variant

        // Wire the animator controller.
        var animator = baseContents.GetComponent<Animator>();
        if (animator == null)
            animator = baseContents.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        PrefabUtility.SaveAsPrefabAsset(baseContents, BaseDest);
        PrefabUtility.UnloadPrefabContents(baseContents);
        AssetDatabase.Refresh();

        basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseDest);
        Debug.Log($"[NpcVariantBuilder] Base prefab ready: {BaseDest}");

        // ── Step 2: create one Prefab Variant per class asset ─────────────────

        string[] classGuids = AssetDatabase.FindAssets("t:NpcAppearanceClass", new[] { ClassFolder });
        int created = 0, skipped = 0;

        foreach (string guid in classGuids)
        {
            string classPath = AssetDatabase.GUIDToAssetPath(guid);
            var    cls       = AssetDatabase.LoadAssetAtPath<NpcAppearanceClass>(classPath);
            if (cls == null) continue;

            // Build a file-safe variant name from the class name.
            string safeName   = cls.className.Replace(" ", "_");
            string variantPath = $"{VariantFolder}/Chr_Npc_{safeName}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
            {
                skipped++;
                continue;
            }

            // Instantiate the base prefab — SaveAsPrefabAsset on an instance
            // of an existing prefab automatically creates a Prefab Variant.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = $"Chr_Npc_{safeName}";

            // Assign the class on the instance before saving.
            var comp = instance.GetComponent<NpcAppearanceComponent>();
            if (comp != null)
                comp.appearanceClass = cls;

            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);

            created++;
            Debug.Log($"[NpcVariantBuilder] Created variant: {variantPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "NPC Variant Builder",
            $"Done.\n\nBase prefab: {BaseDest}\n\n" +
            $"Variants created: {created}\nVariants skipped (already exist): {skipped}",
            "OK");
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string   built = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{built}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(built, parts[i]);
            built = next;
        }
    }
}
