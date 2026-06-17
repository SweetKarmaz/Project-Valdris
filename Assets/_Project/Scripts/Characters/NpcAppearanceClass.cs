using System.Collections.Generic;
using UnityEngine;

// ScriptableObject that defines which visual parts a class can wear.
// Create one per class via: Assets > Create > Valdris > NPC Appearance Class
//
// The PolygonFantasyHeroCharacters preset prefabs organise their child meshes
// under named groups ("Male_03_Torso", "All_04_Back_Attachment", etc.).
// Each group contains N children — index 0 is always the bare/minimal version,
// higher indices add more elaborate gear.
//
// Set min/max index ranges to constrain which variants a class can draw from.
// Leave max at -1 to use the full list for that slot.
// Open the Synty demo scene (Scenes/Demo_RandomCharacter) to preview indices.
[CreateAssetMenu(fileName = "NpcClass_New", menuName = "Valdris/NPC Appearance Class")]
public class NpcAppearanceClass : ScriptableObject
{
    [Header("Identity")]
    public string className = "Peasant";

    [TextArea(1, 3)]
    public string description;

    [Header("Gender Preference")]
    public GenderPreference gender = GenderPreference.Random;

    [Header("Race Preference")]
    public RacePreference race = RacePreference.Human;

    [Header("Head Covering Style")]
    [Tooltip("Base_Hair = visible hair under helm. No_FacialHair = full face helm. No_Hair = full helm, hair tucked.")]
    public HeadCoveringStyle headCoveringStyle = HeadCoveringStyle.Random;

    [Header("Facial Hair (male only)")]
    public FacialHairChance facialHair = FacialHairChance.Sometimes;

    [Header("Torso (chest armour / clothing)")]
    public SlotRange torso = new(1, -1);

    [Header("Upper Arms")]
    public SlotRange armUpper = new(0, -1);

    [Header("Lower Arms / Vambraces")]
    public SlotRange armLower = new(0, -1);

    [Header("Hands / Gauntlets")]
    public SlotRange hands = new(0, -1);

    [Header("Hips / Belt")]
    public SlotRange hips = new(1, -1);

    [Header("Legs")]
    public SlotRange legs = new(0, -1);

    [Header("Shoulder Attachments (pauldrons)")]
    public SlotRange shoulderAttach = new(0, -1);
    [Tooltip("15% chance by default in vanilla randomizer. Set to 0 to always mirror left=right.")]
    [Range(0, 100)]
    public int shoulderMismatchChance = 15;

    [Header("Elbow Attachments")]
    public SlotRange elbowAttach = new(0, -1);
    [Range(0, 100)]
    public int elbowMismatchChance = 10;

    [Header("Knee Attachments")]
    public SlotRange kneeAttach = new(0, -1);
    [Range(0, 100)]
    public int kneeMismatchChance = 10;

    [Header("Chest Attachment (tabard, front decor)")]
    public bool allowChestAttachment = true;
    public SlotRange chestAttach = new(0, -1);

    [Header("Back Attachment (cape, wings, quiver)")]
    public bool allowBackAttachment = false;
    public SlotRange backAttach = new(0, -1);

    [Header("Hips Attachment (skirt, pouches)")]
    public bool allowHipsAttachment = false;
    public SlotRange hipsAttach = new(0, -1);

    [Header("Material Colour Variant")]
    [Tooltip("Which pre-baked texture set to use (1-4). -1 = random.")]
    [Range(-1, 4)]
    public int textureSet = -1;

    [Tooltip("Which colour variant within the texture set (A/B/C). -1 = random.")]
    public ColourVariant colourVariant = ColourVariant.Random;

    // Allowed material list (auto-built from textureSet + colourVariant at runtime).
    // Leave empty to use all available HDRP materials.
    [Tooltip("Override: drag specific HDRP materials here. Leave empty to auto-select from textureSet/colourVariant.")]
    public List<Material> allowedMaterials = new();
}

// ── Supporting types ──────────────────────────────────────────────────────────

[System.Serializable]
public struct SlotRange
{
    [Tooltip("First index to include (inclusive). 0 = bare/no-armour variant.")]
    public int min;
    [Tooltip("Last index to include (inclusive). -1 = use all available.")]
    public int max;

    public SlotRange(int min, int max) { this.min = min; this.max = max; }

    public int Clamp(int count) => Mathf.Clamp(max < 0 ? count - 1 : max, min, count - 1);

    public int Random(int count)
    {
        int lo = Mathf.Clamp(min, 0, count - 1);
        int hi = Clamp(count);
        return lo >= hi ? lo : UnityEngine.Random.Range(lo, hi + 1);
    }
}

public enum GenderPreference  { Random, Male, Female }
public enum RacePreference    { Random, Human, Elf }
public enum HeadCoveringStyle { Random, BaseHair, NoFacialHair, NoHair }
public enum FacialHairChance  { Never, Sometimes, Always }
public enum ColourVariant     { Random, A, B, C }
