using UnityEngine;

// Shared item-icon lookup used by the inventory grid and the HUD hotbar so they
// render items identically: the LootItem's sprite if it has one, otherwise an
// editor asset preview of the prefab (null at runtime → callers fall back to the
// item name).
public static class ItemIconUtil
{
    public static Texture Get(LootItem item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon.texture;
#if UNITY_EDITOR
        GameObject previewGo = item.gameObject;
        if (item.IsGenerated && item.runtimeRoll != null)
        {
            var basePrefab = SaveSystem.Instance?.database?.lootRegistry?
                .FindByName(item.runtimeRoll.basePrefabName);
            if (basePrefab != null) previewGo = basePrefab.gameObject;
        }
        return UnityEditor.AssetPreview.GetAssetPreview(previewGo);
#else
        return null;
#endif
    }
}
