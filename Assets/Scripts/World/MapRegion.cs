using UnityEngine;

/// <summary>
/// Names a patch of the world. Drop one on a trigger collider over a region;
/// when the player walks in, the HUD's region label updates.
///
/// Regions are also usable as gate conditions (set `flagOnEnter` and have a
/// RequirementGate ask for that flag), so "you must have visited the lab"
/// style gating works without extra code.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapRegion : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Shown on the HUD, e.g. \"NORTH RIDGE\".")]
    public string regionName = "Unnamed";
    [Tooltip("Optional second line, e.g. \"Tall grass\".")]
    public string subLabel = "";
    [Tooltip("Lower numbers win when regions overlap. Use for nested areas.")]
    public int priority = 0;

    [Header("State")]
    [Tooltip("Set this GameState flag the first time the player enters.")]
    public string flagOnEnter = "";

    [Header("Minimap")]
    [Tooltip("Draw a tint over this region on the minimap (optional).")]
    public bool highlightOnMinimap = false;
    public Color minimapTint = new Color(0.4f, 0.9f, 1f, 0.25f);

    private static MapRegion activeRegion;

    /// <summary>The region the player is currently inside (or null).</summary>
    public static MapRegion Active { get { return activeRegion; } }

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        GameState state = GameState.Instance;
        if (state != null && !string.IsNullOrEmpty(flagOnEnter))
            state.SetFlag(flagOnEnter);

        if (activeRegion != null && activeRegion != this &&
            activeRegion.priority > priority)
            return;

        activeRegion = this;

        if (HUDController.Instance != null)
            HUDController.Instance.SetRegion(regionName, subLabel);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        if (activeRegion == this)
        {
            activeRegion = null;
            if (HUDController.Instance != null)
                HUDController.Instance.SetRegion("", "");
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;
        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag("Player"))
            return true;
        return other.CompareTag("Player");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.55f, 0.75f, 1f, 0.5f);
        var box = GetComponent<Collider2D>() as BoxCollider2D;
        if (box != null)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset,
                                new Vector3(box.size.x, box.size.y, 0.1f));
    }
}