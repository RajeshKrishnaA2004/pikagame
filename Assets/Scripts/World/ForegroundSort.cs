using UnityEngine;

/// <summary>
/// Forces a fixed sorting order on every SpriteRenderer under this object, and
/// optionally on its own renderer.
///
/// Used for tree CANOPIES that overhang walkable ground: they must draw above
/// the player so the player can walk behind a canopy. Y-sorting cannot do this
/// - the canopy belongs to a tree whose base is at a different height than the
/// player's cell, so a fixed order is the only reliable way.
/// </summary>
public class ForegroundSort : MonoBehaviour
{
    [Tooltip("Order applied to child SpriteRenderers. Above the player (~996).")]
    public int sortingOrder = 4000;

    [Tooltip("Also apply to the SpriteRenderer on this object itself.")]
    public bool includeSelf = true;

    private void Awake()
    {
        Apply();
    }

    /// <summary>Applies the order now. Safe to call from an editor builder.</summary>
    public void Apply()
    {
        SpriteRenderer self = includeSelf ? GetComponent<SpriteRenderer>() : null;
        if (self != null)
            self.sortingOrder = sortingOrder;

        var children = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == self)
                continue;
            children[i].sortingOrder = sortingOrder;
        }
    }
}