using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grid-based, tile-for-tile player movement (Pokémon style).
///
/// Differences from the retired <see cref="PlayerController"/>:
///   * ONE tile per step, interpolated smoothly between cells
///   * blocked by MapCollision cells AND by any collider in the target cell
///   * turns in place first when a new direction is tapped
///   * no run button (speed is fixed)
///   * reads the NEW Input System via <see cref="GameInput"/>
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GridPlayerController : MonoBehaviour
{
    public static GridPlayerController Instance { get; private set; }

    [Header("Stepping")]
    [Tooltip("Seconds to glide across one tile. 1 unit / this = tiles per second.")]
    public float stepDuration = 0.16f;

    [Tooltip("Pause after turning before a step may begin, so the turn is visible.")]
    public float turnDelay = 0.09f;

    [Header("Collision")]
    [Tooltip("Walkability grid built by the MapBuilder. Auto-found if empty.")]
    public MapCollision map;

    [Tooltip("Player's own trigger probe (must match the builder).")]
    public Vector2 probeSize = new Vector2(0.7f, 0.7f);
    public Vector2 probeOffset = new Vector2(0f, 0.35f);

    [Header("State")]
    public bool frozen;

    /// <summary>0 = Down, 1 = Left, 2 = Right, 3 = Up (matches the sprite rows).</summary>
    public int FacingIndex { get; private set; }

    public bool IsMoving { get; private set; }

    /// <summary>0..1 progress through the current step.</summary>
    public float StepProgress { get; private set; }

    public Vector2Int Cell { get; private set; }

    public Vector2 Facing
    {
        get
        {
            switch (FacingIndex)
            {
                case 1: return Vector2.left;
                case 2: return Vector2.right;
                case 3: return Vector2.up;
                default: return Vector2.down;
            }
        }
    }

    private Vector2 stepFrom;
    private Vector2 stepTo;
    private float stepT;
    private float turnCooldown;
    private readonly Collider2D[] probe = new Collider2D[8];

    void Awake()
    {
        Instance = this;

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (map == null)
            map = FindFirstObjectByType<MapCollision>();

        // Snap to the spawn cell before the first frame renders.
        if (map != null)
        {
            Cell = map.spawnCell;
            transform.position = new Vector3(Cell.x + 0.5f, Cell.y, 0f);
        }

        FacingIndex = 0;
    }

    void OnEnable() { GameInput.Enable(); }
    void OnDisable() { GameInput.Disable(); }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (IsMoving)
        {
            AdvanceStep();
            return;
        }

        if (frozen)
            return;

        if (turnCooldown > 0f)
            turnCooldown -= Time.deltaTime;

        Vector2Int dir = Dominant(GameInput.Move);
        if (dir == Vector2Int.zero)
        {
            Diagnose("no input");
            return;
        }

        int want = FaceIndexOf(dir);

        // Turn in place FIRST: a new direction only changes facing here, and a
        // step is not allowed until turnDelay has elapsed on the matching face.
        if (want != FacingIndex)
        {
            FacingIndex = want;
            turnCooldown = turnDelay;
            Diagnose("turning to face " + dir + " (turnDelay)");
            return;
        }

        if (turnCooldown > 0f)
        {
            Diagnose("waiting out turnDelay");
            return;
        }

        if (!TryStep(dir))
            Diagnose("step into " + (Cell + dir) + " refused"
                     + (map != null && map.IsBlocked(Cell + dir) ? " [map blocked]" : "")
                     + (BlockedByCollider(Cell + dir) ? " [collider]" : ""));
        else
            Diagnose(null);
    }

    /// <summary>
    /// Temporary self-diagnosis: prints WHY a held direction produced no step,
    /// once a second. Movement bugs in a grid game are otherwise invisible - the
    /// player just stands there and nothing in the log says why.
    /// </summary>
    public static bool debugMovement = true;
    private static float nextDiag;
    private static string lastReason = "";

    private void Diagnose(string reason)
    {
        if (!debugMovement)
            return;

        if (reason == null)
        {
            lastReason = "";
            return;
        }

        if (reason == lastReason && Time.unscaledTime < nextDiag)
            return;

        lastReason = reason;
        nextDiag = Time.unscaledTime + 1f;

        string why = reason;
        if (IsMoving)
            why = "already mid-step";
        else if (frozen)
            why = "FROZEN (dialogue or cutscene)";

        Debug.Log("[Move] cell=" + Cell + " facing=" + FacingIndex
                  + " input=" + GameInput.Move
                  + " mapWired=" + (map != null)
                  + " -> " + why);
    }

    /// <summary>Returns false when the target cell is blocked for any reason.</summary>
    private bool TryStep(Vector2Int dir)
    {
        Vector2Int target = Cell + dir;

        if (map != null && map.IsBlocked(target))
            return false;

        if (BlockedByCollider(target))
            return false;

        stepFrom = transform.position;
        stepTo = new Vector3(target.x + 0.5f, target.y, 0f);
        stepT = 0f;
        StepProgress = 0f;
        Cell = target;
        IsMoving = true;
        return true;
    }

    private void AdvanceStep()
    {
        stepT += Time.deltaTime / Mathf.Max(0.01f, stepDuration);
        StepProgress = Mathf.Clamp01(stepT);

        Vector2 pos = Vector2.Lerp(stepFrom, stepTo, StepProgress);
        transform.position = new Vector3(pos.x, pos.y, 0f);

        if (stepT < 1f)
            return;

        transform.position = new Vector3(stepTo.x, stepTo.y, 0f);
        IsMoving = false;
        StepProgress = 0f;
        turnCooldown = 0f;     // free to keep walking the way we already face
    }

    /// <summary>Is there a solid (non-self) collider inside the target cell?</summary>
    private bool BlockedByCollider(Vector2Int target)
    {
        Vector2 centre = new Vector2(target.x + 0.5f, target.y) + probeOffset;
        int n = Physics2D.OverlapBoxNonAlloc(centre, probeSize, 0f, probe);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = probe[i];
            if (c == null || c.isTrigger)
                continue;
            if (c.transform == transform || c.transform.IsChildOf(transform))
                continue;
            return true;
        }
        return false;
    }

    private static Vector2Int Dominant(Vector2 v)
    {
        if (v.sqrMagnitude < 0.01f)
            return Vector2Int.zero;
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x > 0f ? Vector2Int.right : Vector2Int.left;
        return v.y > 0f ? Vector2Int.up : Vector2Int.down;
    }

    private static int FaceIndexOf(Vector2Int dir)
    {
        if (dir == Vector2Int.down) return 0;
        if (dir == Vector2Int.left) return 1;
        if (dir == Vector2Int.right) return 2;
        return 3;
    }

    /// <summary>Teleport (used when loading a scene).</summary>
    public void Teleport(Vector2Int cell)
    {
        Cell = cell;
        IsMoving = false;
        StepProgress = 0f;
        transform.position = new Vector3(cell.x + 0.5f, cell.y, 0f);
    }
}
