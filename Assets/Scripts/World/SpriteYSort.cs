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

    /// <summary>
    /// Highest order this object can take. The terrain tilemap renders at 0, so
    /// `baseOrder` must exceed (worldHeight * unitsPerStep) or everything at the
    /// top of the map slips behind the ground and vanishes. The world is 64 tall
    /// and the step is 4 => 256 worst case, hence 1000 leaves comfortable slack.
    /// </summary>
    public int baseOrder = 1000;

    [Tooltip("Hard floor so a misconfigured object can never hide behind terrain.")]
    public int minOrder = 1;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (sr == null)
            return;
        int order = baseOrder - Mathf.RoundToInt(transform.position.y * unitsPerStep);
        sr.sortingOrder = Mathf.Max(minOrder, order);
    }
}