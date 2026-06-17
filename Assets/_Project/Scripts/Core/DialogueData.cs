using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Valdris/Dialogue")]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public class Line { public string speaker; [TextArea] public string text; }
    public Line[] lines;
}
