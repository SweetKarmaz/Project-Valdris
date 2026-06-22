using System;
using UnityEngine;

// A branching conversation authored as a small node graph.
//
// A Node shows one speaker line. If it has choices, the player picks one (each
// choice can be gated by a world flag, can set a world flag, and points to the
// next node). If it has no choices it shows a "Continue" that follows `next`.
// `next == -1` ends the conversation.
//
// Re-talking an NPC can open on a different node: the runner scans nodes flagged
// isEntry top-down and uses the first whose entry gate passes (falling back to
// node 0). That's how "befriended" / "already met" states change the opening.
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Valdris/Dialogue")]
public class DialogueData : ScriptableObject
{
    [Serializable]
    public class Choice
    {
        [TextArea] public string text;

        [Header("Show only if (optional)")]
        public string requireFlag;          // empty = always shown
        public bool   requireValue = true;  // flag must equal this

        [Header("On pick (optional)")]
        public string setFlag;              // empty = sets nothing
        public bool   setValue = true;

        [Tooltip("Index of the node to go to. -1 ends the conversation.")]
        public int next = -1;
    }

    [Serializable]
    public class Node
    {
        [Tooltip("Speaker name. Empty → the dialogue's Default Speaker, then the NPC's name.")]
        public string speaker;
        [TextArea(2, 6)] public string text;

        [Header("Entry selection (optional)")]
        [Tooltip("Mark as a candidate opening node. Runner uses the first passing entry, else node 0.")]
        public bool   isEntry;
        public string entryRequireFlag;     // empty = no gate
        public bool   entryRequireValue = true;

        [Tooltip("Where 'Continue' goes when this node has no choices. -1 ends.")]
        public int next = -1;

        public Choice[] choices;

        public bool HasChoices => choices != null && choices.Length > 0;
    }

    [Tooltip("Fallback speaker name when a node leaves Speaker blank and no NPC name is supplied.")]
    public string defaultSpeaker;

    public Node[] nodes;

    // First isEntry node whose gate passes; otherwise 0.
    public int ResolveEntryIndex(WorldStateSystem flags)
    {
        if (nodes == null || nodes.Length == 0) return -1;
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (!n.isEntry) continue;
            if (string.IsNullOrEmpty(n.entryRequireFlag)) return i;
            bool v = flags != null && flags.GetFlag(n.entryRequireFlag);
            if (v == n.entryRequireValue) return i;
        }
        return 0;
    }
}
