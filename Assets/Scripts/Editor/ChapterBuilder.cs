using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
            Debug.LogError("[BorrowedTime] missing sprite: " + path);
        return s;
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

    /// <summary>Ground tiles plus the invisible full-cell blocker tile.</summary>
    public static Dictionary<string, Tile> BuildTileAssets()
    {
        EnsureTileFolder();
        var tiles = new Dictionary<string, Tile>();
        var wanted = new List<string>();

        foreach (var pair in GroundTileName)
        {
            if (!wanted.Contains(pair.Value))
                wanted.Add(pair.Value);
        }

        foreach (string name in wanted)
        {
            Sprite s = LoadSprite(PackRoot + "/Environment/Tiles/" + name + ".png");
            if (s == null)
                continue;
            tiles[name] = MakeTileAsset(
                TileAssetDir + "/" + name + ".asset", s, Tile.ColliderType.None);
        }

        // Blocker uses ColliderType.Grid: a solid square taken from the cell
        // bounds, so collision does not depend on the art's outline.
        Sprite blockerSprite = LoadSprite(PackRoot + "/Environment/Tiles/Grass.png");
        tiles["Blocker"] = MakeTileAsset(
            TileAssetDir + "/Blocker.asset", blockerSprite, Tile.ColliderType.Grid);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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
                pick = ((cellX + cellY) % 2 == 0) ? "House_Small" : "House_Large";
                return LoadSprite(PackRoot + "/Environment/Buildings/" + pick + ".png");
            case 'f':
                return LoadSprite(PackRoot + "/Environment/Props/Fence.png");
            case 'm':
                return LoadSprite(PackRoot + "/Environment/Props/Mailbox.png");
            case 'G':
                return LoadSprite(PackRoot + "/Environment/Buildings/Gym.png");
            case 'n':
                // NO dedicated NPC art exists in the pack yet. Standing in with a
                // player frame so the route reads correctly; flagged by
                // ValidateStage1 until real NPC art lands.
                return LoadSprite(PackRoot + "/Player/Player_down_0.png");
            default:
                return null;
        }
    }

    // ------------------------------------------------------------ scene build

    [MenuItem("Tools/BorrowedTime/2. Build Chapter1 Route")]
    public static void BuildStage1Route()
    {
        PixelArtImporter.ImportArtPack();

        TextMap map = LoadLayout(LayoutPath);

        int steps;
        bool reachable = CanReachGymDoor(map, out steps);
        if (!reachable)
        {
            Debug.LogError("[BorrowedTime] layout is not completable - aborting build");
            throw new System.Exception("[BorrowedTime] spawn cannot reach the gym door");
        }

        Dictionary<string, Tile> tiles = BuildTileAssets();

        int spawnCol, spawnRow, gymCol, gymRow;
        FindAscii(map, '@', out spawnCol, out spawnRow);
        FindAscii(map, 'G', out gymCol, out gymRow);
        Vector2Int spawnCell = map.ToCell(spawnCol, spawnRow);
        Vector2Int gymCell = map.ToCell(gymCol, gymRow);
        Vector2Int doorCell = new Vector2Int(gymCell.x, gymCell.y - 1);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
        groundR.sortingOrder = 0;
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

        var composite = blockGo.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.usedByComposite = false;
        composite.edgeRadius = 0f;

        var tilemapCol = blockGo.AddComponent<TilemapCollider2D>();
        tilemapCol.usedByComposite = true;

        // ------------------------------------------------------- paint + props
        var propsRoot = new GameObject("Props");
        propsRoot.transform.SetParent(gridGo.transform, false);

        int painted = 0, blockedCells = 0, propCount = 0;
        Tile blockerTile = tiles.ContainsKey("Blocker") ? tiles["Blocker"] : null;

        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                char ch = map.At(c, r);
                Vector3Int cell = map.ToCell(c, r);

                string tileName;
                if (GroundTileName.TryGetValue(ch, out tileName) && tiles.ContainsKey(tileName))
                    ground.SetTile(cell, tiles[tileName]);
                painted++;

                if (IsBlocking(ch))
                {
                    if (blockerTile != null)
                        blockers.SetTile(cell, blockerTile);
                    blockedCells++;
                }

                if (SpriteChars.IndexOf(ch) >= 0)
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
        collision.blocked = new bool[map.Width * map.Height];
        collision.spawnCell = spawnCell;
        collision.gymDoorCell = doorCell;
        collision.layoutName = "Chapter1_Route";

        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                Vector3Int cell = map.ToCell(c, r);
                collision.blocked[cell.y * collision.width + cell.x] = IsBlocking(map.At(c, r));
            }
        }

        // ------------------------------------------------------------- the player
        Sprite[] frames = LoadPlayerFrames();

        var playerGo = new GameObject("Player");
        playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(spawnCell.x + 0.5f, spawnCell.y, 0f);

        var playerSr = playerGo.AddComponent<SpriteRenderer>();
        playerSr.sprite = frames.Length > 0 ? frames[0] : null;
        playerGo.AddComponent<SpriteYSort>();

        var playerRb = playerGo.AddComponent<Rigidbody2D>();
        playerRb.bodyType = RigidbodyType2D.Kinematic;
        playerRb.gravityScale = 0f;
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

        var locked = camGo.AddComponent<LockedCamera>();
        locked.target = playerGo.transform;
        locked.orthoSize = CameraOrtho;
        locked.mapBounds = new Rect(0f, 0f, map.Width * CellSize, map.Height * CellSize);
        locked.pixelSnap = true;
        locked.pixelsPerUnit = PixelArtImporter.PixelsPerUnit;
        // NOTE: no CameraRig on purpose - the new scenes have no zoom, no
        // smoothing and no minimap.

        // ------------------------------------------------------------------ save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, RouteScene);
        SetStage1BuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log(string.Format(
            "[BorrowedTime] BUILD OK {0}: {1}x{2} map, {3} tiles, {4} blockers, " +
            "{5} props, spawn {6}, gym door {7}, path {8} steps",
            RouteScene, map.Width, map.Height, painted, blockedCells, propCount,
            spawnCell, doorCell, steps));
    }

    private static void AddProp(Transform parent, char ch, int col, int row,
                                Vector3Int cell, Sprite sprite)
    {
        var go = new GameObject("Prop_" + ch + "_" + col + "_" + row);
        go.transform.SetParent(parent, false);
        // Bottom-centre pivot: position the object by its base on the cell.
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        go.AddComponent<SpriteYSort>();
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

        foreach (string path in head)
        {
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].path == path)
                {
                    ordered.Add(new EditorBuildSettingsScene(path, true));
                    break;
                }
            }
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
    private static int skips;

    private static void Check(string label, bool ok)
    {
        if (!ok)
            failures++;
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

        // ------------------------------------------------------------ layout
        TextMap map = null;
        try
        {
            map = LoadLayout(LayoutPath);
            Check("layout " + map.Width + " x " + map.Height + " (expected 24 x 18)",
                  map.Width == 24 && map.Height == 18);
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

        // ------------------------------------------------------------- scene
        var scene = EditorSceneManager.OpenScene(RouteScene, OpenSceneMode.Single);
        Check("scene exists: " + RouteScene, scene.IsValid());

        var collision = UnityEngine.Object.FindFirstObjectByType<MapCollision>();
        Check("MapCollision component present", collision != null);

        if (collision != null)
        {
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
                    Vector3Int cell = map.ToCell(c, r);
                    bool expected = IsBlocking(map.At(c, r));
                    bool actual = collision.blocked[cell.y * collision.width + cell.x];
                    if (expected != actual)
                        mismatches++;
                }
            }
            Check("walkability grid matches legend exactly", mismatches == 0);

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
                if (IsBlocking(map.At(c, r)))
                    expectedBlocked++;

        if (groundGo != null)
        {
            Check("Ground painted every cell (" + (map.Width * map.Height) + ")",
                  CountTiles(groundGo.GetComponent<Tilemap>()) == map.Width * map.Height);
        }

        if (blockGo != null)
        {
            Check("Blockers painted blocking cells (" + expectedBlocked + ")",
                  CountTiles(blockGo.GetComponent<Tilemap>()) == expectedBlocked);

            var br = blockGo.GetComponent<TilemapRenderer>();
            Check("Blockers renderer disabled (invisible)", br != null && !br.enabled);

            var tcol = blockGo.GetComponent<TilemapCollider2D>();
            Check("TilemapCollider2D feeds CompositeCollider2D",
                  tcol != null && tcol.usedByComposite &&
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

    private static void FinishValidate()
    {
        Debug.Log("[BorrowedTime] " + failures + " failure(s), " + skips + " skipped");
        if (failures > 0)
            throw new System.Exception("[BorrowedTime] " + failures + " validation failure(s)");
        Debug.Log("[BorrowedTime] VALIDATE OK");
    }
}
