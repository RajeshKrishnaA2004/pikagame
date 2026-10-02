using UnityEngine;

/// <summary>
/// Marks a world object as readable. Confirm while facing it prints its note.
/// Used for signs, mailboxes and anything else that is looked at, not talked to.
/// </summary>
public class Inspectable : MonoBehaviour
{
    [Tooltip("Shown in the dialogue box when the player faces this.")]
    [TextArea(2, 4)]
    public string note = "Nothing worth writing down.";

    [Tooltip("Optional speaker name. Leave empty for an unattributed line.")]
    public string speaker = "";

    [Tooltip("Also usable when the object blocks the cell - i.e. you can read a sign in a wall.")]
    public bool readFromAdjacent = true;
}
