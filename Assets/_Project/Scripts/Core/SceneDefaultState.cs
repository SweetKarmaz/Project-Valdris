using System.Collections.Generic;
using UnityEngine;

// Pristine reference state for a scene — created once by the editor tool
// (Tools > Valdris > Create Default Scene State) and NEVER modified at runtime.
//
// New games and the very first visit to a scene use this as the starting point.
// Because it is a ScriptableObject asset, Unity prevents accidental runtime writes
// in a build, keeping it safe for future playthroughs.
//
// NPCs: the list here represents the initial NPC configuration. The editor tool
// populates it from the scene; at runtime SceneStateManager reads it to know
// what a fresh visit should look like.
//
// Props: future — will capture interactable prop positions and states.
[CreateAssetMenu(fileName = "DefaultSceneState", menuName = "Valdris/Scene Default State")]
public class SceneDefaultState : ScriptableObject
{
    [Tooltip("NPC definitions as they should appear at the start of a new game. " +
             "Populated by Tools > Valdris > Create Default Scene State.")]
    public List<SavedNPCState> npcs = new();

    [Tooltip("Saveable props as they should appear at the start of a new game. " +
             "Populated by Tools > Valdris > Create Default Scene State.")]
    public List<SavedPropState> props = new();
}
