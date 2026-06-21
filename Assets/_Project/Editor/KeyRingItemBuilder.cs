using UnityEditor;
using UnityEngine;

// Tools > Valdris > Build Keys Ring Item
//
// Creates the single "Keys" token item (the inventory representation of the
// Keyring) from SM_Item_Key_05, in a dedicated Resources folder so it can never
// be confused with normal loot. The Keyring system loads it from
// Resources/KeyRing/Keys and drops it into the inventory once the player has
// their first key.
public static class KeyRingItemBuilder
{
    const string Source = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Items/SM_Item_Key_05.prefab";
    const string OutDir = "Assets/_Project/Resources/KeyRing";
    const string OutPath = OutDir + "/Keys.prefab";

    [MenuItem("Tools/Valdris/Build Keys Ring Item")]
    public static void Build()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (src == null) { EditorUtility.DisplayDialog("Keys Ring", $"Source not found:\n{Source}", "OK"); return; }

        if (!System.IO.Directory.Exists(OutDir)) System.IO.Directory.CreateDirectory(OutDir);

        // Instantiate a copy of the key model and add the LootItem token component.
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(src);
        temp.name = "Keys";
        var loot = temp.GetComponent<LootItem>();
        if (loot == null) loot = temp.AddComponent<LootItem>();
        loot.itemType = LootItemType.KeyItem;
        loot.isKeyRing = true;                 // never routed/absorbed
        loot.displayNameOverride = "Keys";
        loot.flavorText = "A ring holding every key you've found.";
        loot.isStackable = false;

        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, OutPath);
        Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
        EditorUtility.DisplayDialog("Keys Ring",
            "Built the 'Keys' token at Resources/KeyRing/Keys.\n\n" +
            "Keys you loot now feed the Keyring; this token appears once you have any.", "OK");
    }
}
