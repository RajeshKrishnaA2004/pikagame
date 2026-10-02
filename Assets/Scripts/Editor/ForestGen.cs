using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seeded, repeatable forest generator for the route.
///
/// Two things matter here:
///  * LOOK - trees must not read as a grid. Sprites are picked at random, nudged,
///    scaled, flipped, alternate rows are staggered, the outer band has a ragged
///    depth, and roughly one cell in ten is left as a gap.
///  * COLLISION - decided by CELL, never by the visual jitter. A tree blocks its
///    trunk cell plus every cell its bottom 40% covers; the canopy above that is
///    walkable and drawn over the player, so you can walk behind a canopy.
///
/// The random offset/scale/flip are presentation ONLY. Collision uses the
/// untransformed sprite bounds so it stays exact and stable between builds.
/// </summary>
public static class ForestGen
{
    /// <summary>Fixed so every build produces the same forest.</summary>
    public const int Seed = 20260214;

    /// <summary>Fraction of a sprite's height, from its base, that BLOCKS.</summary>
    public const float BaseBlockFraction = 0.40f;

    /// <summary>
    /// A cell blocks when its base footprint covers this much of it.
    /// Coverage() returns overlap AREA, so 1.0 = the whole cell is under the
    /// trunk. This must be well above half a cell: at 0.25 a tree only had to
    /// clip a 0.5x0.5 corner of a cell to seal it, and the forest walls off the
    /// open ground around it - which is what trapped the player at spawn.
    /// </summary>
    public const float BlockCoverage = 0.55f;

    /// <summary>Chance a candidate cell is left as a gap instead of a tree.</summary>
    public const float GapChance = 0.10f;

    public const int MaxTreeBig = 3;

    /// <summary>
    /// Weighted pool. Bushes read as undergrowth inside the treeline.
    /// Tree_Big appears once, so it is naturally rare, and PickSprite further
    /// caps it at <see cref="MaxTreeBig"/> and never allows two in a row.
    /// </summary>
    public static readonly string[] TreePool =
    {
        "Tree_Small", "Tree_Small", "Tree_Medium", "Tree_Medium",
        "Pine", "Tree_Autumn", "Bush", "Bush_Flowers", "Tree_Big"
    };

    public class Tree
    {
        public Vector2Int cell;
        public string sprite;
        public Vector2 offset;      // visual only, +/-0.25
        public float scale = 1f;    // visual only, 0.9 - 1.1
        public bool flipX;
        public bool foreground;     // canopy overhangs walkable ground
        public bool isBig;
    }

    public class Plan
    {
        public readonly Dictionary<Vector2Int, Tree> trees = new Dictionary<Vector2Int, Tree>();
        public readonly Dictionary<Vector2Int, string> gaps = new Dictionary<Vector2Int, string>();
        public readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> canopy = new HashSet<Vector2Int>();
        public int placed, skipped, bigCount, gapCount;

        public int ForegroundCount
        {
            get
            {
                int n = 0;
                foreach (var kv in trees)
                    if (kv.Value.foreground)
                        n++;
                return n;
            }
        }
    }

    /// <summary>Deterministic 32-bit hash -> [0,1). Used for band depths.</summary>
    public static float Hash01(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0x00FFFFFFu) / 16777216f;
        }
    }

    // ------------------------------------------------------------ band depths

    /// <summary>Forest depth from the top at this column: 3, 4 or 5.</summary>
    public static int TopDepth(int col)
    {
        return 3 + (int)(Hash01(col, 11) * 3f);
    }

    /// <summary>Forest depth in from each side at this row: 2, 3 or 4.</summary>
    public static int SideDepth(int row)
    {
        return 2 + (int)(Hash01(row, 57) * 3f);
    }

    /// <summary>Forest depth from the bottom: 1 or 2. The spawn corner stays open.</summary>
    public static int BottomDepth(int col)
    {
        if (col < 4 || col > 19)
            return 0;
        return 1 + (int)(Hash01(col, 91) * 2f);
    }

    public static bool IsForestBand(int col, int row, int width, int height)
    {
        if (row < TopDepth(col))
            return true;
        if (col < SideDepth(row) || col >= width - SideDepth(row))
            return true;
        return row >= height - BottomDepth(col);
    }

    // -------------------------------------------------------------------- rng

    /// <summary>Small deterministic PRNG (xorshift32) so builds repeat exactly.</summary>
    private class Rng
    {
        private uint s;
        public Rng(int seed) { s = (uint)seed; if (s == 0) s = 0x9E3779B9u; }
        public uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        public float Value01() { return (Next() & 0x00FFFFFFu) / 16777216f; }
        public float Range(float lo, float hi) { return lo + (hi - lo) * Value01(); }
        public int Range(int lo, int hi)          // inclusive
        {
            if (hi <= lo) return lo;
            return lo + (int)(Next() % (uint)(hi - lo + 1));
        }
        public bool Chance(float p) { return Value01() < p; }
    }
    // ------------------------------------------------------------- generation

    /// <summary>
    /// Build the forest. <paramref name="sizeOf"/> gives a sprite's world size at
    /// its canonical scale, so the caller controls the per-folder PPU.
    /// </summary>
    public static Plan Generate(char[][] grid, int width, int height,
                                System.Func<string, Vector2> sizeOf)
    {
        var plan = new Plan();
        var rng = new Rng(Seed);

        // previous sprite per row, so we never get 3 identical trees in a row
        var lastSprite = new string[height];
        var repeatRun = new int[height];
        int bigUsed = 0;

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                char ch = grid[r][c];

                // Authored spawn and brick-path cells stay clear even where
                // the procedural forest band crosses them. Otherwise trees
                // can overwrite the only way out of the spawn clearing.
                if (ch == '@' || ch == '=')
                    continue;

                bool band = IsForestBand(c, r, width, height);
                bool interiorAccent = (ch == 'T' || ch == 'Y') && !band;
                if (!band && !interiorAccent)
                    continue;

                Vector2Int cell = new Vector2Int(c, height - 1 - r);

                // Random gaps so the forest edge is ragged, not a solid wall.
                if (rng.Chance(GapChance))
                {
                    plan.gaps[cell] = rng.Chance(0.5f) ? "Bush" : null;
                    plan.gapCount++;
                    plan.skipped++;
                    continue;
                }

                string name = PickSprite(rng, r, lastSprite, repeatRun, ref bigUsed);
                if (name == null)
                {
                    plan.skipped++;
                    continue;
                }

                Vector2 size = sizeOf(name);
                if (size.x <= 0f || size.y <= 0f)
                {
                    plan.skipped++;
                    continue;
                }

                var tree = new Tree
                {
                    cell = cell,
                    sprite = name,
                    isBig = (name == "Tree_Big"),
                    // Presentation only - collision never sees these.
                    offset = new Vector2(rng.Range(-0.25f, 0.25f), rng.Range(-0.25f, 0.25f)),
                    scale = rng.Range(0.9f, 1.1f),
                    flipX = rng.Chance(0.5f)
                };

                // Stagger alternate rows by half a tile to break the grid.
                if ((r % 2) == 0)
                    tree.offset += new Vector2(0.5f, 0f);

                plan.trees[cell] = tree;
                plan.gaps[cell] = name;
                plan.placed++;
                if (tree.isBig)
                    plan.bigCount++;
            }
        }

        MarkFootprints(plan, sizeOf);
        return plan;
    }

    private static string PickSprite(Rng rng, int row, string[] lastSprite,
                                    int[] repeatRun, ref int bigUsed)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            string name = TreePool[rng.Range(0, TreePool.Length - 1)];

            // Never three identical trees in a row.
            if (lastSprite[row] == name && repeatRun[row] >= 2)
                continue;

            // Tree_Big is a rare set piece, and never two in a row.
            if (name == "Tree_Big")
            {
                if (bigUsed >= MaxTreeBig)
                    continue;
                if (lastSprite[row] == "Tree_Big")
                    continue;
                bigUsed++;
            }

            if (lastSprite[row] == name)
                repeatRun[row]++;
            else
            {
                lastSprite[row] = name;
                repeatRun[row] = 1;
            }
            return name;
        }

        // Fallback: a narrow default rather than nothing.
        const string fallback = "Tree_Small";
        if (lastSprite[row] == fallback && repeatRun[row] >= 2)
            return null;
        lastSprite[row] = fallback;
        repeatRun[row]++;
        return fallback;
    }
    // ------------------------------------------------------------- footprints

    /// <summary>
    /// Split every tree into a blocking base (bottom 40%) and a walkable canopy.
    /// Uses the untransformed sprite rect, so it is independent of the jitter.
    /// </summary>
    private static void MarkFootprints(Plan plan, System.Func<string, Vector2> sizeOf)
    {
        foreach (var kv in plan.trees)
        {
            Vector2Int cell = kv.Key;
            Tree tree = kv.Value;
            Vector2 size = sizeOf(tree.sprite);

            Rect full = SpriteRect(size, cell);
            float baseH = size.y * BaseBlockFraction;
            Rect baseRect = new Rect(full.xMin, full.yMin, full.width, baseH);
            Rect canopyRect = new Rect(full.xMin, full.yMin + baseH,
                                       full.width, full.height - baseH);

            // The trunk cell always blocks, whatever the maths says.
            plan.blocked.Add(cell);

            AddCovered(plan.blocked, baseRect, cell, true);
            AddCovered(plan.canopy, canopyRect, cell, false);
        }
    }

    public static Rect SpriteRect(Vector2 size, Vector2Int cell)
    {
        return new Rect(cell.x + 0.5f - size.x * 0.5f, cell.y, size.x, size.y);
    }

    /// <summary>Adds every cell the rect covers by at least BlockCoverage.</summary>
    private static void AddCovered(HashSet<Vector2Int> set, Rect rect, Vector2Int trunk,
                                   bool skipTrunk)
    {
        int x0 = Mathf.FloorToInt(rect.xMin);
        int x1 = Mathf.CeilToInt(rect.xMax) - 1;
        int y0 = Mathf.FloorToInt(rect.yMin);
        int y1 = Mathf.CeilToInt(rect.yMax) - 1;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (skipTrunk && c == trunk)
                    continue;
                if (Coverage(rect, c) >= BlockCoverage)
                    set.Add(c);
            }
        }
    }

    /// <summary>How much of a unit cell the rect covers, 0..1.</summary>
    public static float Coverage(Rect rect, Vector2Int cell)
    {
        float ox = Mathf.Min(rect.xMax, cell.x + 1f) - Mathf.Max(rect.xMin, cell.x);
        float oy = Mathf.Min(rect.yMax, cell.y + 1f) - Mathf.Max(rect.yMin, cell.y);
        if (ox <= 0f || oy <= 0f)
            return 0f;
        return ox * oy;
    }
}
