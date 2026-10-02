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
/// </summary>
public class MapCollision : MonoBehaviour
{
    public int width;
    public int height;

    /// <summary>True = player may not stand here.</summary>
    public bool[] blocked;

    /// <summary>Cell the player spawns on.</summary>
    public Vector2Int spawnCell;

    /// <summary>Walkable cell in front of the gym door (the level objective).</summary>
    public Vector2Int gymDoorCell;

    /// <summary>Name of the layout file this map was built from.</summary>
    public string layoutName = "";

    public bool Contains(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }

    public bool IsBlocked(Vector2Int cell)
    {
        if (!Contains(cell))
            return true;                       // outside the map counts as blocked
        return blocked != null && blocked[cell.y * width + cell.x];
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
