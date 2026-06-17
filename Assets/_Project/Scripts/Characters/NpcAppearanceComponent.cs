using System.Collections.Generic;
using UnityEngine;

// Attach to any PolygonFantasyHeroCharacters preset prefab.
// Assign an NpcAppearanceClass ScriptableObject and call Randomize() (or let
// Awake do it) to generate a class-appropriate random appearance.
//
// Works with the Synty preset prefab hierarchy which organises all part meshes
// as disabled children under named group objects ("Male_03_Torso" etc.).
// BuildLists() mirrors the CharacterRandomizer's own method so no dependency
// on that component is needed — drop either one on the prefab, not both.
[DisallowMultipleComponent]
public class NpcAppearanceComponent : MonoBehaviour
{
    [Header("Class")]
    [Tooltip("The NpcAppearanceClass that defines this character's visual constraints.")]
    public NpcAppearanceClass appearanceClass;

    [Header("Options")]
    [Tooltip("Randomize appearance on Awake. Disable if you want to call Randomize() manually.")]
    public bool randomizeOnAwake = true;

    [Tooltip("Seed for repeatable randomization. 0 = truly random each time.")]
    public int seed = 0;

    // ── Internal part lists (mirrors CharacterRandomizer.BuildLists) ──────────

    // Gender-specific groups
    List<GameObject> _headAll_M   = new(), _headAll_F   = new();
    List<GameObject> _headNone_M  = new(), _headNone_F  = new();
    List<GameObject> _eyebrow_M   = new(), _eyebrow_F   = new();
    List<GameObject> _facialHair_M = new();
    List<GameObject> _torso_M     = new(), _torso_F     = new();
    List<GameObject> _armUpper_R_M = new(), _armUpper_L_M = new();
    List<GameObject> _armUpper_R_F = new(), _armUpper_L_F = new();
    List<GameObject> _armLower_R_M = new(), _armLower_L_M = new();
    List<GameObject> _armLower_R_F = new(), _armLower_L_F = new();
    List<GameObject> _hand_R_M    = new(), _hand_L_M    = new();
    List<GameObject> _hand_R_F    = new(), _hand_L_F    = new();
    List<GameObject> _hips_M      = new(), _hips_F      = new();
    List<GameObject> _leg_R_M     = new(), _leg_L_M     = new();
    List<GameObject> _leg_R_F     = new(), _leg_L_F     = new();

    // All-gender groups
    List<GameObject> _hair               = new();
    List<GameObject> _headAttachment     = new();
    List<GameObject> _headCover_BaseHair = new();
    List<GameObject> _headCover_NoFacial = new();
    List<GameObject> _headCover_NoHair   = new();
    List<GameObject> _chestAttach        = new();
    List<GameObject> _backAttach         = new();
    List<GameObject> _shoulder_R         = new();
    List<GameObject> _shoulder_L         = new();
    List<GameObject> _elbow_R            = new();
    List<GameObject> _elbow_L            = new();
    List<GameObject> _hipsAttach         = new();
    List<GameObject> _knee_R             = new();
    List<GameObject> _knee_L             = new();
    List<GameObject> _elfEar             = new();

    // All currently enabled mesh GameObjects (cleared on each Randomize call).
    List<GameObject> _active = new();

    // True once appearance has been set and saved — blocks re-randomization.
    bool _locked;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (appearanceClass == null)
            ResolveClassFromDefinition();

        BuildLists();
        // Randomize is deferred to Start so NpcController.Start (which runs
        // first via DefaultExecutionOrder -10) can call RestoreAppearance and
        // set _locked before we attempt to randomize.
    }

    void Start()
    {
        if (randomizeOnAwake && !_locked)
            Randomize();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Randomize()
    {
        if (appearanceClass == null)
        {
            Debug.LogWarning($"[NpcAppearanceComponent] No appearance class assigned on {name}.", this);
            return;
        }

        if (seed != 0)
            Random.InitState(seed);

        // Disable all currently active parts.
        foreach (var go in _active) if (go) go.SetActive(false);
        _active.Clear();

        // Resolve gender and race.
        bool isMale = appearanceClass.gender switch
        {
            GenderPreference.Male   => true,
            GenderPreference.Female => false,
            _                       => Random.value > 0.5f,
        };
        bool isElf = appearanceClass.race switch
        {
            RacePreference.Elf   => true,
            RacePreference.Human => false,
            _                    => Random.value < 0.15f,
        };

        // Resolve head covering style.
        HeadCoveringStyle hcs = appearanceClass.headCoveringStyle == HeadCoveringStyle.Random
            ? (HeadCoveringStyle)Random.Range(0, 3)
            : appearanceClass.headCoveringStyle;

        // Resolve facial hair.
        bool wantFacialHair = isMale && appearanceClass.facialHair switch
        {
            FacialHairChance.Always  => true,
            FacialHairChance.Never   => false,
            _                        => Random.value > 0.5f,
        };

        var cls = appearanceClass;

        if (isMale)
        {
            // Head
            var headList = _headAll_M.Count > 0 ? _headAll_M : _headNone_M;
            Activate(headList, new SlotRange(0, -1));

            // Eyebrows
            Activate(_eyebrow_M, new SlotRange(0, -1));

            // Facial hair
            if (wantFacialHair && hcs != HeadCoveringStyle.NoFacialHair)
                Activate(_facialHair_M, new SlotRange(0, -1));

            // Body
            Activate(_torso_M,     cls.torso);
            ActivateMirror(_armUpper_R_M, _armUpper_L_M, cls.armUpper,  cls.shoulderMismatchChance);
            ActivateMirror(_armLower_R_M, _armLower_L_M, cls.armLower,  cls.elbowMismatchChance);
            ActivateMirror(_hand_R_M,     _hand_L_M,     cls.hands,     0);
            Activate(_hips_M, cls.hips);
            ActivateMirror(_leg_R_M, _leg_L_M, cls.legs, 0);
        }
        else
        {
            Activate(_headAll_F.Count > 0 ? _headAll_F : _headNone_F, new SlotRange(0, -1));
            Activate(_eyebrow_F, new SlotRange(0, -1));

            Activate(_torso_F,     cls.torso);
            ActivateMirror(_armUpper_R_F, _armUpper_L_F, cls.armUpper, cls.shoulderMismatchChance);
            ActivateMirror(_armLower_R_F, _armLower_L_F, cls.armLower, cls.elbowMismatchChance);
            ActivateMirror(_hand_R_F,     _hand_L_F,     cls.hands,    0);
            Activate(_hips_F, cls.hips);
            ActivateMirror(_leg_R_F, _leg_L_F, cls.legs, 0);
        }

        // Hair + head covering (gender-neutral)
        switch (hcs)
        {
            case HeadCoveringStyle.BaseHair:
                Activate(_hair, new SlotRange(0, -1));
                Activate(_headCover_BaseHair, new SlotRange(0, -1));
                break;
            case HeadCoveringStyle.NoFacialHair:
                Activate(_hair, new SlotRange(0, -1));
                Activate(_headCover_NoFacial, new SlotRange(0, -1));
                break;
            case HeadCoveringStyle.NoHair:
                Activate(_headCover_NoHair, new SlotRange(0, -1));
                if (isElf)
                    Activate(_elfEar, new SlotRange(0, -1));
                break;
        }

        // Optional attachments
        if (cls.allowChestAttachment && _chestAttach.Count > 0)
            Activate(_chestAttach, cls.chestAttach);

        if (cls.allowBackAttachment && _backAttach.Count > 0)
            Activate(_backAttach, cls.backAttach);

        if (cls.allowHipsAttachment && _hipsAttach.Count > 0)
            Activate(_hipsAttach, cls.hipsAttach);

        if (_shoulder_R.Count > 0)
            ActivateMirror(_shoulder_R, _shoulder_L, cls.shoulderAttach, cls.shoulderMismatchChance);

        if (_elbow_R.Count > 0)
            ActivateMirror(_elbow_R, _elbow_L, cls.elbowAttach, cls.elbowMismatchChance);

        if (_knee_R.Count > 0)
            ActivateMirror(_knee_R, _knee_L, cls.kneeAttach, cls.kneeMismatchChance);

        // Material colour variant
        ApplyMaterial();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Activate(List<GameObject> list, SlotRange range)
    {
        if (list.Count == 0) return;
        int idx = range.Random(list.Count);
        Enable(list[idx]);
    }

    void ActivateMirror(List<GameObject> right, List<GameObject> left, SlotRange range, int mismatchChance)
    {
        if (right.Count == 0) return;
        int idxR = range.Random(right.Count);
        Enable(right[idxR]);

        if (left.Count == 0) return;
        int idxL = mismatchChance > 0 && Random.Range(0, 100) < mismatchChance
            ? range.Random(left.Count)
            : Mathf.Clamp(idxR, 0, left.Count - 1);
        Enable(left[idxL]);
    }

    void Enable(GameObject go)
    {
        if (go == null) return;
        go.SetActive(true);
        _active.Add(go);
    }

    void ApplyMaterial()
    {
        var cls = appearanceClass;

        // Use the explicit override list if provided.
        if (cls.allowedMaterials != null && cls.allowedMaterials.Count > 0)
        {
            SetMaterial(cls.allowedMaterials[Random.Range(0, cls.allowedMaterials.Count)]);
            return;
        }

        // Otherwise look for HDRP materials in the pack folder.
        // Material naming: PolygonFantasyHero_Texture_0{set}_{variant}.mat
        int set = cls.textureSet < 1 ? Random.Range(1, 5) : cls.textureSet;
        string variant = cls.colourVariant switch
        {
            ColourVariant.A => "A",
            ColourVariant.B => "B",
            ColourVariant.C => "C",
            _               => new[] { "A", "B", "C" }[Random.Range(0, 3)],
        };

        string matName = $"PolygonFantasyHero_Texture_0{set}_{variant}";
        string matPath = $"Assets/Synty/PolygonFantasyHeroCharacters/Materials/HDRP/{matName}.mat";

#if UNITY_EDITOR
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null) SetMaterial(mat);
#endif
        // At runtime, materials are baked into the prefab by the NpcClassPrefabSetup
        // editor tool — no runtime asset load is needed.
    }

    void SetMaterial(Material mat)
    {
        if (mat == null) return;
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false))
            r.sharedMaterial = mat;
    }

    // ── Default appearance ────────────────────────────────────────────────────

    // Called by PlayerManager.AttachModel() to give the player a visible baseline
    // before PlayerAppearanceComponent takes over equip-driven changes.
    // BuildLists() in Awake() disables all mesh children; this re-enables the
    // first variant (index 0) of every main body group.
    public void EnableDefaultMeshes()
    {
        bool female = transform.Find("Female_03_Torso") != null;

        if (female)
        {
            EnableFirst(_headAll_F.Count > 0 ? _headAll_F : _headNone_F);
            EnableFirst(_eyebrow_F);
            EnableFirst(_torso_F);
            EnableFirst(_armUpper_R_F); EnableFirst(_armUpper_L_F);
            EnableFirst(_armLower_R_F); EnableFirst(_armLower_L_F);
            EnableFirst(_hand_R_F);     EnableFirst(_hand_L_F);
            EnableFirst(_hips_F);
            EnableFirst(_leg_R_F);      EnableFirst(_leg_L_F);
        }
        else
        {
            EnableFirst(_headAll_M.Count > 0 ? _headAll_M : _headNone_M);
            EnableFirst(_eyebrow_M);
            EnableFirst(_torso_M);
            EnableFirst(_armUpper_R_M); EnableFirst(_armUpper_L_M);
            EnableFirst(_armLower_R_M); EnableFirst(_armLower_L_M);
            EnableFirst(_hand_R_M);     EnableFirst(_hand_L_M);
            EnableFirst(_hips_M);
            EnableFirst(_leg_R_M);      EnableFirst(_leg_L_M);
        }
        EnableFirst(_hair);
    }

    static void EnableFirst(List<GameObject> list)
    {
        if (list.Count > 0) list[0].SetActive(true);
    }

    // ── Save / Restore ────────────────────────────────────────────────────────

    // Called by NpcController.CaptureState() — records which child index is
    // active in each mesh group so the exact look can be reproduced on reload.
    public List<AppearanceSlotSave> CaptureAppearance()
    {
        var result = new List<AppearanceSlotSave>();
        foreach (var go in _active)
        {
            if (go == null) continue;
            var parent = go.transform.parent;
            if (parent == null) continue;
            int idx = go.transform.GetSiblingIndex();
            result.Add(new AppearanceSlotSave { groupName = parent.name, index = idx });
        }
        return result;
    }

    // Called by NpcController.RestoreFromState() before Awake's Randomize() runs.
    // Activates the exact saved set of meshes and sets _locked so no re-randomization occurs.
    public void RestoreAppearance(List<AppearanceSlotSave> slots)
    {
        if (slots == null || slots.Count == 0) return;

        BuildLists(); // lists must exist before we can enable children

        foreach (var go in _active) if (go) go.SetActive(false);
        _active.Clear();

        foreach (var slot in slots)
        {
            var group = FindGroup(slot.groupName);
            if (group == null) continue;
            if (slot.index < 0 || slot.index >= group.childCount) continue;
            var child = group.GetChild(slot.index).gameObject;
            child.SetActive(true);
            _active.Add(child);
        }

        _locked = true; // prevent Awake from re-randomizing
    }

    // Maps NpcClass enum → the matching NpcAppearanceClass asset name so we
    // can auto-resolve the class when NpcDefinition is present but appearanceClass
    // was not manually assigned on this component.
    void ResolveClassFromDefinition()
    {
        var ctrl = GetComponent<NpcController>();
        if (ctrl == null || ctrl.definition == null) return;

        string assetName = ctrl.definition.npcClass switch
        {
            NpcClass.Peasant    => "NpcClass_Peasant",
            NpcClass.Farmer     => "NpcClass_Farmer",
            NpcClass.Beggar     => "NpcClass_Beggar",
            NpcClass.Merchant   => "NpcClass_Merchant",
            NpcClass.Innkeeper  => "NpcClass_Innkeeper",
            NpcClass.Scholar    => "NpcClass_Scholar",
            NpcClass.Bard       => "NpcClass_Bard",
            NpcClass.Blacksmith => "NpcClass_Blacksmith",
            NpcClass.Mage       => "NpcClass_Mage",
            NpcClass.Priest     => "NpcClass_Priest",
            NpcClass.Cultist    => "NpcClass_Cultist",
            NpcClass.Shaman     => "NpcClass_Shaman",
            NpcClass.Archer     => "NpcClass_Archer",
            NpcClass.Thief      => "NpcClass_Thief",
            NpcClass.Assassin   => "NpcClass_Assassin",
            NpcClass.TownGuard  => "NpcClass_TownGuard",
            NpcClass.Soldier    => "NpcClass_Soldier",
            NpcClass.Bandit     => "NpcClass_Bandit",
            NpcClass.Ranger     => "NpcClass_Ranger",
            NpcClass.CityGuard  => "NpcClass_CityGuard",
            NpcClass.Knight     => "NpcClass_Knight",
            NpcClass.Paladin    => "NpcClass_Paladin",
            NpcClass.King       => "NpcClass_King",
            NpcClass.Queen      => "NpcClass_Queen",
            NpcClass.Noble      => "NpcClass_Noble",
            NpcClass.Barbarian  => "NpcClass_Barbarian",
            _                   => null, // QuestGiver — appearance set manually
        };

        if (assetName == null) return;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            $"{assetName} t:NpcAppearanceClass",
            new[] { "Assets/_Project/ScriptableObjects/NpcClasses" });
        if (guids.Length > 0)
            appearanceClass = UnityEditor.AssetDatabase.LoadAssetAtPath<NpcAppearanceClass>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
    }

    // Builds all part lists by scanning child GameObjects by their group name.
    // Must be called before Randomize(). Awake() calls this automatically.
    void BuildLists()
    {
        Fill(_headAll_M,     "Male_Head_All_Elements");
        Fill(_headNone_M,    "Male_Head_No_Elements");
        Fill(_eyebrow_M,     "Male_01_Eyebrows");
        Fill(_facialHair_M,  "Male_02_FacialHair");
        Fill(_torso_M,       "Male_03_Torso");
        Fill(_armUpper_R_M,  "Male_04_Arm_Upper_Right");
        Fill(_armUpper_L_M,  "Male_05_Arm_Upper_Left");
        Fill(_armLower_R_M,  "Male_06_Arm_Lower_Right");
        Fill(_armLower_L_M,  "Male_07_Arm_Lower_Left");
        Fill(_hand_R_M,      "Male_08_Hand_Right");
        Fill(_hand_L_M,      "Male_09_Hand_Left");
        Fill(_hips_M,        "Male_10_Hips");
        Fill(_leg_R_M,       "Male_11_Leg_Right");
        Fill(_leg_L_M,       "Male_12_Leg_Left");

        Fill(_headAll_F,     "Female_Head_All_Elements");
        Fill(_headNone_F,    "Female_Head_No_Elements");
        Fill(_eyebrow_F,     "Female_01_Eyebrows");
        Fill(_torso_F,       "Female_03_Torso");
        Fill(_armUpper_R_F,  "Female_04_Arm_Upper_Right");
        Fill(_armUpper_L_F,  "Female_05_Arm_Upper_Left");
        Fill(_armLower_R_F,  "Female_06_Arm_Lower_Right");
        Fill(_armLower_L_F,  "Female_07_Arm_Lower_Left");
        Fill(_hand_R_F,      "Female_08_Hand_Right");
        Fill(_hand_L_F,      "Female_09_Hand_Left");
        Fill(_hips_F,        "Female_10_Hips");
        Fill(_leg_R_F,       "Female_11_Leg_Right");
        Fill(_leg_L_F,       "Female_12_Leg_Left");

        Fill(_hair,               "All_01_Hair");
        Fill(_headAttachment,     "All_02_Head_Attachment");
        Fill(_headCover_BaseHair, "HeadCoverings_Base_Hair");
        Fill(_headCover_NoFacial, "HeadCoverings_No_FacialHair");
        Fill(_headCover_NoHair,   "HeadCoverings_No_Hair");
        Fill(_chestAttach,        "All_03_Chest_Attachment");
        Fill(_backAttach,         "All_04_Back_Attachment");
        Fill(_shoulder_R,         "All_05_Shoulder_Attachment_Right");
        Fill(_shoulder_L,         "All_06_Shoulder_Attachment_Left");
        Fill(_elbow_R,            "All_07_Elbow_Attachment_Right");
        Fill(_elbow_L,            "All_08_Elbow_Attachment_Left");
        Fill(_hipsAttach,         "All_09_Hips_Attachment");
        Fill(_knee_R,             "All_10_Knee_Attachement_Right");
        Fill(_knee_L,             "All_11_Knee_Attachement_Left");
        Fill(_elfEar,             "Elf_Ear");
    }

    void Fill(List<GameObject> list, string groupName)
    {
        list.Clear();
        var group = FindGroup(groupName);
        if (group == null) return;
        for (int i = 0; i < group.childCount; i++)
        {
            var child = group.GetChild(i).gameObject;
            child.SetActive(false);
            list.Add(child);
        }
    }

    Transform FindGroup(string name)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(includeInactive: true))
            if (t.name == name) return t;
        return null;
    }
}
