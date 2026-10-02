using UnityEngine;

/// <summary>
/// Appearance for a route NPC, kept as DATA on the object so real NPC art can
/// be dropped in from the Inspector without touching any code.
///
/// Right now ChapterBuilder assigns the player's down-frame as a stand-in,
/// because the art pack has no NPC sprite yet. Each NPC gets a different tint
/// so they are immediately distinguishable from the player. Assign a real
/// sprite to <see cref="sprite"/> and the tint stops mattering.
/// </summary>
public class NpcVisual : MonoBehaviour
{
    [Header("Art")]
    [Tooltip("Swap in real NPC art here. Leave empty to fall back to the stand-in.")]
    public Sprite sprite;

    [Tooltip("Applied on top of the sprite. Used to tell NPCs apart while they " +
             "share the player's stand-in art.")]
    public Color tint = Color.white;

    [Header("Identity")]
    [Tooltip("Shown in logs and used later by the dialogue system.")]
    public string npcName = "NPC";

    [Tooltip("Said when the player faces this NPC and presses Confirm.")]
    [TextArea(2, 4)]
    public string defaultLine = "...";

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        Apply();
    }

    /// <summary>Re-apply sprite + tint. Safe to call from an editor preview.</summary>
    public void Apply()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        if (sprite != null)
            spriteRenderer.sprite = sprite;
        if (spriteRenderer.sprite != null)
            spriteRenderer.color = tint;
    }

    /// <summary>
    /// Used by the builder when it assembles an NPC. The stand-in is rendered
    /// immediately, but the public <see cref="sprite"/> field is deliberately
    /// left empty so it stays free for real NPC art.
    /// </summary>
    public void Configure(Sprite standIn, Color npcTint, string label)
    {
        tint = npcTint;
        npcName = label;

        if (standIn != null)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = standIn;
        }
        Apply();
    }
}