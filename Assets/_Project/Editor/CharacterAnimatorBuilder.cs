using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds the shared stance-based Animator controller from the Humanoid clips in
// Assets/ThirdParty/{NoWeapon,OneHanded,TwoHanded,Archer}Animation, and fixes
// the Loop Time flag on locomotion clips.
//
// Run via Tools > Valdris > Build Character Animator (loops + controller).
//
// Controller layout (single layer):
//   Parameters: Speed (float), Stance (int 0..3), Attack/Cast/Hit (trigger), Dead (bool)
//   Locomotion : 2D blend (X = Stance, Y = Speed) → idle/walk/run per stance
//   Attack/Hit/Cast/Death : 1D blend on Stance → the matching clip per stance
//   AnyState → Attack/Hit/Cast (trigger, while !Dead); → Death (Dead). Actions
//   exit back to Locomotion; Death latches.
public static class CharacterAnimatorBuilder
{
    const string A  = "Assets/ThirdParty/ArcherAnimation";
    const string NW = "Assets/ThirdParty/NoWeaponAnimations";
    const string OH = "Assets/ThirdParty/OneHandedAnimation";
    const string TH = "Assets/ThirdParty/TwoHandedAnimation";
    const string ControllerPath = "Assets/_Project/Animation/CharacterAnimator.controller";

    // Stance order must match CharacterAnimator.WeaponStance.
    // [stance] = { idle, walk, run, attack, hit, cast, death }
    static readonly string[][] Clips =
    {
        // NoWeapon (0)
        new[] { $"{NW}/idle.fbx", $"{NW}/walking.fbx", $"{NW}/running.fbx",
                $"{NW}/idle.fbx", $"{A}/standing react small from front.fbx",
                $"{NW}/idle.fbx", $"{A}/standing death backward 01.fbx" },
        // OneHanded (1)
        new[] { $"{OH}/sword and shield idle.fbx", $"{OH}/sword and shield walk.fbx", $"{OH}/sword and shield run.fbx",
                $"{OH}/sword and shield slash.fbx", $"{OH}/sword and shield impact.fbx",
                $"{OH}/sword and shield casting.fbx", $"{OH}/sword and shield death.fbx" },
        // TwoHanded (2)
        new[] { $"{TH}/great sword idle.fbx", $"{TH}/great sword walk.fbx", $"{TH}/great sword run.fbx",
                $"{TH}/great sword slash.fbx", $"{TH}/great sword impact.fbx",
                $"{TH}/great sword casting.fbx", $"{TH}/two handed sword death.fbx" },
        // Archer (3)
        new[] { $"{A}/standing idle 01.fbx", $"{A}/standing walk forward.fbx", $"{A}/standing run forward.fbx",
                $"{A}/standing draw arrow.fbx", $"{A}/standing react small from front.fbx",
                $"{A}/standing aim overdraw.fbx", $"{A}/standing death backward 01.fbx" },
    };

    const int Idle = 0, Walk = 1, Run = 2, Attack = 3, Hit = 4, Cast = 5, Death = 6;

    [MenuItem("Tools/Valdris/Character/Build Character Animator (loops + controller)")]
    public static void Build()
    {
        FixLoops();
        BuildController();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharacterAnimatorBuilder] Done. If characters show no animation, run " +
                  "Tools > Valdris > Assign Controller to All Synty Characters (or reassign the controller).");
    }

    // ── Loop flags ────────────────────────────────────────────────────────────

    static void FixLoops()
    {
        string[] folders = { A, NW, OH, TH };
        foreach (string folder in folders)
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx")) continue;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;

            bool loop = ShouldLoop(System.IO.Path.GetFileNameWithoutExtension(path));
            var clips = imp.defaultClipAnimations;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].loopTime != loop) { clips[i].loopTime = loop; changed = true; }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
        }
    }

    // Locomotion clips loop; one-shots (attacks, draws, deaths, etc.) do not.
    static bool ShouldLoop(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("stop") || n.Contains(" to ") || n.Contains("land")) return false;
        return n.Contains("idle") || n.Contains("walk") || n.Contains("run")
            || n.Contains("strafe") || n.Contains("sneak") || n.Contains("crouching");
    }

    // ── Controller ──────────────────────────────────────────────────────────────

    static void BuildController()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Wipe existing parameters, states, and orphaned blend-tree sub-assets so a
        // rebuild is clean while keeping the asset GUID (so character refs survive).
        while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(ctrl.parameters[0]);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToList()) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states.ToList()) sm.RemoveState(s.state);
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).ToList())
            if (o is BlendTree bt) Object.DestroyImmediate(bt, true);

        ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Stance", AnimatorControllerParameterType.Float); // blend trees require float
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Cast",   AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit",    AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dead",   AnimatorControllerParameterType.Bool);

        // Locomotion: 2D blend over (Stance, Speed).
        var loco = sm.AddState("Locomotion");
        var locoTree = NewTree(ctrl, "Locomotion", BlendTreeType.FreeformCartesian2D, "Speed");
        locoTree.blendParameter  = "Stance";
        locoTree.blendParameterY = "Speed";
        for (int s = 0; s < Clips.Length; s++)
        {
            locoTree.AddChild(Clip(s, Idle), new Vector2(s, 0f));
            locoTree.AddChild(Clip(s, Walk), new Vector2(s, 0.5f));
            locoTree.AddChild(Clip(s, Run),  new Vector2(s, 1f));
        }
        loco.motion = locoTree;
        sm.defaultState = loco;

        var attack = StanceState(ctrl, sm, "Attack", Attack);
        var hit    = StanceState(ctrl, sm, "Hit",    Hit);
        var cast   = StanceState(ctrl, sm, "Cast",   Cast);
        var death  = StanceState(ctrl, sm, "Death",  Death);

        AddActionTransitions(sm, attack, "Attack", loco);
        AddActionTransitions(sm, hit,    "Hit",    loco);
        AddActionTransitions(sm, cast,   "Cast",   loco);

        var toDeath = sm.AddAnyStateTransition(death);
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        toDeath.duration = 0.05f; toDeath.hasExitTime = false; toDeath.canTransitionToSelf = false;

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[CharacterAnimatorBuilder] Controller rebuilt with 4 stances.");
    }

    static AnimatorState StanceState(AnimatorController ctrl, AnimatorStateMachine sm, string name, int clipIndex)
    {
        var state = sm.AddState(name);
        var tree  = NewTree(ctrl, name, BlendTreeType.Simple1D, "Stance");
        for (int s = 0; s < Clips.Length; s++) tree.AddChild(Clip(s, clipIndex), s);
        state.motion = tree;
        return state;
    }

    static void AddActionTransitions(AnimatorStateMachine sm, AnimatorState state, string trigger, AnimatorState loco)
    {
        var enter = sm.AddAnyStateTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        enter.duration = 0.08f; enter.hasExitTime = false; enter.canTransitionToSelf = false;

        var exit = state.AddTransition(loco);
        exit.hasExitTime = true; exit.exitTime = 0.85f; exit.duration = 0.12f;
    }

    static BlendTree NewTree(AnimatorController ctrl, string name, BlendTreeType type, string param)
    {
        var tree = new BlendTree { name = name, blendType = type, blendParameter = param };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        return tree;
    }

    static AnimationClip Clip(int stance, int slot)
    {
        string path = Clips[stance][slot];
        foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__")) return c;
        Debug.LogWarning($"[CharacterAnimatorBuilder] Clip not found: {path}");
        return null;
    }
}
