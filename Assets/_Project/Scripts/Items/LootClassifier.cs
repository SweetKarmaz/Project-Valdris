// Generatable item categories for randomized loot. Each weapon class carries its
// handedness so the damage-range lookup is direct. Armor maps to its slot;
// accessories are limited to Ring / Necklace (the only magic-eligible ones).
public enum GenCategory
{
    None = 0,

    // Weapons
    AxeOneHand, AxeTwoHand,
    Dagger,
    MaceOneHand, MaceTwoHand,
    Ranged,
    Sceptre,
    Spear,            // always two-handed
    Staff,
    SwordOneHand, SwordTwoHand,
    Thrown,
    Shield,

    // Armor
    ArmorBack, ArmorChest, ArmorHands, ArmorHead, ArmorHips, ArmorLegs, ArmorShoulders,

    // Accessories
    Ring, Necklace,
}

// Folder-authoritative classification of a Loot prefab into a GenCategory.
// Runs at editor build time (LootRegistryBuilder has the asset path); the result
// is cached on the LootRegistry so the runtime generator never needs the path.
public static class LootClassifier
{
    // Two-handed name keywords for folders that contain both 1H and 2H weapons
    // (Axes, Maces, Swords). Spears are always 2H via their own category.
    static readonly string[] TwoHandKeywords =
        { "great", "large", "zwei", "maul", "claymore", "halberd" };

    public static bool IsTwoHandedCategory(GenCategory c) =>
        c == GenCategory.AxeTwoHand || c == GenCategory.MaceTwoHand ||
        c == GenCategory.SwordTwoHand || c == GenCategory.Spear;

    public static bool IsWeapon(GenCategory c) =>
        c != GenCategory.None && c <= GenCategory.Thrown; // Shield is armor, see below

    // path: full asset path, e.g. ".../Loot/Weapons/Axes/GreatAxe_01.prefab".
    public static GenCategory ClassifyByPath(string path, string prefabName)
    {
        if (string.IsNullOrEmpty(path)) return GenCategory.None;
        string p = path.Replace('\\', '/').ToLowerInvariant();
        string n = (prefabName ?? string.Empty).ToLowerInvariant();

        // Ignore Synty's accidental duplicate prefabs.
        if (n.Contains("_duplicate")) return GenCategory.None;

        bool TwoH()
        {
            foreach (var k in TwoHandKeywords) if (n.Contains(k)) return true;
            return false;
        }

        if (p.Contains("/weapons/axes/"))    return TwoH() ? GenCategory.AxeTwoHand   : GenCategory.AxeOneHand;
        if (p.Contains("/weapons/maces/"))   return TwoH() ? GenCategory.MaceTwoHand  : GenCategory.MaceOneHand;
        if (p.Contains("/weapons/swords/"))  return TwoH() ? GenCategory.SwordTwoHand : GenCategory.SwordOneHand;
        if (p.Contains("/weapons/daggers/")) return GenCategory.Dagger;
        if (p.Contains("/weapons/ranged/"))  return GenCategory.Ranged;
        if (p.Contains("/weapons/sceptre/")) return GenCategory.Sceptre;
        if (p.Contains("/weapons/spears/"))  return GenCategory.Spear;
        if (p.Contains("/weapons/staffs/"))  return GenCategory.Staff;
        if (p.Contains("/weapons/thrown/"))  return GenCategory.Thrown;
        if (p.Contains("/weapons/shields/")) return GenCategory.Shield;

        if (p.Contains("/armor/back/"))      return GenCategory.ArmorBack;
        if (p.Contains("/armor/chest/"))     return GenCategory.ArmorChest;
        if (p.Contains("/armor/hands/"))     return GenCategory.ArmorHands;
        if (p.Contains("/armor/head/"))      return GenCategory.ArmorHead;
        if (p.Contains("/armor/hips/"))      return GenCategory.ArmorHips;
        if (p.Contains("/armor/legs/"))      return GenCategory.ArmorLegs;
        if (p.Contains("/armor/shoulders/")) return GenCategory.ArmorShoulders;

        // Accessories: only Rings and Necklaces are magic-eligible for now.
        if (p.Contains("/accessories/"))
        {
            if (n.StartsWith("ring"))     return GenCategory.Ring;
            if (n.StartsWith("necklace")) return GenCategory.Necklace;
        }
        return GenCategory.None;
    }
}
