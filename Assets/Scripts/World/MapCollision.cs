using UnityEngine;

/// <summary>
/// Authoritative walkability grid for a text-layout map.
///
/// Cells are indexed in WORLD space: index = y * width + x, where (0,0) is the
/// bottom-left cell of the grid (Unity's +Y is up). The ASCII layout is stored
/// top-down, so the MapBuilder flips rows when it fills this array.
///
/// Keeping this as data (rather than relying purely on physics) is what lets
/// ValidateStage1 run a BFS from spawn to the gym door in -batchmode, where no
/// physics step is ever executed.
///
/// The grid is stored as a STRING of '0'/'1' rather than a bool[] on purpose.
/// Unity's bool[] scene serialization did not round-trip here: a 24x23 (552 cell)
/// map came back out of the .unity file as 1104 characters, so the array the
/// player read at runtime was garbage and it could not step anywhere. A string is
/// written verbatim. The bool[] is a non-serialized runtime cache only.
/// </summary>
public class MapCollision : MonoBehaviour
{
    public int width;
    public int height;

    /// <summary>Row-major from y=0. '1' = player may not stand here.</summary>
    public string blockedBits = "";

    /// <summary>Runtime cache, rebuilt from <see cref="blockedBits"/>.</summary>
    [System.NonSerialized]
    public bool[] blocked;

    /// <summary>Cell the player spawns on.</summary>
    public Vector2Int spawnCell;

    /// <summary>Walkable cell in front of the gym door (the level objective).</summary>
    public Vector2Int gymDoorCell;

    /// <summary>Name of the layout file this map was built from.</summary>
    public string layoutName = "";

    /// <summary>
    /// (Re)builds the bool cache from the bit string. Cheap and idempotent - it
    /// only reallocates when the size does not already match.
    /// </summary>
    public void Rebuild()
    {
        int n = width * height;
        if (n <= 0)
        {
            blocked = new bool[0];
            return;
        }
        if (blocked == null || blocked.Length != n)
            blocked = new bool[n];

        for (int i = 0; i < n; i++)
            blocked[i] = blockedBits != null
                         && blockedBits.Length > i
                         && blockedBits[i] == '1';
    }

    /// <summary>
    /// Writes a grid into both the cache and the serialised string.
    /// Used by the builder; keeps the cache warm for validation.
    /// </summary>
    public void SetGrid(bool[] grid)
    {
        blocked = grid;
        if (grid == null)
        {
            blockedBits = "";
            return;
        }

        var sb = new System.Text.StringBuilder(grid.Length);
        for (int i = 0; i < grid.Length; i++)
            sb.Append(grid[i] ? '1' : '0');
        blockedBits = sb.ToString();
    }

    void Awake()
    {
        Rebuild();
    }

    void OnValidate()
    {
        // Keep the cache in step in the editor so nothing reads a stale array.
        Rebuild();
    }

    public bool Contains(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }

    public bool IsBlocked(Vector2Int cell)
    {
        if (!Contains(cell))
            return true;                       // outside the map counts as blocked

        int n = width * height;
        if (blocked == null || blocked.Length != n)
            Rebuild();
        if (blocked == null || blocked.Length != n)
            return true;

        return blocked[cell.y * width + cell.x];
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
    }

    /// <summary>World position of a cell's bottom-centre (where a sprite stands).</summary>
    public Vector2 CellFoot(Vector2Int cell)
    {
        return new Vector2(cell.x + 0.5f, cell.y);
    }
}
