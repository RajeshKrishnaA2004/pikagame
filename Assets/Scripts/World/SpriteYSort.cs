using UnityEngine;

/// <summary>
/// Top-down depth sorting without depending on the renderer asset's
/// transparency-sort settings: the further up the screen something is, the
/// earlier it draws, so the player correctly passes behind tree trunks and in
/// front of foreground bushes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(100)]
public class SpriteYSort : MonoBehaviour
{
    [Tooltip("Sub-units of world Y per sorting-order step.")]
    public float unitsPerStep = 4f;

    [Tooltip("Base offset so UI-facing sprites stay above the terrain tilemap.")]
    public int baseOrder = 10;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (sr == null)
            return;
        sr.sortingOrder = baseOrder - Mathf.RoundToInt(transform.position.y * unitsPerStep);
    }
}