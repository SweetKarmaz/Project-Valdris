using UnityEngine;

// Shared logic for attaching a weapon/shield model to a character's hand bone,
// used by both the player (PlayerAppearanceComponent) and NPCs (NpcController).
//
// Synty hand bones carry a tiny lossyScale, so a "mount" under the bone cancels
// that scale (held items render at natural size; grip offsets are in metres).
// Grip offsets come from the per-item LootItem fields, falling back to the
// category defaults on PlayerManager.
public static class HeldItemVisual
{
    // Instantiates item's model under the given hand and returns the mount root
    // (destroy it to detach). Returns null if the bone isn't found.
    public static GameObject Attach(Transform root, LootItem item, EquipSlot slot,
                                    string rightBone, string leftBone)
    {
        if (item == null || root == null) return null;

        string boneName = slot == EquipSlot.MainHand ? rightBone : leftBone;
        Transform bone = FindBone(root, boneName);
        if (bone == null)
        {
            Debug.LogWarning($"[HeldItemVisual] Hand bone '{boneName}' not found on '{root.name}' for {slot}.");
            return null;
        }

        var mount = new GameObject($"_HeldMount_{slot}").transform;
        mount.SetParent(bone, false);
        mount.localPosition = Vector3.zero;
        mount.localRotation = Quaternion.identity;
        Vector3 bs = bone.lossyScale;
        mount.localScale = new Vector3(
            bs.x != 0f ? 1f / bs.x : 1f,
            bs.y != 0f ? 1f / bs.y : 1f,
            bs.z != 0f ? 1f / bs.z : 1f);

        var vis = Object.Instantiate(item.gameObject, mount);
        // The source may be inactive (generated items live under an inactive holder);
        // the held copy must be visible.
        vis.SetActive(true);
        vis.name = $"_Held_{slot}";
        foreach (var c in vis.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(c);
        foreach (var c in vis.GetComponentsInChildren<Collider>(true))      Object.Destroy(c);
        foreach (var c in vis.GetComponentsInChildren<Rigidbody>(true))     Object.Destroy(c);
        // Strip any lights so a held model never registers/destroys HDRP lights
        // as it's attached/detached.
        foreach (var l in vis.GetComponentsInChildren<Light>(true))         Object.Destroy(l);

        ApplyGrip(vis.transform, item, slot);

        // Match the bone's layer (player body = UICharacter, NPC = its own layer).
        int layer = bone.gameObject.layer;
        foreach (var t in mount.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        return mount.gameObject;
    }

    // Positions a held visual using the per-item grip offset if set, otherwise
    // the category default from PlayerManager. Called on attach and (for the
    // player) re-applied each frame for live tuning.
    public static void ApplyGrip(Transform vis, LootItem item, EquipSlot slot)
    {
        if (vis == null || item == null) return;
        var pm = PlayerManager.Instance;

        Vector3 catRot = Vector3.zero, catPos = Vector3.zero;
        if (pm != null)
        {
            if (slot == EquipSlot.OffHand && item.itemType == LootItemType.Armor)
            { catRot = pm.shieldGripRotation;    catPos = pm.shieldGripPosition; }
            else if (item.itemType == LootItemType.Weapon && item.isTwoHanded)
            { catRot = pm.twoHandedGripRotation; catPos = pm.twoHandedGripPosition; }
            else
            { catRot = pm.oneHandedGripRotation; catPos = pm.oneHandedGripPosition; }
        }

        Vector3 rot = item.gripRotationOffset != Vector3.zero ? item.gripRotationOffset : catRot;
        Vector3 pos = item.gripPositionOffset != Vector3.zero ? item.gripPositionOffset : catPos;
        Vector3 scl = item.gripScale == Vector3.zero ? Vector3.one : item.gripScale;

        vis.localRotation = Quaternion.Euler(rot);
        vis.localScale    = scl;

        if (item.gripAtCenter)
        {
            var mf = vis.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 c = mf.sharedMesh.bounds.center;
                pos -= Quaternion.Euler(rot) * Vector3.Scale(c, scl);
            }
        }
        vis.localPosition = pos;
    }

    static Transform FindBone(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
