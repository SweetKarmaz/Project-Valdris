using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Tools > Valdris > Scene > Make Solid Black
//
// Assigns a pure-black HDRP/Unlit material (ignores lighting, so it reads as a
// true void) to the selected object, or to "GreyspirePrisonBlackDoor" if nothing
// is selected. Handy for capping the tunnel mouth so you can't see through it.
public static class MakeSolidBlack
{
    const string MatDir  = "Assets/_Project/Data/Materials";
    const string MatPath = MatDir + "/VC_Black.mat";

    [MenuItem("Tools/Valdris/Scene/Make Solid Black")]
    public static void Apply()
    {
        var go = Selection.activeGameObject != null
            ? Selection.activeGameObject
            : GameObject.Find("GreyspirePrisonBlackDoor");
        if (go == null)
        {
            EditorUtility.DisplayDialog("Make Solid Black",
                "Select an object (or have a 'GreyspirePrisonBlackDoor' in the scene).", "OK");
            return;
        }

        var r = go.GetComponent<Renderer>();
        if (r == null)
        {
            EditorUtility.DisplayDialog("Make Solid Black", $"'{go.name}' has no Renderer.", "OK");
            return;
        }

        r.sharedMaterial = LoadOrCreateBlack();
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"[MakeSolidBlack] Set '{go.name}' to solid black.");
    }

    static Material LoadOrCreateBlack()
    {
        if (!System.IO.Directory.Exists(MatDir)) System.IO.Directory.CreateDirectory(MatDir);

        // Regular LIT black: looks black but responds to light like any surface,
        // so it doesn't read as zero luminance and spike auto-exposure (unlike
        // an Unlit black, which is what caused the blinding flash).
        var lit = Shader.Find("HDRP/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null)
        {
            m = new Material(lit) { name = "VC_Black" };
            AssetDatabase.CreateAsset(m, MatPath);
        }
        else if (m.shader != lit)
        {
            m.shader = lit;   // convert any earlier Unlit version
        }
        m.SetColor("_BaseColor", Color.black);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", Color.black);
        HDMaterial.ValidateMaterial(m);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
