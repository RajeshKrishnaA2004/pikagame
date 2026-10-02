using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds and validates the Borrowed Time chapter scenes.
///
/// Separate from <see cref="ProjectSetup"/> on purpose: ProjectSetup owns the
/// retired painted "Overworld" (2 units/tile, zoom, minimap) and MUST NOT be
/// re-run, because it overwrites Overworld.unity. Everything in here works at
/// 1 tile = 1 Unity unit against the hand-made pack in
/// <see cref="PixelArtImporter.PackRoot"/>.
///
/// Headless:
///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
///             -executeMethod ChapterBuilder.BuildStage1Route
///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
///             -executeMethod ChapterBuilder.ValidateStage1
/// </summary>
public static class ChapterBuilder
{
    public const string PackRoot = PixelArtImporter.PackRoot;
    public const string LayoutPath = "Assets/Data/Chapter1_Route.txt";
    public const string RouteScene = "Assets/Scenes/Chapter1_Route.unity";
    public const string TileAssetDir = "Assets/Data/Tiles";

    public const float CellSize = 1f;          // 1 tile = 1 unit (fixed decision)
    public const float CameraOrtho = 4.5f;     // 9 tiles tall

    /// <summary>Visible area in tiles, width : height. 10:9 at ortho 4.5 = 10 x 9 tiles.</summary>
    public const float ViewAspect = 10f / 9f;

    /// <summary>Ground must render behind everything (SpriteYSort starts at 1).</summary>
    public const int GroundSortingOrder = -100;

    /// <summary>
    /// Canopies that overhang walkable ground render above the player (whose
    /// SpriteYSort peaks around 996), so you can walk behind a canopy.
    /// </summary>
    public const int CanopySortingOrder = 4000;

    /// <summary>Ground painted under a tree cell that was skipped by the spacing rule.</summary>
    public const string SkippedTreeTileName = "DarkGrass";

    // Sprites are indexed Down, Left, Right, Up - same order the rest of the
    // project uses for FacingIndex, and the order the PNGs are named in.
    private static readonly string[] DirOrder = { "down", "left", "right", "up" };

    // -------------------------------------------------------------- legend

    /// <summary>Chars that paint a ground tile.</summary>
    private static readonly Dictionary<char, string> GroundTileName =
        new Dictionary<char, string>
        {
            { '.', "Grass" },
            { ',', "TallGrass" },
            { '=', "BrickPath" },
            { 'o', "Flowers" },        // deliberate 1-cell gap in a hedge run
            { '~', "ShallowWater" },
            { '#', "Hedge" },
            { '@', "Grass" },
            { 'G', "Grass" },
            { 'T', "Grass" },
            { 'Y', "Grass" },
            { 'h', "Grass" },
            { 'f', "Grass" },
            { 'm', "Grass" },
            { 'n', "Grass" }
        };

    /// <summary>Chars the player may never occupy.</summary>
    private const string BlockingChars = "~#TYhfmGn";

    /// <summary>Chars rendered as a SpriteRenderer rather than a tile.</summary>
    private const string SpriteChars = "TYhfmGn";

    // -------------------------------------------------------------- layout

    /// <summary>One parsed text map. Row 0 of <see cref="rows"/> is the TOP row.</summary>
    public class TextMap
    {
        public string[] rows;
        public int Width { get { return rows.Length > 0 ? rows[0].Length : 0; } }
        public int Height { get { return rows.Length; } }

        public char At(int col, int row) { return rows[row][col]; }

        public bool InBounds(int col, int row)
        {
            return col >= 0 && row >= 0 && col < Width && row < Height;
        }

        /// <summary>ASCII row/col -> Unity cell (Y flips because ASCII is top-down).</summary>
        public Vector2Int ToCell(int col, int row)
        {
            return new Vector2Int(col, (Height - 1) - row);
        }

        /// <summary>Find the first occurrence of a char.</summary>
        public Vector2Int Find(char c)
        {
            for (int r = 0; r < Height; r++)
                for (int col = 0; col < Width; col++)
                    if (rows[r][col] == c)
                        return ToCell(col, r);
            return new Vector2Int(-1, -1);
        }
    }

    /// <summary>
    /// Read the layout file, dropping "//" comments and blank lines.
    /// Throws with a clear message if rows differ in length - a ragged map
    /// would silently misplace the spawn or the gym door.
    /// </summary>
    public static TextMap LoadLayout(string assetPath)
    {
        string full = Path.Combine(Application.dataPath,
                                   assetPath.Replace("Assets/", "").Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(full))
            throw new FileNotFoundException("Layout not found: " + assetPath, assetPath);

        var lines = new List<string>();
        foreach (string raw in File.ReadAllLines(full))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith("//"))
                continue;
            lines.Add(line);
        }

        if (lines.Count == 0)
            throw new System.Exception("Layout has no data rows: " + assetPath);

        int width = lines[0].Length;
        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i].Length != width)
                throw new System.Exception(string.Format(
                    "Layout row {0} is {1} chars but row 0 is {2} ({3})",
                    i, lines[i].Length, width, assetPath));
        }

        var map = new TextMap { rows = lines.ToArray() };
        return map;
    }

    public static bool IsBlocking(char c)
    {
        return BlockingChars.IndexOf(c) >= 0;
    }

    public static bool IsWalkable(char c)
    {
        return !IsBlocking(c);
    }

    // ---------------------------------------------------------- connectivity

    /// <summary>Locate a char in ASCII (col,row) space. Row 0 is the TOP row.</summary>
    public static bool FindAscii(TextMap m, char c, out int col, out int row)
    {
        for (int r = 0; r < m.Height; r++)
        {
            for (int i = 0; i < m.Width; i++)
            {
                if (m.rows[r][i] == c)
                {
                    col = i;
                    row = r;
                    return true;
                }
            }
        }
        col = -1;
        row = -1;
        return false;
    }

    /// <summary>
    /// BFS over walkable cells from '@' (spawn) to the gym door, which is the
    /// tile directly below 'G' in the layout (one row further down the text).
    ///
    /// Runs in -batchmode with no physics step, so this is the authoritative
    /// proof the level is completable. BFS guarantees the shortest path.
    /// </summary>
    public static bool CanReachGymDoor(TextMap m, out int steps)
    {
        steps = -1;

        int spawnCol, spawnRow, gymCol, gymRow;
        if (!FindAscii(m, '@', out spawnCol, out spawnRow))
        {
            Debug.LogError("[BorrowedTime] layout has no '@' spawn cell");
            return false;
        }
        if (!FindAscii(m, 'G', out gymCol, out gymRow))
        {
            Debug.LogError("[BorrowedTime] layout has no 'G' gym cell");
            return false;
        }

        int doorCol = gymCol;
        int doorRow = gymRow + 1;
        if (doorRow >= m.Height || !IsWalkable(m.At(doorCol, doorRow)))
        {
            Debug.LogError(string.Format(
                "[BorrowedTime] gym door at ({0},{1}) is off-map or blocking - " +
                "'G' needs a walkable tile directly below it", doorCol, doorRow));
            return false;
        }

        int total = m.Width * m.Height;
        var dist = new int[total];
        for (int i = 0; i < total; i++)
            dist[i] = -1;

        int start = spawnRow * m.Width + spawnCol;
        int goal = doorRow * m.Width + doorCol;

        var queue = new Queue<int>();
        dist[start] = 0;
        queue.Enqueue(start);

        int guard = 0;
        while (queue.Count > 0 && guard++ <= total)
        {
            int cur = queue.Dequeue();
            if (cur == goal)
                break;

            int cx = cur % m.Width;
            int cy = cur / m.Width;

            Visit(m, dist, queue, cx - 1, cy, dist[cur] + 1);
            Visit(m, dist, queue, cx + 1, cy, dist[cur] + 1);
            Visit(m, dist, queue, cx, cy - 1, dist[cur] + 1);
            Visit(m, dist, queue, cx, cy + 1, dist[cur] + 1);
        }

        steps = dist[goal];
        return steps >= 0;
    }

    private static void Visit(TextMap m, int[] dist, Queue<int> queue, int col, int row, int d)
    {
        if (!m.InBounds(col, row))
            return;
        if (!IsWalkable(m.At(col, row)))
            return;
        int idx = row * m.Width + col;
        if (dist[idx] >= 0)
            return;
        dist[idx] = d;
        queue.Enqueue(idx);
    }

    // ------------------------------------------------------------- tile assets

    private static void EnsureTileFolder()
    {
        if (!AssetDatabase.IsValidFolder(TileAssetDir))
            AssetDatabase.CreateFolder("Assets/Data", "Tiles");
    }

    /// <summary>
    /// Missing art is collected rather than logged per call, so a broken pack
    /// produces ONE actionable error naming every missing file instead of N
    /// near-identical red lines.
    /// </summary>
    private static readonly List<string> MissingSprites = new List<string>();

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null && !MissingSprites.Contains(path))
            MissingSprites.Add(path);
        return s;
    }

    /// <summary>Log one consolidated error if anything failed to resolve.</summary>
    private static void ReportMissingSprites(string when)
    {
        if (MissingSprites.Count == 0)
            return;

        Debug.LogError(string.Format(
            "[BorrowedTime] {0} could not load {1} sprite(s):\n  - {2}",
            when, MissingSprites.Count, string.Join("\n  - ", MissingSprites.ToArray())));
    }

    /// <summary>
    /// Hard precondition. Every ground tile the layout needs must resolve to a
    /// real Sprite BEFORE any Tile asset is created. If they do not, stop: an
    /// empty Ground tilemap looks like a rendering bug but is really a broken
    /// build, and saving that scene wastes everyone's time.
    /// </summary>
    private static void EnsureArtResolvable()
    {
        string[] required =
        {
            "Grass", "TallGrass", "BrickPath", "ShallowWater", "Hedge",
            SkippedTreeTileName
        };

        MissingSprites.Clear();
        foreach (string name in required)
            LoadSprite(PackRoot + "/Environment/Tiles/" + name + ".png");

        if (MissingSprites.Count == 0)
            return;

        Debug.LogError(string.Format(
            "[BorrowedTime] BUILD ABORTED - {0} ground tile sprite(s) did not resolve:\n  - {1}\n" +
            "Run Tools > BorrowedTime > 1. Import Art Pack (_Mine), wait for the " +
            "import to finish, then rebuild.",
            MissingSprites.Count, string.Join("\n  - ", MissingSprites.ToArray())));

        throw new System.Exception(
            "[BorrowedTime] ground tile sprites unresolvable - import the art pack first");
    }

    /// <summary>
    /// True once '1. Import Art Pack (_Mine)' has run. Used to SKIP rather than
    /// FAIL any check that has to load real sprites.
    /// </summary>
    private static bool SpritesReady()
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(
            PackRoot + "/Environment/Buildings/Gym.png") != null;
    }

    private static Tile MakeTileAsset(string assetPath, Sprite sprite, Tile.ColliderType collider)
    {
        EnsureTileFolder();
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }
        tile.sprite = sprite;
        tile.colliderType = collider;
        tile.color = Color.white;
        tile.transform = Matrix4x4.identity;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    /// <summary>
    /// Ground tiles plus the invisible full-cell blocker tile.
    ///
    /// Two hard-won rules:
    ///  1. Never call AssetDatabase.Refresh() here. Refresh re-imports and
    ///     DESTROYS the Tile objects, so references held afterwards are Unity
    ///     "fake null" and Tilemap.SetTile silently paints nothing.
    ///  2. Keep the LIVE objects returned by MakeTileAsset instead of re-loading
    ///     them from disk, and self-heal if a sprite reference did not stick.
    ///     Re-loading right after creation is unreliable and produced an empty
    ///     Ground tilemap (0 of 552 cells painted).
    /// </summary>
    public static Dictionary<string, Tile> BuildTileAssets()
    {
        EnsureTileFolder();
        MissingSprites.Clear();

        var wanted = new List<string>();
        foreach (var pair in GroundTileName)
        {
            if (!wanted.Contains(pair.Value))
                wanted.Add(pair.Value);
        }
        wanted.Add(SkippedTreeTileName);      // ground for skipped tree cells
        wanted.Add("Blocker");

        // ---- PHASE 1: create/update every asset, keeping NO references.
        // AssetDatabase.CreateAsset triggers a reimport that DESTROYS other
        // loaded asset objects, so a reference captured here can be fake-null
        // moments later. Nothing is stored yet.
        foreach (string name in wanted)
        {
            string spritePath = (name == "Blocker")
                ? PackRoot + "/Environment/Tiles/Grass.png"
                : PackRoot + "/Environment/Tiles/" + name + ".png";

            Sprite s = LoadSprite(spritePath);
            if (s == null)
            {
                Debug.LogError("[BorrowedTime] tile '" + name +
                               "' has no source sprite: " + spritePath);
                continue;
            }

            var collider = (name == "Blocker")
                ? Tile.ColliderType.Grid
                : Tile.ColliderType.None;

            MakeTileAsset(TileAssetDir + "/" + name + ".asset", s, collider);
        }

        AssetDatabase.SaveAssets();

        // ---- PHASE 2: import fully and synchronously, THEN load. This Refresh
        // is safe only because nothing is being held across it.
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate |
                              ImportAssetOptions.ForceSynchronousImport);

        var tiles = new Dictionary<string, Tile>();
        var bad = new List<string>();

        foreach (string name in wanted)
        {
            string path = TileAssetDir + "/" + name + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null || t.sprite == null)
            {
                bad.Add(name + (t == null ? " (no asset)" : " (null sprite)"));
                continue;
            }
            tiles[name] = t;                    // fresh, live reference
        }

        Debug.Log(string.Format(
            "[BorrowedTime] tile assets: {0}/{1} usable{2}",
            tiles.Count, wanted.Count,
            bad.Count == 0 ? "" : " -- unusable: " + string.Join(", ", bad.ToArray())));

        ReportMissingSprites("BuildTileAssets");

        if (bad.Count > 0)
            throw new System.Exception(
                "[BorrowedTime] tile assets unusable (" + string.Join(", ", bad.ToArray()) +
                ") - refusing to save a broken scene");

        return tiles;
    }

    /// <summary>16 walk frames: [direction * 4 + frame], Down/Left/Right/Up.</summary>
    public static Sprite[] LoadPlayerFrames()
    {
        var frames = new Sprite[16];
        for (int d = 0; d < 4; d++)
        {
            for (int f = 0; f < 4; f++)
            {
                frames[d * 4 + f] = LoadSprite(string.Format(
                    "{0}/Player/Player_{1}_{2}.png", PackRoot, DirOrder[d], f));
            }
        }
        return frames;
    }

    // ----------------------------------------------------------------- props

    private static readonly string[] TreeVariants =
        { "Tree_Small", "Tree_Medium", "Tree_Big", "Tree_Autumn" };

    /// <summary>Incremented per NPC so each gets its own name and tint.</summary>
    private static int npcIndex;

    /// <summary>
    /// Sprite for an object char. Trees/houses vary deterministically by cell so
    /// a long hedge of 'T' does not read as one repeated sprite.
    /// </summary>
    private static Sprite PropSprite(char c, int cellX, int cellY)
    {
        string pick;
        switch (c)
        {
            case 'T':
                pick = TreeVariants[Mathf.Abs(cellX * 7 + cellY * 3) % TreeVariants.Length];
                return LoadSprite(PackRoot + "/Environment/Trees/" + pick + ".png");
            case 'Y':
                return LoadSprite(PackRoot + "/Environment/Trees/Pine.png");
            case 'h':
                return LoadSprite(PackRoot + "/Environment/Buildings/House_Small.png");
            case 'f':
                return LoadSprite(PackRoot + "/Environment/Props/Fence.png");
            case 'm':
                return LoadSprite(PackRoot + "/Environment/Props/Mailbox.png");
            case 'G':
                return LoadSprite(PackRoot + "/Environment/Buildings/Gym.png");
            case 'n':
                // No dedicated NPC art in the pack. The gym leader's coat reads
                // as a distinct character, so it cannot be mistaken for a second
                // player the way Player_down_0 was. NpcVisual keeps sprite+tint
                // as Inspector fields so real art drops in without code changes.
                return LoadSprite(PackRoot + "/GymLeader/GymLeader_down_0.png");
            default:
                return null;
        }
    }

// ------------------------------------------------------------------ trees

    /// <summary>Per-glyph PPU: the Trees folder imports at 96, everything else at 64.</summary>
    private static float PPUForGlyph(char ch)
    {
        return (ch == 'T' || ch == 'Y')
            ? PixelArtImporter.TreePixelsPerUnit
            : PixelArtImporter.PixelsPerUnit;
    }

    /// <summary>
    /// World size of a prop sprite from its source texture and the PPU its folder
    /// imports at, so it is correct even before the art has been re-imported.
    /// </summary>
    private static Vector2 PropWorldSize(char ch, Sprite s)
    {
        if (s == null)
            return Vector2.one;
        var tex = s.texture;
        if (tex == null)
            return Vector2.one;
        float ppu = PPUForGlyph(ch);
        return new Vector2(tex.width / ppu, tex.height / ppu);
    }

    /// <summary>Size of a tree sprite by name. Trees always use the 96 PPU.</summary>
    private static Vector2 TreeWorldSize(string name)
    {
        return PropWorldSize('T', LoadSprite(PackRoot + "/Environment/Trees/" + name + ".png"));
    }

    private static Sprite TreeSprite(string name)
    {
        return LoadSprite(PackRoot + "/Environment/Trees/" + name + ".png");
    }

    private static Rect PropRect(char ch, Sprite s, Vector2Int cell)
    {
        Vector2 size = PropWorldSize(ch, s);
        return new Rect(cell.x + 0.5f - size.x * 0.5f, cell.y, size.x, size.y);
    }

    /// <summary>
    /// Run the seeded forest planner over the layout. Pure data - no assets are
    /// held afterwards, so this is safe either side of AssetDatabase work.
    /// </summary>
    private static ForestGen.Plan PlanForest(TextMap map)
    {
        var grid = new char[map.Height][];
        for (int r = 0; r < map.Height; r++)
        {
            grid[r] = new char[map.Width];
            for (int c = 0; c < map.Width; c++)
                grid[r][c] = map.At(c, r);
        }
        return ForestGen.Generate(grid, map.Width, map.Height, TreeWorldSize);
    }

    /// <summary>Cell-based walkability: trunk + bottom 40% block, canopy is walkable.</summary>
    private static bool CellBlocked(TextMap map, int col, int row, ForestGen.Plan forest)
    {
        return ChapterForest.CellBlocked(map, col, row, forest);
    }

    /// <summary>
    /// True when a tree's canopy overhangs ground the player can actually stand
    /// on. Those trees move to the foreground layer so the player walks behind.
    /// </summary>
    private static bool TreeIsForeground(ForestGen.Plan forest, Vector2Int cell,
                                         TextMap map)
    {
        ForestGen.Tree tree;
        if (!forest.trees.TryGetValue(cell, out tree))
            return false;

        Vector2 size = TreeWorldSize(tree.sprite);
        Rect full = ForestGen.SpriteRect(size, cell);
        float baseH = size.y * ForestGen.BaseBlockFraction;
        Rect canopy = new Rect(full.xMin, full.yMin + baseH,
                               full.width, full.height - baseH);

        int x0 = Mathf.FloorToInt(canopy.xMin);
        int x1 = Mathf.CeilToInt(canopy.xMax) - 1;
        int y0 = Mathf.FloorToInt(canopy.yMin);
        int y1 = Mathf.CeilToInt(canopy.yMax) - 1;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (c == cell)
                    continue;
                if (ForestGen.Coverage(canopy, c) < ForestGen.BlockCoverage)
                    continue;

                int row = (map.Height - 1) - y;
                if (x < 0 || x >= map.Width || row < 0 || row >= map.Height)
                    continue;
                if (!CellBlocked(map, x, row, forest))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Places a tree with its visual jitter (offset / scale / flip). None of that
    /// affects collision, which is decided by cell in ChapterForest.
    /// </summary>
    private static void AddTree(Transform parent, Sprite sprite, Vector2Int cell,
                                ForestGen.Tree tree)
    {
        var go = new GameObject("Tree_" + tree.sprite + "_" + cell.x + "_" + cell.y);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipX = tree.flipX;

        // Bottom-centre pivot, then the jitter, so the art is placed by its feet.
        go.transform.localPosition = new Vector3(
            cell.x + 0.5f + tree.offset.x,
            cell.y + tree.offset.y,
            0f);
        go.transform.localScale = new Vector3(tree.scale, tree.scale, 1f);

        if (!tree.foreground)
            go.AddComponent<SpriteYSort>();
        else
            go.AddComponent<ForegroundSort>().Apply();
    }
    // ------------------------------------------------------------ scene build

    [MenuItem("Tools/BorrowedTime/2. Build Chapter1 Route")]
    public static void BuildStage1Route()
    {
        PixelArtImporter.ImportArtPack();

        // Deliberately NO forced reimport here.
        // AssetDatabase.ImportAsset(..., ForceUpdate) destroys every Sprite
        // reference, so the very next LoadAssetAtPath<Sprite> returns null and
        // the Tile assets get baked empty - that is what produced "0 of 552
        // ground cells" while props rendered fine a moment later.
        // ImportArtPack now only reimports textures whose settings really
        // changed, so a second run invalidates nothing.
        //
        // A plain Refresh (no ForceUpdate) lets a first-time import settle
        // without forcing anything to be reimported a second time.
        AssetDatabase.Refresh();
        EnsureArtResolvable();

        TextMap map = LoadLayout(LayoutPath);

        int steps;
        bool reachable = CanReachGymDoor(map, out steps);
        if (!reachable)
        {
            Debug.LogError("[BorrowedTime] layout is not completable - aborting build");
            throw new System.Exception("[BorrowedTime] spawn cannot reach the gym door");
        }

        int spawnCol, spawnRow, gymCol, gymRow;
        FindAscii(map, '@', out spawnCol, out spawnRow);
        FindAscii(map, 'G', out gymCol, out gymRow);
        Vector2Int spawnCell = map.ToCell(spawnCol, spawnRow);
        Vector2Int gymCell = map.ToCell(gymCol, gymRow);
        Vector2Int doorCell = new Vector2Int(gymCell.x, gymCell.y - 1);

        // The scene is created BEFORE the tile assets are loaded. NewScene
        // touches the AssetDatabase internally, which can invalidate asset
        // references held across it - and a fake-null Tile makes SetTile paint
        // nothing at all.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Phase 2 of BuildTileAssets ends with a Refresh + Load. Nothing may run
        // between that and the paint loop below.
        Dictionary<string, Tile> tiles = BuildTileAssets();

        ForestGen.Plan forest = PlanForest(map);

        // The player-facing route: spawn -> each NPC's standing place -> gym door.
        bool routeComplete;
        HashSet<Vector2Int> routeCells =
            ChapterForest.CarveRoute(map, forest, out routeComplete);

        // --------------------------------------------------------- grid + maps
        var gridGo = new GameObject("Grid");
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(CellSize, CellSize, 1f);
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;

        var groundGo = new GameObject("Ground");
        groundGo.transform.SetParent(gridGo.transform, false);
        var ground = groundGo.AddComponent<Tilemap>();
        ground.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var groundR = groundGo.AddComponent<TilemapRenderer>();
        groundR.sortingOrder = GroundSortingOrder;   // behind every prop and the player
        groundR.mode = TilemapRenderer.Mode.Chunk;

        var blockGo = new GameObject("Blockers");
        blockGo.transform.SetParent(gridGo.transform, false);
        var blockers = blockGo.AddComponent<Tilemap>();
        blockers.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var blockR = blockGo.AddComponent<TilemapRenderer>();
        blockR.enabled = false;                 // collider-only, never drawn

        var blockRb = blockGo.AddComponent<Rigidbody2D>();
        blockRb.bodyType = RigidbodyType2D.Static;
        blockRb.gravityScale = 0f;

        // The operation belongs on the source TilemapCollider2D. A
        // CompositeCollider2D is the receiver and cannot itself be composited.
        var tilemapCol = blockGo.AddComponent<TilemapCollider2D>();
        var composite = blockGo.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.edgeRadius = 0f;
        tilemapCol.compositeOperation = Collider2D.CompositeOperation.Merge;

        // ------------------------------------------------------- paint + props
        var propsRoot = new GameObject("Props");
        propsRoot.transform.SetParent(gridGo.transform, false);

        // Canopies that overhang walkable ground draw ABOVE the player, so you
        // walk behind them. Fixed order, no Y-sorting.
        var canopyRoot = new GameObject("Trees_Foreground");
        canopyRoot.transform.SetParent(gridGo.transform, false);
        var canopyOrder = canopyRoot.AddComponent<ForegroundSort>();
        canopyOrder.sortingOrder = CanopySortingOrder;

        int painted = 0, blockedCells = 0, propCount = 0, unresolvedGround = 0;
        Tile blockerTile = tiles.ContainsKey("Blocker") ? tiles["Blocker"] : null;
        npcIndex = 0;

        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                char ch = map.At(c, r);
                // Tilemap APIs take Vector3Int. The layout is 2D, and Unity
                // defines NO conversion operator between Vector2Int and
                // Vector3Int, so this must be built explicitly.
                Vector2Int flat = map.ToCell(c, r);
                Vector3Int cell = new Vector3Int(flat.x, flat.y, 0);

                string tileName = null;
                if (routeCells.Contains(flat))
                {
                    tileName = "BrickPath";          // the obvious walking route
                }
                else if (forest.gaps.ContainsKey(flat))
                {
                    tileName = SkippedTreeTileName;   // ragged gap in the treeline
                }
                else
                {
                    GroundTileName.TryGetValue(ch, out tileName);
                }

                Tile groundTile = null;
                if (tileName != null &&
                    tiles.TryGetValue(tileName, out groundTile) && groundTile != null)
                {
                    ground.SetTile(cell, groundTile);
                    painted++;
                }
                else
                {
                    unresolvedGround++;
                }

                // Cell-based blocking: trunk + bottom 40% only, and the map edge
                // is always sealed. Canopy cells above stay walkable.
                if (CellBlocked(map, c, r, forest) && blockerTile != null)
                {
                    blockers.SetTile(cell, blockerTile);
                    blockedCells++;
                }

                if (ch == 'T' || ch == 'Y')
                {
                    ForestGen.Tree tree;
                    if (forest.trees.TryGetValue(flat, out tree))
                    {
                        Sprite ts = TreeSprite(tree.sprite);
                        if (ts != null)
                        {
                            bool front = TreeIsForeground(forest, flat, map);
                            tree.foreground = front;
                            AddTree(front ? canopyRoot.transform : propsRoot.transform,
                                    ts, flat, tree);
                            propCount++;
                        }
                    }

                    // A gap may still get a bush to break up the edge.
                    string gapSprite;
                    if (forest.gaps.TryGetValue(flat, out gapSprite) && gapSprite != null)
                    {
                        Sprite bs = TreeSprite(gapSprite);
                        if (bs != null)
                        {
                            AddTree(propsRoot.transform, bs, flat, new ForestGen.Tree());
                            propCount++;
                        }
                    }
                }
                else if (SpriteChars.IndexOf(ch) >= 0)
                {
                    Sprite s = PropSprite(ch, cell.x, cell.y);
                    if (s != null)
                    {
                        AddProp(propsRoot.transform, ch, c, r, cell, s);
                        propCount++;
                    }
                }
            }
        }

        ground.CompressBounds();
        blockers.CompressBounds();

        // --------------------------------------------------------- map collision
        var mcGo = new GameObject("MapCollision");
        var collision = mcGo.AddComponent<MapCollision>();
        collision.width = map.Width;
        collision.height = map.Height;
        collision.spawnCell = spawnCell;
        collision.gymDoorCell = doorCell;
        collision.layoutName = "Chapter1_Route";

        // Fill a plain array, then hand it to SetGrid, which also writes the
        // serialised bit STRING. Never assign collision.blocked directly - it is
        // a non-serialised cache and would not survive the scene save.
        bool[] blockedGrid = new bool[map.Width * map.Height];
        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                Vector2Int cell = map.ToCell(c, r);
                blockedGrid[cell.y * map.Width + cell.x] =
                    CellBlocked(map, c, r, forest);
            }
        }
        collision.SetGrid(blockedGrid);

        // FAIL-FAST. If the spawn cell is blocked the player cannot take a single
        // step, which reads as "movement is broken" with no obvious cause. Catch
        // it here, before anything is written to disk.
        Check("spawn cell is walkable (" + spawnCell + ")",
              !collision.IsBlocked(spawnCell));
        Check("spawn cell is walkable in the blockers tilemap",
              blockers.GetTile(new Vector3Int(spawnCell.x, spawnCell.y, 0)) == null);

        // ------------------------------------------------------------- the player
        Sprite[] frames = LoadPlayerFrames();

        var playerGo = new GameObject("Player");
        playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(spawnCell.x + 0.5f, spawnCell.y, 0f);

        var playerSr = playerGo.AddComponent<SpriteRenderer>();
        playerSr.sprite = frames.Length > 0 ? frames[0] : null;
        playerGo.AddComponent<SpriteYSort>();

        // Set here as well as in GridPlayerController.Awake: Awake never runs for
        // a scene sitting on disk, so validation reads the unconfigured body.
        var playerRb = playerGo.AddComponent<Rigidbody2D>();
        playerRb.bodyType = RigidbodyType2D.Kinematic;
        playerRb.gravityScale = 0f;
        playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerRb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        // Trigger, not solid: the walkability grid is authoritative, so physics
        // must never shove the player. It still receives OnTriggerEnter2D, which
        // is how NPCs and the gym door will detect the player later.
        var playerBox = playerGo.AddComponent<BoxCollider2D>();
        playerBox.size = new Vector2(0.7f, 0.7f);
        playerBox.offset = new Vector2(0f, 0.35f);
        playerBox.isTrigger = true;

        var controller = playerGo.AddComponent<GridPlayerController>();
        controller.map = collision;
        controller.stepDuration = 0.16f;
        controller.turnDelay = 0.09f;

        // Confirm talks to / inspects whatever the player faces. Added here so
        // the component is never left unwired by a hand-edited scene.
        var interaction = playerGo.AddComponent<PlayerInteraction>();
        interaction.map = collision;


        var anim = playerGo.AddComponent<GridPlayerAnimator>();
        anim.frames = frames;
        anim.framesPerDirection = 4;
        anim.idleFrame = 0;

        // ------------------------------------------------------------- the camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(spawnCell.x + 0.5f, spawnCell.y, -10f);

        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CameraOrtho;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;      // letterbox bars are black

        var locked = camGo.AddComponent<LockedCamera>();
        locked.target = playerGo.transform;
        locked.orthoSize = CameraOrtho;
        locked.mapBounds = new Rect(0f, 0f, map.Width * CellSize, map.Height * CellSize);
        locked.pixelSnap = true;
        locked.pixelsPerUnit = PixelArtImporter.PixelsPerUnit;
        // 10 x 9 tiles instead of 16 x 9; LockedCamera letterboxes to match.
        locked.tileAspect = ViewAspect;
        locked.letterboxColor = Color.black;
        // NOTE: no CameraRig on purpose - the new scenes have no zoom, no
        // smoothing and no minimap.

        // ------------------------------------------------------------------ verify
        // Truth, not intent: how many cells the tilemaps ACTUALLY hold. Checked
        // BEFORE saving, so a broken build never overwrites a good scene.
        int groundTiles = CountTiles(ground);
        int blockerTiles = CountTiles(blockers);
        int expectedGround = map.Width * map.Height;

        if (groundTiles != expectedGround || blockerTiles != blockedCells)
        {
            Debug.LogError(string.Format(
                "[BorrowedTime] BUILD ABORTED before saving - Ground {0}/{1}, " +
                "Blockers {2}/{3}. Scene NOT written.",
                groundTiles, expectedGround, blockerTiles, blockedCells));
            throw new System.Exception("[BorrowedTime] tilemaps incomplete - scene not saved");
        }

        // ------------------------------------------------------------------ save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, RouteScene);
        SetStage1BuildSettings();
        AssetDatabase.SaveAssets();

        ReportMissingSprites("BuildStage1Route");

        Debug.Log(string.Format(
            "[BorrowedTime] BUILD OK {0}: {1}x{2} map, {3} tiles, {4} blockers, " +
            "{5} props, spawn {6}, gym door {7}, path {8} steps",
            RouteScene, map.Width, map.Height, painted, blockedCells, propCount,
            spawnCell, doorCell, steps));

        Debug.Log(string.Format(
            "[BorrowedTime] tilemap check: Ground {0}/{1} cells, Blockers {2}/{3} cells",
            groundTiles, expectedGround, blockerTiles, blockedCells));

        Debug.Log(string.Format(
            "[BorrowedTime] trees: {0} placed, {1} skipped for spacing, {2} Tree_Big " +
            "(cap {3}); ground cells resolved: {4}/{5}",
            forest.placed, forest.skipped, forest.bigCount, ForestGen.MaxTreeBig,
            painted, expectedGround));
    }

    private static void AddProp(Transform parent, char ch, int col, int row,
                                Vector3Int cell, Sprite sprite)
    {
        // NPCs get a named object and a distinct tint so they cannot be
        // mistaken for the player while they share the stand-in art.
        string name = (ch == 'n')
            ? "NPC_" + (++npcIndex)
            : "Prop_" + ch + "_" + col + "_" + row;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // Bottom-centre pivot: position the object by its base on the cell.
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        go.AddComponent<SpriteYSort>();

        if (ch == 'n')
        {
            var visual = go.AddComponent<NpcVisual>();
            visual.Configure(sprite, NpcTint(npcIndex), "Route NPC " + npcIndex);
        }
    }

    /// <summary>
    /// Distinct tints for the stand-in NPCs, cycling so any number of them stay
    /// visually separable. Replaced the moment real NPC art is assigned.
    /// </summary>
    private static readonly Color[] NpcTints =
    {
        new Color(1.00f, 0.82f, 0.68f),   // warm
        new Color(0.70f, 0.85f, 1.00f),   // cool
        new Color(0.80f, 1.00f, 0.80f),   // green
        new Color(1.00f, 0.85f, 1.00f)    // pink
    };

    private static Color NpcTint(int index)
    {
        if (NpcTints.Length == 0)
            return Color.white;
        return NpcTints[(index - 1) % NpcTints.Length];
    }

    // -------------------------------------------------------- build settings

    /// <summary>
    /// Stage 1 order: MainMenu, Lab, Chapter1_Route (GymInterior / Battle /
    /// Stage1Complete arrive in later steps and are not in the list yet).
    /// Overworld is REMOVED from Build Settings but the scene file is left on
    /// disk untouched, as instructed.
    /// </summary>
    public static void SetStage1BuildSettings()
    {
        var current = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var ordered = new List<EditorBuildSettingsScene>();

        string[] head = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Lab.unity", RouteScene };

        // Always ADD the head scenes, even if they are not in the list yet.
        // (The old version only reordered entries that already existed, so a
        // freshly generated Chapter1_Route would silently never reach Build
        // Settings.)
        foreach (string path in head)
        {
            if (!ordered.Exists(s => s.path == path))
                ordered.Add(new EditorBuildSettingsScene(path, true));
        }

        // Everything else that is not Overworld and not already placed, kept.
        for (int i = 0; i < current.Count; i++)
        {
            string path = current[i].path;
            if (path == "Assets/Scenes/Overworld.unity")
                continue;                                    // removed, not deleted
            if (ordered.Exists(s => s.path == path))
                continue;
            ordered.Add(new EditorBuildSettingsScene(path, current[i].enabled));
        }

        EditorBuildSettings.scenes = ordered.ToArray();
    }

    // ------------------------------------------------------------- validation

    private static int failures;

    /// <summary>Labels of the checks that failed, repeated at the end so the
    /// console summary is self-describing.</summary>
    private static readonly List<string> failureLabels = new List<string>();
    private static int skips;

    private static void Check(string label, bool ok)
    {
        if (!ok)
        {
            failures++;
            failureLabels.Add(label);
        }
        Debug.Log("[BorrowedTime] " + (ok ? "PASS  " : "FAIL  ") + label);
    }

    /// <summary>
    /// A Stage 1 requirement that cannot be asserted yet because the system it
    /// belongs to has not been built in this step. Logged loudly so the gap is
    /// never mistaken for a passing test.
    /// </summary>
    private static void Skip(string label, string why)
    {
        skips++;
        Debug.Log("[BorrowedTime] SKIP  " + label + " -- " + why);
    }

    private static int CountTiles(Tilemap map)
    {
        if (map == null)
            return -1;
        map.CompressBounds();
        int n = 0;
        foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            if (map.HasTile(pos))
                n++;
        return n;
    }

    [MenuItem("Tools/BorrowedTime/3. Validate Stage 1")]
    public static void ValidateStage1()
    {
        failures = 0;
        skips = 0;
        failureLabels.Clear();

        // ------------------------------------------------------------ layout
        TextMap map = null;
        try
        {
            map = LoadLayout(LayoutPath);
            Check("layout " + map.Width + " x " + map.Height + " (expected 24 x 23)",
                  map.Width == 24 && map.Height == 23);
        }
        catch (System.Exception e)
        {
            Check("layout loads: " + e.Message, false);
        }

        if (map == null)
        {
            FinishValidate();
            return;
        }

        // Deterministic, so this reproduces exactly what the builder planned.
        ForestGen.Plan forest = PlanForest(map);
        bool routeComplete;
        HashSet<Vector2Int> routeCells =
            ChapterForest.CarveRoute(map, forest, out routeComplete);

        int steps = 0;
        bool reachable = CanReachGymDoor(map, out steps);
        Check("connected walkable path @ -> gym door (" + steps + " steps)", reachable);

        int spawnCol, spawnRow, gymCol, gymRow;
        FindAscii(map, '@', out spawnCol, out spawnRow);
        FindAscii(map, 'G', out gymCol, out gymRow);

        Check("spawn '@' exists and is walkable",
              FindAscii(map, '@', out spawnCol, out spawnRow) && IsWalkable(map.At(spawnCol, spawnRow)));
        Check("gym 'G' cell is blocking", IsBlocking(map.At(gymCol, gymRow)));
        Check("gym door (one below G) is walkable",
              gymRow + 1 < map.Height && IsWalkable(map.At(gymCol, gymRow + 1)));

        // Tall grass must NOT block (decoration only, no encounters).
        int tallGrass = 0, tallGrassBlocked = 0;
        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                if (map.At(c, r) == ',')
                {
                    tallGrass++;
                    if (IsBlocking(map.At(c, r)))
                        tallGrassBlocked++;
                }
            }
        }
        Check("tall grass present (" + tallGrass + " cells) and walkable",
              tallGrass > 0 && tallGrassBlocked == 0);

        // --------------------------------------------------------- footprints
        // Sprite overlaps / camera reach. Guarded because it loads sprites,
        // which is only possible once the art pack has been imported.
        if (SpritesReady())
        {
            ValidateFootprints(map, forest);
        }
        else
        {
            Skip("building footprint checks",
                 "art pack not imported yet - run '1. Import Art Pack (_Mine)' first");
        }

        // Tree spacing is derived from the layout and PNG dimensions only, so it
        // is always checked - never SKIPped.
        ValidateTrees(map, forest, routeCells, routeComplete);

        // ------------------------------------------------------------- scene
        var scene = EditorSceneManager.OpenScene(RouteScene, OpenSceneMode.Single);
        Check("scene exists: " + RouteScene, scene.IsValid());

        var collision = UnityEngine.Object.FindFirstObjectByType<MapCollision>();
        Check("MapCollision component present", collision != null);

        if (collision != null)
        {
            // Read from the serialised bit STRING, not the cache. This is what
            // the player will actually see at runtime.
            Check("MapCollision bit string survived the scene save (" +
                  (collision.blockedBits != null ? collision.blockedBits.Length : 0) +
                  "/" + (collision.width * collision.height) + " chars)",
                  collision.blockedBits != null
                  && collision.blockedBits.Length == collision.width * collision.height);

            collision.Rebuild();
            Check("MapCollision size matches layout",
                  collision.width == map.Width && collision.height == map.Height);

            Vector2Int spawnCell = map.ToCell(spawnCol, spawnRow);
            Vector2Int gymCell = map.ToCell(gymCol, gymRow);
            Check("MapCollision spawnCell == '@'", collision.spawnCell == spawnCell);
            Check("MapCollision gymDoorCell == below G",
                  collision.gymDoorCell == new Vector2Int(gymCell.x, gymCell.y - 1));

            // Every cell in the built grid must match the legend.
            int mismatches = 0;
            for (int r = 0; r < map.Height; r++)
            {
                for (int c = 0; c < map.Width; c++)
                {
                    Vector2Int cell = map.ToCell(c, r);
                    bool expected = CellBlocked(map, c, r, forest);
                    bool actual = collision.blocked[cell.y * collision.width + cell.x];
                    if (expected != actual)
                        mismatches++;
                }
            }
            Check("walkability grid matches the plan exactly (" + mismatches + " wrong)",
                  mismatches == 0);

            Check("spawn cell is walkable in grid", !collision.IsBlocked(collision.spawnCell));
            Check("gym door cell is walkable in grid", !collision.IsBlocked(collision.gymDoorCell));
        }

        // ------------------------------------------------------------ tilemaps
        var groundGo = GameObject.Find("Ground");
        var blockGo = GameObject.Find("Blockers");
        Check("Ground tilemap present", groundGo != null);
        Check("Blockers tilemap present", blockGo != null);

        int expectedBlocked = 0;
        for (int r = 0; r < map.Height; r++)
            for (int c = 0; c < map.Width; c++)
                if (CellBlocked(map, c, r, forest))
                    expectedBlocked++;

        if (groundGo != null)
        {
            var groundTile = groundGo.GetComponent<Tilemap>();
            int expectedCells = map.Width * map.Height;

            Check("Ground painted EVERY cell (" + expectedCells + ")",
                  CountTiles(groundTile) == expectedCells);

            if (groundTile != null)
            {
                BoundsInt b = groundTile.cellBounds;
                Check("Ground tilemap is not empty", b.size.x > 0 && b.size.y > 0);
                Check("Ground bounds cover the whole map (" + map.Width + " x " + map.Height + ")",
                      b.xMin == 0 && b.yMin == 0 &&
                      b.xMax == map.Width && b.yMax == map.Height);
            }

            // Every ground character in the layout must have resolved to a tile.
            int holes = 0;
            for (int r = 0; r < map.Height; r++)
            {
                for (int c = 0; c < map.Width; c++)
                {
                    Vector3Int at = new Vector3Int(c, (map.Height - 1) - r, 0);
                    if (groundTile == null || !groundTile.HasTile(at))
                        holes++;
                }
            }
            Check("every layout cell resolved to a ground tile (" + holes + " missing)",
                  holes == 0);

            var gr = groundGo.GetComponent<TilemapRenderer>();
            Check("Ground renderer is behind everything (order " + GroundSortingOrder + ")",
                  gr != null && gr.sortingOrder == GroundSortingOrder);
        }

        // FAIL-level, not SKIP: a tile asset with a null sprite is exactly what
        // produced an invisible Ground tilemap before.
        string[] tileNames = { "Grass", "TallGrass", "BrickPath", "ShallowWater",
                               "Hedge", SkippedTreeTileName, "Blocker" };
        foreach (string name in tileNames)
        {
            var t = AssetDatabase.LoadAssetAtPath<Tile>(TileAssetDir + "/" + name + ".asset");
            Check("tile asset '" + name + "' exists and has a sprite",
                  t != null && t.sprite != null);
        }

        if (blockGo != null)
        {
            Check("Blockers painted blocking cells (" + expectedBlocked + ")",
                  CountTiles(blockGo.GetComponent<Tilemap>()) == expectedBlocked);

            var br = blockGo.GetComponent<TilemapRenderer>();
            Check("Blockers renderer disabled (invisible)", br != null && !br.enabled);

            var tcol = blockGo.GetComponent<TilemapCollider2D>();
            Check("TilemapCollider2D feeds CompositeCollider2D",
                  tcol != null &&
                  tcol.compositeOperation != Collider2D.CompositeOperation.None &&
                  blockGo.GetComponent<CompositeCollider2D>() != null);

            var brb = blockGo.GetComponent<Rigidbody2D>();
            Check("Blockers body static", brb != null && brb.bodyType == RigidbodyType2D.Static);
        }

        var gridObj = GameObject.Find("Grid");
        var gridComp = gridObj != null ? gridObj.GetComponent<Grid>() : null;
        Check("Grid cellSize is 1x1 (1 tile = 1 unit)",
              gridComp != null && Mathf.Approximately(gridComp.cellSize.x, 1f) &&
              Mathf.Approximately(gridComp.cellSize.y, 1f));

        // -------------------------------------------------------------- player
        var player = GameObject.Find("Player");
        Check("Player exists", player != null);

        if (player != null)
        {
            Check("Player tag", player.CompareTag("Player"));
            Check("GridPlayerController attached", player.GetComponent<GridPlayerController>() != null);

            var anim = player.GetComponent<GridPlayerAnimator>();
            Check("GridPlayerAnimator has 16 non-null frames",
                  anim != null && anim.frames != null && anim.frames.Length == 16 &&
                  System.Array.TrueForAll(anim.frames, s => s != null));

            var prb = player.GetComponent<Rigidbody2D>();
            Check("Player body kinematic / no gravity / rotation frozen",
                  prb != null && prb.bodyType == RigidbodyType2D.Kinematic &&
                  prb.gravityScale == 0f &&
                  prb.constraints == RigidbodyConstraints2D.FreezeRotation);

            var pbox = player.GetComponent<BoxCollider2D>();
            Check("Player trigger box (grid is authoritative)", pbox != null && pbox.isTrigger);
            Check("player uses SpriteYSort", player.GetComponent<SpriteYSort>() != null);
        }

        // -------------------------------------------------------------- camera
        var camGo = GameObject.FindWithTag("MainCamera");
        Check("Main Camera exists", camGo != null);

        if (camGo != null)
        {
            var locked = camGo.GetComponent<LockedCamera>();
            Check("LockedCamera attached (no CameraRig)",
                  locked != null && camGo.GetComponent<CameraRig>() == null);

            var cam = camGo.GetComponent<Camera>();
            Check("camera orthographic size 4.5 (9 tiles tall)",
                  cam != null && cam.orthographic && Mathf.Approximately(cam.orthographicSize, CameraOrtho));
            Check("camera background is black (letterbox)",
                  cam != null && cam.clearFlags == CameraClearFlags.SolidColor &&
                  cam.backgroundColor == Color.black);

            if (locked != null)
            {
                Check("camera follows the player",
                      player != null && locked.target == player.transform);
                Check("camera locked to map bounds",
                      Mathf.Approximately(locked.mapBounds.width, map.Width * CellSize) &&
                      Mathf.Approximately(locked.mapBounds.height, map.Height * CellSize));
                Check("camera pixel-snapped at PPU 64",
                      locked.pixelSnap &&
                      Mathf.Approximately(locked.pixelsPerUnit, PixelArtImporter.PixelsPerUnit));
                Check("camera letterboxed to 10:9 (10 x 9 tiles, not 16 x 9)",
                      Mathf.Approximately(locked.tileAspect, ViewAspect));
                Check("letterbox colour is black",
                      locked.letterboxColor.r <= 0.001f && locked.letterboxColor.g <= 0.001f &&
                      locked.letterboxColor.b <= 0.001f);
            }
        }

        // ------------------------------------------------------------- sorting
        var sorters = UnityEngine.Object.FindObjectsByType<SpriteYSort>(FindObjectsSortMode.None);
        int worst = int.MaxValue;
        for (int i = 0; i < sorters.Length; i++)
        {
            worst = Mathf.Min(worst, sorters[i].baseOrder -
                                    Mathf.CeilToInt(map.Height * CellSize * sorters[i].unitsPerStep));
        }
        Check("worst-case Y-sort order (" + worst + ") stays above terrain (0)", worst > 0);

        // ------------------------------------------------------------- imports
        CheckTileImport("Grass");
        CheckTileImport("TallGrass");
        CheckTileImport("BrickPath");
        CheckTileImport("ShallowWater");
        CheckTileImport("Hedge");

        // ------------------------------------------------------- build settings
        string[] wanted =
        {
            "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Lab.unity", RouteScene
        };

        var scenePaths = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            scenePaths.Add(s.path);

        bool order = true;
        int searchFrom = 0;
        foreach (string want in wanted)
        {
            int at = scenePaths.IndexOf(want, searchFrom);
            if (at < 0)
            {
                order = false;
                break;
            }
            searchFrom = at + 1;
        }

        Check("build settings order: MainMenu, Lab, Chapter1_Route", order);
        Check("Overworld removed from Build Settings",
              !scenePaths.Contains("Assets/Scenes/Overworld.unity"));
        Check("Overworld.unity still on disk (not deleted)",
              System.IO.File.Exists("Assets/Scenes/Overworld.unity"));

        // ------------------------------------------ Stage 1 rules NOT yet built
        Skip("Last Ember has its own party slot",
             "PartyManager is a later step; GameState still stores bare CreatureType values");
        Skip("party max is 6", "PartyManager is a later step");
        Skip("Rowan has exactly 3 creatures", "CreatureData / ChapterData are a later step");
        Skip("type chart is a clean 5-cycle", "TypeChart is a later step");
        Skip("Rowan is Stone and the Leaf starter beats him", "Creature data is a later step");
        Skip("build settings include GymInterior, Battle, Stage1Complete",
             "those scenes do not exist yet");

        FinishValidate();
    }

    /// <summary>PPU 64, Point filtering, no mipmaps - the pixel-art contract.</summary>
    private static void CheckTileImport(string tileName)
    {
        string path = PackRoot + "/Environment/Tiles/" + tileName + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Check("import settings for " + tileName + ".png", false);
            return;
        }

        Check(tileName + ": PPU 64 / Point / no mipmaps / uncompressed",
              Mathf.Approximately(importer.spritePixelsPerUnit, PixelArtImporter.PixelsPerUnit) &&
              importer.filterMode == FilterMode.Point &&
              !importer.mipmapEnabled &&
              importer.textureCompression == TextureImporterCompression.Uncompressed);
    }

    // ------------------------------------------------------------- footprints

    /// <summary>
    /// A graze smaller than this is not a build failure: at PPU 64 it is under
    /// 10 px, and because the two sprites sit at different base Y they still
    /// sort correctly - no z-fighting and nothing visible. The real defect is
    /// an object covering a building's face by a visible amount.
    /// </summary>
    private const float OverlapTolerance = 0.15f;

    private class Footprint
    {
        public char glyph;
        public int col, row;
        public Vector2Int cell;
        public Rect rect;
        public float baseY;
        public string spriteName;

        public bool IsBuilding { get { return glyph == 'h' || glyph == 'G'; } }
    }

    /// <summary>
    /// Bottom-centre-anchored world rect for every blocking object. T/Y cells use
    /// the tree PLAN, so a skipped cell contributes no rect and every placed tree
    /// contributes the sprite it was actually given.
    /// </summary>
    private static List<Footprint> BuildFootprints(TextMap map, ForestGen.Plan forest)
    {
        var list = new List<Footprint>();

        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                char ch = map.At(c, r);
                if (!IsBlocking(ch))
                    continue;

                Vector2Int cell = map.ToCell(c, r);

                Sprite s;
                if (ch == 'T' || ch == 'Y')
                {
                    ForestGen.Tree ftree;
                    s = forest.trees.TryGetValue(cell, out ftree)
                        ? TreeSprite(ftree.sprite)
                        : null;              // a gap: walkable, no sprite
                }
                else
                {
                    s = (ch == '#')
                        ? LoadSprite(PackRoot + "/Environment/Tiles/Hedge.png")
                        : PropSprite(ch, cell.x, cell.y);
                }

                if (s == null)
                    continue;

                Vector2 size = PropWorldSize(ch, s);
                list.Add(new Footprint
                {
                    glyph = ch,
                    col = c,
                    row = r,
                    cell = cell,
                    baseY = cell.y,
                    spriteName = System.IO.Path.GetFileNameWithoutExtension(
                        AssetDatabase.GetAssetPath(s)),
                    rect = new Rect(cell.x + 0.5f - size.x * 0.5f, cell.y, size.x, size.y)
                });
            }
        }
        return list;
    }

    private static bool Overlaps(Rect a, Rect b, float tol)
    {
        return a.xMin < b.xMax - tol && b.xMin < a.xMax - tol &&
               a.yMin < b.yMax - tol && b.yMin < a.yMax - tol;
    }

    private static void ValidateFootprints(TextMap map, ForestGen.Plan forest)
    {
        List<Footprint> fps = BuildFootprints(map, forest);
        float mapW = map.Width * CellSize;
        float mapH = map.Height * CellSize;

        int buildings = 0;
        foreach (Footprint f in fps)
            if (f.IsBuilding)
                buildings++;
        Check("found building footprints (" + buildings + ")", buildings > 0);

        // 1. buildings must sit fully inside the map rectangle
        foreach (Footprint f in fps)
        {
            if (!f.IsBuilding)
                continue;
            Check("building " + f.spriteName + " (" + f.col + "," + f.row + ") inside map bounds",
                  f.rect.xMin >= 0f && f.rect.xMax <= mapW &&
                  f.rect.yMin >= 0f && f.rect.yMax <= mapH);
        }

        // 2. buildings must be fully visible. The camera clamps to the map and
        //    centres on the player, so the highest point it can EVER show is the
        //    highest standable cell plus half the view height.
        int highestY = -1;
        for (int r = 0; r < map.Height; r++)
            for (int c = 0; c < map.Width; c++)
                if (IsWalkable(map.At(c, r)))
                    highestY = Mathf.Max(highestY, map.ToCell(c, r).y);
        float viewTop = highestY + CameraOrtho;

        foreach (Footprint f in fps)
        {
            if (!f.IsBuilding)
                continue;
            Check("building " + f.spriteName + " fully visible (top " +
                  f.rect.yMax.ToString("0.00") + " <= view " + viewTop.ToString("0.00") + ")",
                  f.rect.yMax <= viewTop + 0.001f);
        }

        // The clearing two cells above the gym door must show the WHOLE gym, which is
        // what the player sees on arrival.
        int gymC, gymR;
        if (FindAscii(map, 'G', out gymC, out gymR))
        {
            int doorY = map.ToCell(gymC, gymR).y - 1;
            float fromDoor = (doorY + 2) + CameraOrtho;
            foreach (Footprint f in fps)
            {
                if (f.glyph != 'G')
                    continue;
                Check("gym fully visible from the clearing above its door (top " +
                      f.rect.yMax.ToString("0.00") + " <= view " + fromDoor.ToString("0.00") + ")",
                      f.rect.yMax <= fromDoor + 0.001f);
            }
        }

        // 3. nothing may render IN FRONT of a building and cover its face.
        //    Objects at a HIGHER base Y are behind it - that is fine, and is how
        //    a building standing against the treeline is supposed to look.
        int conflicts = 0;
        foreach (Footprint b in fps)
        {
            if (!b.IsBuilding)
                continue;

            foreach (Footprint o in fps)
            {
                if (ReferenceEquals(o, b))
                    continue;
                if (o.IsBuilding)
                    continue;                       // building/building below
                if (!Overlaps(b.rect, o.rect, OverlapTolerance))
                    continue;
                if (o.baseY > b.baseY + 0.001f)
                    continue;                       // behind the building: fine

                conflicts++;
                Debug.Log(string.Format(
                    "[BorrowedTime]   in front: {0}({1},{2}) is covered by '{3}' {4}({5},{6})",
                    b.spriteName, b.col, b.row, o.glyph, o.spriteName, o.col, o.row));
            }
        }

        foreach (Footprint a in fps)
        {
            if (!a.IsBuilding)
                continue;
            foreach (Footprint b in fps)
            {
                if (!b.IsBuilding || ReferenceEquals(a, b))
                    continue;
                if (b.baseY <= a.baseY + 0.001f)
                    continue;
                if (!Overlaps(a.rect, b.rect, OverlapTolerance))
                    continue;
                conflicts++;
                Debug.Log(string.Format(
                    "[BorrowedTime]   building clash: {0}({1},{2}) x {3}({4},{5})",
                    a.spriteName, a.col, a.row, b.spriteName, b.col, b.row));
            }
        }

        Check("no blocking object renders in front of a building (" + conflicts + ")",
              conflicts == 0);
    }

    private static void ValidateTrees(TextMap map, ForestGen.Plan forest,
                                       HashSet<Vector2Int> routeCells, bool routeComplete)
    {
        // --- counts, always printed -------------------------------------
        Debug.Log(string.Format(
            "[BorrowedTime] trees: {0} placed, {1} skipped, {2} Tree_Big (cap {3}); " +
            "{4} canopy cells walkable",
            forest.placed, forest.skipped, forest.bigCount, ForestGen.MaxTreeBig,
            forest.canopy.Count));

        Check("trees were placed (" + forest.placed + ")", forest.placed > 0);
        Check("Tree_Big is rare (" + forest.bigCount + " <= " + ForestGen.MaxTreeBig + ")",
              forest.bigCount <= ForestGen.MaxTreeBig);

        // --- 1. every walkable cell reachable from spawn -----------------
        List<Vector2Int> pockets = ChapterForest.UnreachablePockets(map, forest);
        Check("every walkable cell is reachable from spawn (" + pockets.Count + " pocket(s))",
              pockets.Count == 0);
        for (int i = 0; i < pockets.Count && i < 8; i++)
            Debug.Log("[BorrowedTime]   pocket at cell " + pockets[i]);

        // --- 2. route to each NPC and the gym door ----------------------
        Check("route reached every NPC and the gym door", routeComplete);
        Check("BrickPath route has cells (" + routeCells.Count + ")", routeCells.Count > 0);

        int npcs = 0;
        for (int r = 0; r < map.Height; r++)
            for (int c = 0; c < map.Width; c++)
                if (map.At(c, r) == 'n')
                    npcs++;
        Check("route reaches every NPC (" + npcs + ")", npcs > 0);

        // --- 3. no walkable cell inside a blocking base footprint -------
        int leaks = 0;
        foreach (var kv in forest.trees)
        {
            Vector2Int cell = kv.Key;
            Vector2 size = TreeWorldSize(kv.Value.sprite);
            Rect full = ForestGen.SpriteRect(size, cell);
            float baseH = size.y * ForestGen.BaseBlockFraction;
            Rect baseRect = new Rect(full.xMin, full.yMin, full.width, baseH);

            int x0 = Mathf.FloorToInt(baseRect.xMin), x1 = Mathf.CeilToInt(baseRect.xMax) - 1;
            int y0 = Mathf.FloorToInt(baseRect.yMin), y1 = Mathf.CeilToInt(baseRect.yMax) - 1;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2Int c2 = new Vector2Int(x, y);
                    int row = (map.Height - 1) - y;
                    if (x < 0 || x >= map.Width || row < 0 || row >= map.Height)
                        continue;
                    if (ForestGen.Coverage(baseRect, c2) < ForestGen.BlockCoverage)
                        continue;
                    // The spawn is an intentional override of the tree footprint.
                    if (ChapterForest.IsSpawnCell(map, x, row))
                        continue;
                    if (!CellBlocked(map, x, row, forest))
                    {
                        leaks++;
                        if (leaks <= 5)
                            Debug.Log("[BorrowedTime]   walkable cell " + c2 +
                                      " sits under a tree trunk");
                    }
                }
            }
        }
        Check("no walkable cell lies under a blocking tree trunk (" + leaks + ")",
              leaks == 0);

        // --- 4. map edge sealed, even where trees were skipped ----------
        int escapes = 0;
        for (int c = 0; c < map.Width; c++)
        {
            if (!ChapterForest.IsSpawnCell(map, c, 0) && !CellBlocked(map, c, 0, forest))
                escapes++;
            if (!ChapterForest.IsSpawnCell(map, c, map.Height - 1)
                && !CellBlocked(map, c, map.Height - 1, forest))
                escapes++;
        }
        for (int r = 0; r < map.Height; r++)
        {
            if (!ChapterForest.IsSpawnCell(map, 0, r) && !CellBlocked(map, 0, r, forest))
                escapes++;
            if (!ChapterForest.IsSpawnCell(map, map.Width - 1, r)
                && !CellBlocked(map, map.Width - 1, r, forest))
                escapes++;
        }
        Check("map edge is sealed (" + escapes + " holes)", escapes == 0);

        // The one thing that must never be blocked, or the player cannot move.
        int sc, sr;
        if (FindAscii(map, '@', out sc, out sr))
        {
            Check("spawn cell is walkable (" + map.ToCell(sc, sr) + ")",
                  !CellBlocked(map, sc, sr, forest));
        }
        else
        {
            Check("layout contains a spawn '@'", false);
        }
    }

    private static void FinishValidate()
    {
        Debug.Log("[BorrowedTime] " + failures + " failure(s), " + skips + " skipped");

        if (failures > 0)
        {
            // Repeat every failure here. The per-check lines scroll past fast, and
            // a bare "7 validation failure(s)" exception tells you nothing about
            // WHICH seven - which has wasted more than one build cycle.
            Debug.LogError("[BorrowedTime] ===== THE FAILING CHECKS =====");
            for (int i = 0; i < failureLabels.Count; i++)
                Debug.LogError("[BorrowedTime]   FAIL " + (i + 1) + ": " + failureLabels[i]);
            Debug.LogError("[BorrowedTime] ================================");

            throw new System.Exception("[BorrowedTime] " + failures + " validation failure(s)");
        }

        Debug.Log("[BorrowedTime] VALIDATE OK");
    }
﻿    /// <summary>
    /// Scene-building menu items must never run inside Play mode.
    /// EditorSceneManager.NewScene throws "This cannot be used during play mode",
    /// which aborts the build before it writes anything - leaving the OLD scene on
    /// disk and looking like the fix did not work. Fail loudly instead.
    /// </summary>
    private static bool RequireEditMode(string what)
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[BorrowedTime] " + what
                           + " cannot run in Play mode. Press the Play button to stop"
                           + " (or Ctrl+P on Windows), then run it again.");
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- main menu

    public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    public const string LabScenePath = "Assets/Scenes/Lab.unity";
    public const string RouteScenePath = "Assets/Scenes/Chapter1_Route.unity";

    [MenuItem("Tools/BorrowedTime/4. Build MainMenu")]
    public static void BuildMainMenuScene()
    {
        PixelArtImporter.ImportArtPack();

        if (!RequireEditMode("BuildStage1Route"))
            return;

        if (!RequireEditMode("BuildMainMenuScene"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(384f, 216f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ------------------------------------------------------- background
        // BattleBackground dimmed to ~30% so the menu text stays readable.
        Sprite bg = LoadSprite(PackRoot + "/Backgrounds/BattleBackground.png");
        var bgRt = MakeRect("BattleBackground", canvasGo.transform);
        Stretch(bgRt);
        if (bg != null)
        {
            var img = bgRt.gameObject.AddComponent<Image>();
            img.sprite = bg;
            img.color = new Color(1f, 1f, 1f, 0.30f);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
        }

        // ---------------------------------------------------------- headings
        MakeLabel("Title", canvasGo.transform, "BORROWED TIME", 22f,
                  new Vector2(0f, 66f));
        MakeLabel("Subtitle", canvasGo.transform, "everything is temporary", 8f,
                  new Vector2(0f, 50f));

        // -------------------------------------------------------- menu panel
        var panelRt = MakeRect("MenuPanel", canvasGo.transform);
        panelRt.sizeDelta = new Vector2(96f, 62f);
        Sprite panelSprite = LoadSprite(PackRoot + "/UI/MenuPanel.png");
        if (panelSprite != null)
        {
            var pimg = panelRt.gameObject.AddComponent<Image>();
            pimg.sprite = panelSprite;
            pimg.type = Image.Type.Sliced;
        }

        var menuRt = MakeRect("Menu", panelRt);
        menuRt.sizeDelta = new Vector2(80f, 46f);

        var items = new List<RectTransform>();
        string[] labels = { "PLAY", "CONTINUE", "QUIT" };
        for (int i = 0; i < labels.Length; i++)
        {
            var label = MakeLabel("Item_" + labels[i], menuRt, labels[i], 9f,
                                  new Vector2(6f, 14f - i * 14f));
            label.rectTransform.sizeDelta = new Vector2(72f, 12f);
            items.Add(label.rectTransform);
        }

        // The selector is a chevron-ish glyph; reuse the diamond as a stand-in.
        Sprite arrowSprite = LoadSprite(PackRoot + "/UI/StatusBar_Diamond.png");
        var selectorRt = MakeRect("Selector", menuRt);
        selectorRt.sizeDelta = new Vector2(8f, 8f);
        if (arrowSprite != null)
        {
            var simg = selectorRt.gameObject.AddComponent<Image>();
            simg.sprite = arrowSprite;
            simg.color = new Color(1f, 1f, 1f, 0.9f);
        }

        // ---------------------------------------------------------- fade veil
        var fadeRt = MakeRect("Fade", canvasGo.transform);
        Stretch(fadeRt);
        fadeRt.SetAsLastSibling();
        var fadeImg = fadeRt.gameObject.AddComponent<Image>();
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = false;
        var fadeGroup = fadeRt.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;

        // -------------------------------------------------------- controller
        var ctrl = canvasGo.AddComponent<MainMenuController>();
        ctrl.items = items.ToArray();
        ctrl.selector = selectorRt;
        ctrl.fadeGroup = fadeGroup;

        ApplyBuildSettings();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),
                                    MainMenuScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log("[BorrowedTime] BUILD OK " + MainMenuScenePath
                  + ": menu PLAY/CONTINUE/QUIT, background dimmed to 30%");
        Debug.Log("[BorrowedTime] build settings order: MainMenu, Lab, Chapter1_Route");

        // Silent null art is the usual way a generated UI goes wrong, so say so.
        if (bg == null || panelSprite == null || arrowSprite == null)
            Debug.LogError("[BorrowedTime] MainMenu built with MISSING art: bg="
                           + (bg != null) + " panel=" + (panelSprite != null)
                           + " arrow=" + (arrowSprite != null)
                           + " - re-run the art importer and rebuild.");
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(100f, 20f);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static TMP_Text MakeLabel(string name, Transform parent, string text,
                                      float size, Vector2 pos)
    {
        var rt = MakeRect(name, parent);
        rt.anchoredPosition = pos;

        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// Build Settings order drives what Play starts in and what the fade targets
    /// by index. MainMenu must be 0 or the game boots straight into a save.
    ///
    /// Overworld is deliberately dropped from the list (not deleted) - it is the
    /// retired legacy scene and must not be reachable from the new flow.
    /// </summary>
    private static void ApplyBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
        list.Add(new EditorBuildSettingsScene(LabScenePath, true));
        list.Add(new EditorBuildSettingsScene(RouteScenePath, true));

        EditorBuildSettings.scenes = list.ToArray();
    }

    [MenuItem("Tools/BorrowedTime/5. Validate Step 2")]
    public static void ValidateStep2()
    {
        var scenes = EditorBuildSettings.scenes;
        Check("Build Settings has 3 scenes", scenes.Length == 3);
        if (scenes.Length == 3)
        {
            Check("scene 0 is MainMenu", scenes[0].path == MainMenuScenePath);
            Check("scene 1 is Lab", scenes[1].path == LabScenePath);
            Check("scene 2 is Chapter1_Route", scenes[2].path == RouteScenePath);
        }

        bool overworld = false;
        for (int i = 0; i < scenes.Length; i++)
            if (scenes[i].path.Contains("Overworld"))
                overworld = true;
        Check("Overworld is not in Build Settings", !overworld);
        Check("Overworld.unity still exists on disk (not deleted)",
              AssetDatabase.LoadAssetAtPath<Object>("Assets/Scenes/Overworld.unity") != null);

        // ---- save round trip -------------------------------------------------
        // NOTE: this deletes any real save. It is a validation pass.
        GameSave.Delete();
        Check("no save after Delete", !GameSave.Exists());
        Check("Load() returns null when absent", GameSave.Load() == null);

        var data = new GameSaveData();
        data.starterId = "ember";
        data.party.Add("ember");
        data.lastEmberGranted = true;

        Check("new save defaults: upgradesLeft 5", data.upgradesLeft == 5);
        Check("new save defaults: chapterIndex 0", data.chapterIndex == 0);

        Check("Save() succeeds", GameSave.Save(data));
        Check("Exists() after Save", GameSave.Exists());
        Check("save path is under persistentDataPath",
              GameSave.FilePath.StartsWith(Application.persistentDataPath));
        Check("save path is not PlayerPrefs", GameSave.FilePath.EndsWith(".json"));

        GameSaveData back = GameSave.Load();
        Check("Load() returns data", back != null);
        if (back != null)
        {
            Check("starterId round-trips", back.starterId == "ember");
            Check("party round-trips", back.party.Count == 1 && back.party[0] == "ember");
            Check("lastEmberGranted round-trips", back.lastEmberGranted);
            Check("upgradesLeft round-trips (5)", back.upgradesLeft == 5);
            Check("chapterIndex round-trips (0)", back.chapterIndex == 0);
        }

        Check("Delete() removes the file", GameSave.Delete());
        Check("no save after Delete", !GameSave.Exists());

        // ---- MainMenu wiring ------------------------------------------------
        MainMenuController present =
            UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (present == null)
        {
            skips++;
            Debug.Log("[BorrowedTime] SKIP  MainMenuController checks"
                      + " (open Assets/Scenes/MainMenu.unity to run these)");
        }
        else
        {
            Check("MainMenuController has 3 items", present.items.Length == 3);
            Check("selector assigned", present.selector != null);
            Check("fade group assigned", present.fadeGroup != null);
        }

        FinishValidate();
    }
}
