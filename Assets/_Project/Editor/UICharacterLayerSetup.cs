using UnityEditor;
using UnityEngine;

// Ensures the "UICharacter" layer exists. The player model is placed on this
// layer so the main camera can exclude it (first-person) while the inventory
// portrait camera renders only it. Runs automatically on editor load and adds
// the layer to the first free user-layer slot if it isn't already present.
[InitializeOnLoad]
public static class UICharacterLayerSetup
{
    const string LayerName = "UICharacter";

    static UICharacterLayerSetup()
    {
        EnsureLayer();
    }

    static void EnsureLayer()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray) return;

        // Already present?
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == LayerName)
                return;

        // User layers are slots 8..31. Find the first empty one.
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = LayerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[UICharacterLayerSetup] Created '{LayerName}' layer in slot {i}.");
                return;
            }
        }

        Debug.LogWarning("[UICharacterLayerSetup] No free user-layer slot to add " +
            $"'{LayerName}'. Free a layer in Project Settings → Tags and Layers.");
    }
}
