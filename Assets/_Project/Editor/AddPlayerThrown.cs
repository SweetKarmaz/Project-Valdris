using UnityEditor;
using UnityEngine;

// Tools > Valdris > Add PlayerThrown to Player
//
// Finds the Player GameObject in the currently open scene (by "Player" tag),
// adds PlayerThrown if it isn't already there, and marks the scene dirty.
// Run once per scene that has a Player. Safe to re-run — skips if already added.
public static class AddPlayerThrown
{
    [MenuItem("Tools/Valdris/Add PlayerThrown to Player")]
    public static void Run()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Add PlayerThrown",
                "No GameObject tagged 'Player' found in the open scene.", "OK");
            return;
        }

        if (player.GetComponent<PlayerThrown>() != null)
        {
            EditorUtility.DisplayDialog("Add PlayerThrown",
                $"PlayerThrown is already on '{player.name}'.", "OK");
            return;
        }

        player.AddComponent<PlayerThrown>();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            player.scene);

        Debug.Log($"[AddPlayerThrown] Added PlayerThrown to '{player.name}'. " +
                  "Assign the spawnPoint Transform in the Inspector, then save the scene.");

        EditorUtility.DisplayDialog("Add PlayerThrown",
            $"PlayerThrown added to '{player.name}'.\n\n" +
            "Next: assign the 'Spawn Point' Transform in the Inspector " +
            "(the same empty child on the camera you use for PlayerRanged), " +
            "then save the scene.",
            "OK");
    }
}
