using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges the pure <see cref="ForestGen"/> planner to the Chapter 1 builder:
/// deciding walkability BY CELL, and carving the brick path the player follows.
///
/// Nothing here holds a Unity asset reference, so it is safe to call before or
/// after the AssetDatabase work.
/// </summary>
public static class ChapterForest
{
    /// <summary>
    /// Is this cell impassable?
    ///
    /// Rules, in order:
    ///   1. The outer ring is blocked - EXCEPT the spawn cell. This layout's outer
    ///      ring is trees rather than a '#' wall, and '@' sits ON the bottom text
    ///      row, so sealing the ring blindly walled the player in completely.
    ///      Only one cell is exempt, and it is the one the player must stand on.
    ///   2. A cell inside any tree's bottom-40% base footprint is blocked.
    ///   3. A T/Y cell with no tree (a gap) is walkable.
    ///   4. Everything else follows the layout legend.
    /// </summary>
    public static bool CellBlocked(ChapterBuilder.TextMap map, int col, int row,
                                   ForestGen.Plan forest)
    {
        // The spawn cell is authored walkable and NOTHING may override it. It sits
        // inside the outer ring AND next to a tree in the bottom row, so either the
        // edge rule or a trunk's base footprint will otherwise seal it and the
        // player cannot take a single step. Checked first, deliberately.
        if (IsSpawnCell(map, col, row))
            return false;

        if (IsMapEdge(map, col, row))
            return true;

        Vector2Int cell = map.ToCell(col, row);

        if (forest.blocked.Contains(cell))
            return true;

        char ch = map.At(col, row);
        if (ch == 'T' || ch == 'Y')
            return forest.trees.ContainsKey(cell);      // a gap is walkable

        return ChapterBuilder.IsBlocking(ch);
    }

    /// <summary>
    /// The single cell exempt from the sealed outer ring. Keyed off the glyph
    /// rather than a cached Vector2Int so it stays correct if the layout moves.
    /// </summary>
    public static bool IsSpawnCell(ChapterBuilder.TextMap map, int col, int row)
    {
        return map.At(col, row) == '@';
    }

    /// <summary>True for the single ring of cells around the map.</summary>
    public static bool IsMapEdge(ChapterBuilder.TextMap map, int col, int row)
    {
        return col == 0 || col == map.Width - 1 ||
               row == 0 || row == map.Height - 1;
    }

    /// <summary>
    /// BFS from the spawn, then collect every cell on the route to each NPC's
    /// standing place and to the gym door, so the ground can be painted
    /// BrickPath and the route is provably continuous.
    /// </summary>
    public static HashSet<Vector2Int> CarveRoute(ChapterBuilder.TextMap map,
                                                 ForestGen.Plan forest,
                                                 out bool allFound)
    {
        var route = new HashSet<Vector2Int>();
        allFound = true;

        if (!ChapterBuilder.FindAscii(map, '@', out int sc, out int sr))
            return route;

        Vector2Int spawn = map.ToCell(sc, sr);
        var parent = Flood(map, forest, spawn);

        var targets = new List<Vector2Int>();
        if (ChapterBuilder.FindAscii(map, 'G', out int gc, out int gr))
            targets.Add(new Vector2Int(gc, (map.Height - 1) - gr - 1));

        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                if (map.At(c, r) != 'n')
                    continue;
                Vector2Int? stand =
                    NearestWalkableNeighbour(map, forest, parent, map.ToCell(c, r));
                if (stand == null)
                    allFound = false;
                else
                    targets.Add(stand.Value);
            }
        }

        foreach (Vector2Int t in targets)
            TraceTo(route, parent, spawn, t);

        return route;
    }
    private static readonly int[] NeighbourDX = { 1, -1, 0, 0 };
    private static readonly int[] NeighbourDY = { 0, 0, 1, -1 };

    /// <summary>BFS across walkable cells; maps each reachable cell to its parent.</summary>
    private static Dictionary<Vector2Int, Vector2Int> Flood(
        ChapterBuilder.TextMap map, ForestGen.Plan forest, Vector2Int start)
    {
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        if (CellBlocked(map, start.x, (map.Height - 1) - start.y, forest))
            return parent;

        parent[start] = start;
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            int col = cur.x;
            int row = (map.Height - 1) - cur.y;

            for (int i = 0; i < 4; i++)
            {
                int nc = col + NeighbourDX[i];
                int nr = row + NeighbourDY[i];
                if (nc < 0 || nc >= map.Width || nr < 0 || nr >= map.Height)
                    continue;
                if (CellBlocked(map, nc, nr, forest))
                    continue;

                Vector2Int next = map.ToCell(nc, nr);
                if (parent.ContainsKey(next))
                    continue;
                parent[next] = cur;
                queue.Enqueue(next);
            }
        }
        return parent;
    }

    private static Vector2Int? NearestWalkableNeighbour(
        ChapterBuilder.TextMap map, ForestGen.Plan forest,
        Dictionary<Vector2Int, Vector2Int> parent, Vector2Int npc)
    {
        for (int i = 0; i < 4; i++)
        {
            int col = npc.x + NeighbourDX[i];
            int row = ((map.Height - 1) - npc.y) + NeighbourDY[i];
            if (col < 0 || col >= map.Width || row < 0 || row >= map.Height)
                continue;
            if (CellBlocked(map, col, row, forest))
                continue;
            Vector2Int c = map.ToCell(col, row);
            if (parent.ContainsKey(c))
                return c;
        }
        return null;
    }

    private static void TraceTo(HashSet<Vector2Int> route,
                                Dictionary<Vector2Int, Vector2Int> parent,
                                Vector2Int from, Vector2Int to)
    {
        if (!parent.ContainsKey(to))
            return;

        Vector2Int cur = to;
        int guard = 0;
        while (cur != from && guard++ < 100000)
        {
            route.Add(cur);
            if (!parent.TryGetValue(cur, out cur))
                return;
        }
        route.Add(from);
    }

    /// <summary>Walkable cells the player could never reach from the spawn.</summary>
    public static List<Vector2Int> UnreachablePockets(ChapterBuilder.TextMap map,
                                                      ForestGen.Plan forest)
    {
        var pockets = new List<Vector2Int>();
        if (!ChapterBuilder.FindAscii(map, '@', out int sc, out int sr))
            return pockets;

        Vector2Int spawn = map.ToCell(sc, sr);
        var parent = Flood(map, forest, spawn);

        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                if (CellBlocked(map, c, r, forest))
                    continue;
                if (!parent.ContainsKey(map.ToCell(c, r)))
                    pockets.Add(map.ToCell(c, r));
            }
        }
        return pockets;
    }
}