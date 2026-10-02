using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Confirm talks to the NPC in the cell the player faces, or inspects it if the
/// object carries an <see cref="Inspectable"/>.
///
/// Only the cell directly in front is considered, so facing something is a real
/// positional act. Nothing happens while the dialogue box or a Yes/No box is
/// open, which stops Confirm from both advancing dialogue and re-triggering.
/// </summary>
[RequireComponent(typeof(GridPlayerController))]
public class PlayerInteraction : MonoBehaviour
{
    [Tooltip("Walkability grid. Auto-found if empty.")]
    public MapCollision map;

    [Tooltip("Line used when an NPC has no note of its own.")]
    [TextArea(2, 3)]
    public string fallbackNpcLine = "...";

    [Tooltip("Line used when facing empty, walkable ground.")]
    public string nothingLine = "Nothing here.";

    private GridPlayerController player;
    private readonly List<NpcVisual> npcs = new List<NpcVisual>();
    private readonly List<Inspectable> inspectables = new List<Inspectable>();
    private bool cached;

    void Awake()
    {
        player = GetComponent<GridPlayerController>();
        // Deliberately NOT owning GameInput here. GameInput is global; calling
        // Disable() from a second component would kill input for the player too.
        // GridPlayerController, which owns movement, is the only Enable/Disable.
    }

    void Start()
    {
        if (map == null)
            map = FindFirstObjectByType<MapCollision>();
        Cache();
    }

    void Update()
    {
        if (player == null)
            return;

        // Moving, or busy: Confirm belongs to the movement / UI, not to us.
        if (player.IsMoving)
            return;

        DialogueSystem dlg = DialogueSystem.Instance;
        if (dlg != null && dlg.IsOpen)
            return;

        YesNoBox box = YesNoBox.Instance;
        if (box != null && box.IsOpen)
            return;

        if (!GameInput.ConfirmPressed)
            return;

        TryInteract();
    }

    private void TryInteract()
    {
        if (!cached)
            Cache();

        Vector2Int target = player.Cell + FacingOffset(player.FacingIndex);

        // 1. An NPC standing in the cell in front.
        NpcVisual npc = FindNpc(target);
        if (npc != null)
        {
            Inspectable asInspect = npc.GetComponent<Inspectable>();
            if (asInspect != null && !string.IsNullOrEmpty(asInspect.note))
            {
                Say(asInspect.speaker, asInspect.note);
                return;
            }

            Say(npc.npcName, npc.defaultLine);
            return;
        }

        // 2. Something in that cell worth reading.
        Inspectable item = FindInspectable(target);
        if (item != null)
        {
            Say(item.speaker, item.note);
            return;
        }

        // 3. Nothing interactive there.
        Say(null, nothingLine);
    }

    /// <summary>Pushes one line into the dialogue box, or logs it if there is none.</summary>
    private static void Say(string speaker, string body)
    {
        DialogueSystem dlg = DialogueSystem.Instance;
        if (dlg != null)
            dlg.ShowLine(speaker, body);
        else
            Debug.Log("[Talk] " + (speaker ?? "") + ": " + body);
    }

    private void Cache()
    {
        npcs.Clear();
        inspectables.Clear();

        NpcVisual[] foundNpcs = FindObjectsByType<NpcVisual>(FindObjectsSortMode.None);
        npcs.AddRange(foundNpcs);

        Inspectable[] foundIns = FindObjectsByType<Inspectable>(FindObjectsSortMode.None);
        inspectables.AddRange(foundIns);

        cached = true;
    }

    private NpcVisual FindNpc(Vector2Int cell)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            if (npcs[i] == null)
                continue;
            if (CellOf(npcs[i].transform) == cell)
                return npcs[i];
        }
        return null;
    }

    private Inspectable FindInspectable(Vector2Int cell)
    {
        for (int i = 0; i < inspectables.Count; i++)
        {
            if (inspectables[i] == null)
                continue;
            if (CellOf(inspectables[i].transform) == cell)
                return inspectables[i];
        }
        return null;
    }

    private static Vector2Int CellOf(Transform t)
    {
        Vector3 p = t.position;
        return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
    }

    /// <summary>0=Down, 1=Left, 2=Right, 3=Up - matches GridPlayerController.</summary>
    private static Vector2Int FacingOffset(int facingIndex)
    {
        switch (facingIndex)
        {
            case 1: return Vector2Int.left;
            case 2: return Vector2Int.right;
            case 3: return Vector2Int.up;
            default: return Vector2Int.down;
        }
    }
}
