using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// One-shot project setup + overworld scene builder for the PikaGame remake.
///
/// Run from the menu (Tools > PikaGame > ...) or headlessly:
///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
///             -executeMethod ProjectSetup.BuildAll -logFile -
///
/// Everything here is idempotent: re-running it re-applies import settings and
/// rebuilds the generated scene without duplicating objects.
/// </summary>
public static class ProjectSetup
{
    public const string ArtRoot = "Assets/Art";
    public const string TileAssetDir = "Assets/Art/Tiles/Tiles";
    public const string OverworldScene = "Assets/Scenes/Overworld.unity";

    /// <summary>Pixel size of the generated terrain tiles (must match GenArt.py).</summary>
    public static int tilePixelSize = 512;

    // ------------------------------------------------------------------ folders

    [MenuItem("Tools/PikaGame/1. Ensure Folders")]
    public static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/Art", "Assets/Art/Tiles", "Assets/Art/Tiles/Tiles",
            "Assets/Art/Characters", "Assets/Art/Creatures", "Assets/Art/UI",
            "Assets/Art/World", "Assets/Settings", "Assets/Prefabs",
            "Assets/Sprites/Player", "Assets/Sprites/Pokemon",
            "Assets/Audio/Music", "Assets/Audio/SFX",
            "Assets/Data", "Assets/Tools/out"
        };

        foreach (string f in folders)
        {
            if (AssetDatabase.IsValidFolder(f))
                continue;
            string parent = Path.GetDirectoryName(f).Replace('\\', '/');
            string leaf = Path.GetFileName(f);
            AssetDatabase.CreateFolder(parent, leaf);
        }
        Debug.Log("[ProjectSetup] folders ensured");
    }

    // -------------------------------------------------------- import settings

    /// <summary>Pixels-per-unit, pivot, filter and wrap for each asset group.</summary>
    private static void GetImportProfile(string assetPath, out float ppu, out Vector2 pivot,
                                         out FilterMode filter, out TextureWrapMode wrap)
    {
        filter = FilterMode.Bilinear;
        wrap = TextureWrapMode.Clamp;
        pivot = new Vector2(0.5f, 0.5f);
        ppu = 128f;

        string file = Path.GetFileName(assetPath);
        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');

        if (dir.StartsWith("Assets/Art/Tiles"))
        {
            ppu = file == "tile_collision.png" ? 16f : (tilePixelSize / 2f);
            pivot = new Vector2(0.5f, 0.5f);      // tiles MUST be centre-pivoted
        }
        else if (dir.StartsWith("Assets/Art/Characters"))
        {
            ppu = 192f;
            pivot = new Vector2(0.5f, 0.04f);     // anchor at the feet
        }
        else if (dir.StartsWith("Assets/Art/Creatures"))
        {
            ppu = 192f;
            pivot = new Vector2(0.5f, 0.04f);     // anchor at the feet
        }
        else if (dir.StartsWith("Assets/Art/World"))
        {
            ppu = 128f;
            pivot = new Vector2(0.5f, 0.04f);     // props stand on the ground
        }
        else if (dir.StartsWith("Assets/Art/UI"))
        {
            ppu = 128f;
            pivot = new Vector2(0.5f, 0.5f);
        }
        else if (dir.StartsWith("Assets/Sprites"))
        {
            ppu = 100f;
            pivot = new Vector2(0.5f, 0.5f);
        }
    }

    /// <summary>
    /// Apply quality-first import settings to every sprite. Mipmaps + anisotropy
    /// + no compression is what stops the painted art shimmering when zoomed
    /// out and smearing when zoomed in.
    /// </summary>
    [MenuItem("Tools/PikaGame/2. Apply Import Settings")]
    public static void ApplyImportSettings()
    {
        var guids = new List<string>();
        guids.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" }));
        guids.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }));

        int changed = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".jpg") || path.EndsWith(".jpeg"))
                continue;   // MainUi.jpg is left alone

            // The hand-made pixel-art pack has its own importer: this profile
            // enables mipmaps + bilinear filtering, which blurs pixel art.
            // See PixelArtImporter.ImportArtPack.
            if (path.StartsWith("Assets/Art/_Mine"))
                continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            float ppu;
            Vector2 pivot;
            FilterMode filter;
            TextureWrapMode wrap;
            GetImportProfile(path, out ppu, out pivot, out filter, out wrap);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.spritePivot = pivot;
            importer.filterMode = filter;
            importer.wrapMode = wrap;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            // 9-slice borders for the UI frames so Image.Type.Sliced works.
            string file = Path.GetFileName(path);
            if (file == "hud_panel.png")
                importer.spriteBorder = new Vector4(40f, 40f, 40f, 40f);
            else if (file == "minimap_bezel.png" || file == "minimap_mask.png")
                importer.spriteBorder = new Vector4(0f, 0f, 0f, 0f);

            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log("[ProjectSetup] import settings applied to " + changed + " textures");
    }

    /// <summary>Make sure the URP asset is actually the active pipeline.</summary>
    [MenuItem("Tools/PikaGame/3. Ensure URP")]
    public static void EnsureRenderPipeline()
    {
        const string urpPath = "Assets/Settings/UniversalRP.asset";
        var urp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(urpPath);
        if (urp == null)
        {
            Debug.LogError("[ProjectSetup] " + urpPath + " is missing, so URP cannot be " +
                           "assigned. Re-extract Assets/Settings from a URP 2D template.");
            return;
        }

        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urp;

        int levels = QualitySettings.names.Length;
        int original = QualitySettings.GetQualityLevel();
        for (int i = 0; i < levels; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urp;
        }
        QualitySettings.SetQualityLevel(original, false);

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] URP assigned to Graphics + " + levels + " quality levels");
    }

    // ----------------------------------------------------------------- assets

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
            Debug.LogError("[ProjectSetup] missing sprite: " + path);
        return s;
    }

    /// <summary>Create (or refresh) a Tile asset at <paramref name="assetPath"/>.</summary>
    private static Tile MakeTile(string assetPath, Sprite sprite, Color color)
    {
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Sprite;
        tile.color = color;
        tile.transform = Matrix4x4.identity;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    /// <summary>Build the Tile assets from the generated tile sprites.</summary>
    [MenuItem("Tools/PikaGame/4. Build Tile Assets")]
    public static void BuildTileAssets()
    {
        EnsureFolders();

        for (int i = 0; i < TERRAIN_NAMES.Length; i++)
        {
            string name = TERRAIN_NAMES[i].ToLowerInvariant();
            // GenArt.py writes tile_00_grass.png -- zero padded to two digits.
            string spritePath = string.Format("Assets/Art/Tiles/tile_{0:00}_{1}.png", i, name);
            Sprite s = LoadSprite(spritePath);
            if (s == null)
                continue;
            MakeTile(string.Format("{0}/tile_{1:00}_{2}.asset", TileAssetDir, i, name),
                     s, Color.white);
        }

        // Collision-only tile: collider only, never drawn (renderer is disabled).
        Sprite collSprite = LoadSprite("Assets/Art/Tiles/tile_collision.png");
        if (collSprite != null)
            MakeTile(TileAssetDir + "/tile_collision.asset", collSprite, Color.clear);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProjectSetup] tile assets built in " + TileAssetDir);
    }

    /// <summary>
    /// Terrain indices that the player may NOT stand on. Everything else is
    /// painted into the invisible Blockers tilemap, so paths are gated purely
    /// by the classified map rather than hand-placed box colliders.
    /// MUST match Tools/GenArt.py: 0 Grass 1 TallGrass 2 Path 3 Sand 4 Water
    /// 5 Forest 6 Rock 7 Wall 8 Floor 9 Bridge, with WALKABLE = {0,1,2,3,8,9}.
    /// </summary>
    public static readonly string[] TERRAIN_NAMES =
        { "Grass", "TallGrass", "Path", "Sand", "Water", "Forest",
          "Rock", "Wall", "Floor", "Bridge" };

    /// <summary>Water / Forest / Rock / Wall.</summary>
    public static readonly HashSet<int> BlockedTerrain = new HashSet<int> { 4, 5, 6, 7 };

    // ------------------------------------------------------------- terrain map

    /// <summary>Row/column grid written by Tools/GenArt.py.</summary>
    public class TerrainMap
    {
        public int cols, rows;
        public int spawnCol, spawnRow;
        public int[,] cell;          // [row, col]; row 0 is the TOP row of the source art

        /// <summary>Unity cell for a CSV row/col. Unity's +Y is up, so rows flip.</summary>
        public Vector3Int ToCell(int col, int row)
        {
            return new Vector3Int(col, rows - 1 - row, 0);
        }

        /// <summary>World centre of a cell (Grid cellSize is 2x2).</summary>
        public Vector2 ToWorld(int col, int row)
        {
            Vector3Int c = ToCell(col, row);
            return new Vector2((c.x + 0.5f) * 2f, (c.y + 0.5f) * 2f);
        }
    }

    /// <summary>
    /// Read Tools/out/terrain_map.csv. Written as '#' header lines (cols/rows/
    /// spawn/walkable/names) followed by one comma-separated row per line, so no
    /// JSON parser is required in the editor.
    /// </summary>
    public static TerrainMap LoadTerrainMap()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                                                    "../Tools/out/terrain_map.csv"));
        if (!File.Exists(path))
        {
            Debug.LogError("[ProjectSetup] terrain_map.csv not found at " + path +
                           " -- run  python Tools/GenArt.py --only terrain");
            return null;
        }

        var m = new TerrainMap();
        var rows = new List<int[]>();

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#"))
            {
                if (line.Contains("cols=")) m.spawnCol = ReadInt(line, "spawn_col=");
                if (line.Contains("rows=")) m.spawnRow = ReadInt(line, "spawn_row=");
                continue;
            }

            string[] parts = line.Split(',');
            var vals = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                int.TryParse(parts[i].Trim(), out vals[i]);
            rows.Add(vals);
        }

        m.rows = rows.Count;
        m.cols = rows.Count > 0 ? rows[0].Length : 0;
        m.cell = new int[m.rows, m.cols];
        for (int r = 0; r < m.rows; r++)
            for (int c = 0; c < m.cols && c < rows[r].Length; c++)
                m.cell[r, c] = rows[r][c];

        // Re-read both headers properly (they are on different lines).
        foreach (string raw in File.ReadAllLines(path))
        {
            if (!raw.StartsWith("#")) continue;
            if (raw.Contains("cols="))
            {
                m.cols = ReadInt(raw, "cols=");
                m.rows = ReadInt(raw, "rows=");
                m.spawnCol = ReadInt(raw, "spawn_col=");
                m.spawnRow = ReadInt(raw, "spawn_row=");
            }
        }

        if (m.rows * m.cols == 0)
        {
            Debug.LogError("[ProjectSetup] terrain_map.csv parsed to nothing");
            return null;
        }

        Debug.Log(string.Format("[ProjectSetup] terrain {0}x{1}, spawn ({2},{3})",
                                m.cols, m.rows, m.spawnCol, m.spawnRow));
        return m;
    }

    private static int ReadInt(string line, string key)
    {
        int i = line.IndexOf(key, System.StringComparison.Ordinal);
        if (i < 0) return 0;
        i += key.Length;
        int end = i;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;
        int v;
        int.TryParse(line.Substring(i, end - i), out v);
        return v;
    }

    public static readonly Rect WorldBounds = new Rect(0f, 0f, 96f, 64f);

    // ------------------------------------------------------------- tilemaps

    private static Tile[] LoadTiles()
    {
        var tiles = new Tile[TERRAIN_NAMES.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            string name = TERRAIN_NAMES[i].ToLowerInvariant();
            tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>(
                string.Format("{0}/tile_{1:00}_{2}.asset", TileAssetDir, i, name));
            if (tiles[i] == null)
                Debug.LogError("[ProjectSetup] missing Tile asset for index " + i + " (" + name + ")");
        }
        return tiles;
    }

    /// <summary>
    /// Create the Grid with a visible Ground tilemap and an invisible Blockers
    /// tilemap that carries the CompositeCollider2D. Replaces the old single
    /// BoxCollider2D stretched over the whole map.
    /// </summary>
    private static void BuildGrid(TerrainMap m, out GameObject gridGo)
    {
        Tile[] tiles = LoadTiles();
        Tile collTile = AssetDatabase.LoadAssetAtPath<Tile>(TileAssetDir + "/tile_collision.asset");
        if (collTile == null)
            Debug.LogError("[ProjectSetup] missing tile_collision.asset");

        gridGo = new GameObject("Grid");
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(2f, 2f, 1f);
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;

        var groundGo = new GameObject("Ground");
        groundGo.transform.SetParent(gridGo.transform, false);
        var ground = groundGo.AddComponent<Tilemap>();
        ground.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var groundR = groundGo.AddComponent<TilemapRenderer>();
        groundR.sortingOrder = 0;
        groundR.mode = TilemapRenderer.Mode.Chunk;
        groundR.chunkCullingBounds = new Vector3(12f, 12f, 0f);

        var blockGo = new GameObject("Blockers");
        blockGo.transform.SetParent(gridGo.transform, false);
        var blockers = blockGo.AddComponent<Tilemap>();
        blockers.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        var blockR = blockGo.AddComponent<TilemapRenderer>();
        blockR.enabled = false;            // collider-only: never drawn

        var rb = blockGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.gravityScale = 0f;

        var composite = blockGo.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.usedByComposite = false;
        composite.edgeRadius = 0f;

        var col = blockGo.AddComponent<TilemapCollider2D>();
        col.usedByComposite = true;
        col.offset = Vector2.zero;
        // Each Blocker Tile carries Tile.ColliderType.Sprite (see MakeTile), so
        // a solid square of collider geometry is generated per blocked cell.

        int painted = 0, blocked = 0;
        for (int r = 0; r < m.rows; r++)
        {
            for (int c = 0; c < m.cols; c++)
            {
                int idx = m.cell[r, c];
                if (idx < 0 || idx >= tiles.Length || tiles[idx] == null)
                    continue;

                Vector3Int cell = m.ToCell(c, r);
                ground.SetTile(cell, tiles[idx]);
                painted++;

                if (BlockedTerrain.Contains(idx) && collTile != null)
                {
                    blockers.SetTile(cell, collTile);
                    blocked++;
                }
            }
        }

        ground.CompressBounds();
        blockers.CompressBounds();

        Debug.Log(string.Format("[ProjectSetup] tilemap painted {0} cells, {1} blockers",
                                painted, blocked));
    }

    // ----------------------------------------------------------------- props

    private static readonly string[] PROP_NAMES = { "tree", "bush", "rock", "sign", "flowers" };

    private static bool IsBlocked(TerrainMap m, int c, int r)
    {
        if (c < 0 || r < 0 || c >= m.cols || r >= m.rows)
            return true;
        return BlockedTerrain.Contains(m.cell[r, c]);
    }

    /// <summary>
    /// Deterministically scatter props on grass that touches blocked terrain,
    /// so the forest edge reads as undergrowth rather than a hard line.
    /// </summary>
    private static void BuildProps(TerrainMap m, Transform parent)
    {
        var sprites = new Sprite[PROP_NAMES.Length];
        for (int i = 0; i < PROP_NAMES.Length; i++)
            sprites[i] = LoadSprite("Assets/Art/World/" + PROP_NAMES[i] + ".png");
        if (sprites[0] == null)
            return;

        var props = new GameObject("Props").transform;
        props.SetParent(parent, false);

        var rnd = new System.Random(20261002);       // fixed seed: reproducible
        var used = new HashSet<Vector3Int>();
        int placed = 0;

        for (int r = 0; r < m.rows; r++)
        {
            for (int c = 0; c < m.cols; c++)
            {
                if (m.cell[r, c] != 0)               // Grass only
                    continue;
                if (Mathf.Abs(c - m.spawnCol) < 3 && Mathf.Abs(r - m.spawnRow) < 3)
                    continue;                         // keep the spawn clear

                int edge = 0;
                if (IsBlocked(m, c - 1, r)) edge++;
                if (IsBlocked(m, c + 1, r)) edge++;
                if (IsBlocked(m, c, r - 1)) edge++;
                if (IsBlocked(m, c, r + 1)) edge++;
                if (edge == 0)
                    continue;

                Vector3Int cell = m.ToCell(c, r);
                if (used.Contains(cell) || rnd.NextDouble() > 0.30)
                    continue;
                used.Add(cell);

                int pick = edge >= 2 && rnd.NextDouble() < 0.70 ? 0      // tree
                         : (rnd.NextDouble() < 0.45 ? 1                  // bush
                          : (rnd.NextDouble() < 0.75 ? 2                 // rock
                                                   : 4));                // flowers
                if (sprites[pick] == null)
                    continue;

                var go = new GameObject(PROP_NAMES[pick] + "_" + c + "_" + r);
                go.transform.SetParent(props, false);
                Vector2 w = m.ToWorld(c, r);
                // Nudge off-cell so props never sit dead-centre on the tile.
                go.transform.position = new Vector3(
                    w.x + (float)(rnd.NextDouble() - 0.5) * 0.5f, w.y - 0.4f, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[pick];
                sr.sortingOrder = 10;

                // Decorative props still need to block movement, otherwise the
                // player walks straight through a tree trunk. Flowers do not.
                if (pick != 4)
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    float w2 = pick == 1 ? 0.5f : 0.6f;
                    box.size = new Vector2(w2, 0.6f);
                    box.offset = new Vector2(0f, 0.3f);
                }

                go.AddComponent<SpriteYSort>();
                placed++;
            }
        }
        Debug.Log("[ProjectSetup] " + placed + " props scattered");
    }

    // ---------------------------------------------------------------- player

    private static readonly string[] DIR_ORDER = { "down", "left", "right", "up" };

    private static Sprite[] LoadCharacterFrames(string prefix)
    {
        var frames = new Sprite[16];
        for (int d = 0; d < 4; d++)
            for (int f = 0; f < 4; f++)
                frames[d * 4 + f] = LoadSprite(string.Format(
                    "Assets/Art/Characters/{0}_{1}_{2}.png", prefix, DIR_ORDER[d], f));
        return frames;
    }

    private static GameObject BuildPlayer(TerrainMap m)
    {
        var go = new GameObject("Player");
        go.tag = "Player";
        Vector2 spawn = m.ToWorld(m.spawnCol, m.spawnRow);
        go.transform.position = new Vector3(spawn.x, spawn.y, 0f);

        Sprite[] frames = LoadCharacterFrames("player");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = 10;
        go.AddComponent<SpriteYSort>();

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        // Pivot sits at the feet, so centre the box over the lower body.
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.62f, 0.66f);
        box.offset = new Vector2(0f, 0.33f);

        go.AddComponent<PlayerController>();

        var anim = go.AddComponent<PlayerAnimator>();
        anim.frames = frames;
        anim.framesPerDirection = 4;
        anim.idleFrame = 0;
        anim.walkFps = 8f;
        anim.bobAmplitude = 0f;

        Debug.Log("[ProjectSetup] player spawned at " + spawn);
        return go;
    }

    // ---------------------------------------------------------------- camera

    private static GameObject BuildMainCamera(Transform target)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.nearClipPlane = -100f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
        cam.allowHDR = false;
        cam.allowMSAA = false;

        go.transform.position = new Vector3(target.position.x, target.position.y, -10f);

        var rig = go.AddComponent<CameraRig>();
        rig.target = target;
        rig.minOrthoSize = 2.6f;
        rig.maxOrthoSize = 12f;
        rig.clampToBounds = true;
        rig.worldBounds = WorldBounds;
        rig.dynamicMipBias = true;
        rig.trackedPpu = 256f;                 // 512px tiles across 2 world units
        rig.trackedTextures = LoadTrackedTextures();

        return go;
    }

    /// <summary>
    /// World-rendered textures whose mipmap bias CameraRig drives from the zoom
    /// level. UI and creature portraits are deliberately excluded: they are not
    /// drawn by the world camera, and a bias tuned for 256 PPU terrain would
    /// only soften them.
    /// </summary>
    private static Texture2D[] LoadTrackedTextures()
    {
        var list = new List<Texture2D>();
        string[] worldRoots = { "Assets/Art/Tiles", "Assets/Art/World", "Assets/Art/Characters" };

        foreach (string root in worldRoots)
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("tile_collision.png"))
                    continue;                      // never drawn, so bias is pointless
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null && tex.mipmapCount > 1)
                    list.Add(tex);
            }

        Debug.Log("[ProjectSetup] tracking " + list.Count + " world textures for zoom bias");
        return list.ToArray();
    }

    // -------------------------------------------------------------- UI helpers

    private static TMP_FontAsset cachedFont;

    private static TMP_FontAsset DefaultFont()
    {
        if (cachedFont == null)
        {
            cachedFont = TMP_Settings.defaultFontAsset;
            if (cachedFont == null)
                cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
        return cachedFont;
    }

    /// <summary>New UI object. Always RectTransform, so parenting never breaks layout.</summary>
    private static GameObject UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform Layout(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color tint)
    {
        var go = UI(name, parent);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = tint;
        img.raycastTarget = false;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent, float fontSize, Color color,
                                    TextAlignmentOptions align, TextWrappingModes wrap)
    {
        var go = UI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = DefaultFont();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = wrap;
        tmp.raycastTarget = false;
        tmp.text = "";
        return tmp;
    }

    // --------------------------------------------------------------- minimap

    private const string MinimapRtPath = "Assets/Settings/MinimapRT.asset";

    private static RenderTexture EnsureMinimapRT()
    {
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(MinimapRtPath);
        if (rt == null)
        {
            rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT";
            rt.filterMode = FilterMode.Bilinear;
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();
            AssetDatabase.CreateAsset(rt, MinimapRtPath);
        }
        return rt;
    }

    private static GameObject BuildMinimapCamera()
    {
        var go = new GameObject("MinimapCamera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 32f;
        cam.nearClipPlane = -50f;
        cam.farClipPlane = 50f;
        cam.depth = -1;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.cullingMask = -1;                  // overridden by MinimapController
        cam.targetTexture = EnsureMinimapRT();
        go.transform.position = new Vector3(WorldBounds.center.x,
                                            WorldBounds.center.y, -20f);
        return go;                             // must stay active: it has no enabler
    }

    // ------------------------------------------------------------------- HUD

    /// <summary>Everything the orchestrator needs to wire after building the HUD.</summary>
    private class HudRefs
    {
        public GameObject root;
        public MinimapController minimap;
        public HUDController hud;
        public DialogueSystem dialogue;
        public GameObject dialoguePanel;
        public CanvasGroup dialogueGroup;
        public TMP_Text speakerText, bodyText;
        public TMP_Text regionLabel, subLabel, compassLabel, zoomLabel, mapPosLabel;
        public RectTransform mapContainerRt, markerRt, arrowRt;
        public RawImage mapImage;
        public GameObject regionPanel;
        public CanvasGroup regionGroup;
    }

    private const float RefW = 1920f, RefH = 1080f;
    private const float MiniRadius = 96f;      // canvas pixels: MinimapController default

    private static HudRefs BuildHud(Transform player, GameObject minimapCam, RenderTexture rt)
    {
        var hud = new HudRefs();

        hud.root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas),
                                  typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = hud.root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = hud.root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight = 0.5f;

        BuildMinimapWidgets(hud, rt);
        BuildHudText(hud);

        hud.dialoguePanel = BuildDialogue(hud);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        hud.hud = hud.root.AddComponent<HUDController>();
        hud.hud.regionLabel = hud.regionLabel;
        hud.hud.subLabel = hud.subLabel;
        hud.hud.compassLabel = hud.compassLabel;
        hud.hud.zoomLabel = hud.zoomLabel;
        hud.hud.minimapPositionLabel = hud.mapPosLabel;

        hud.minimap = hud.root.AddComponent<MinimapController>();
        hud.minimap.player = player;
        hud.minimap.playerController = player.GetComponent<PlayerController>();
        hud.minimap.minimapCamera = minimapCam.GetComponent<Camera>();
        hud.minimap.renderTexture = rt;
        hud.minimap.worldBounds = WorldBounds;
        hud.minimap.mode = MinimapController.Mode.WholeWorld;
        hud.minimap.northUp = true;
        hud.minimap.circleRadiusPixels = MiniRadius;
        hud.minimap.minimapCullingMask = ~0;
        hud.minimap.mapImage = hud.mapImage;
        hud.minimap.mapContainer = hud.mapContainerRt;
        hud.minimap.marker = hud.markerRt;
        hud.minimap.markerArrow = hud.arrowRt;

        hud.hud.minimap = hud.minimap;
        hud.hud.regionPanel = hud.regionGroup;

        hud.dialogue = hud.root.AddComponent<DialogueSystem>();
        hud.dialogue.panel = hud.dialoguePanel;
        hud.dialogue.bodyText = hud.bodyText;
        hud.dialogue.speakerText = hud.speakerText;
        hud.dialogue.canvasGroup = hud.dialogueGroup;
        hud.dialogue.defaultDuration = 3f;
        hud.dialogue.queueMessages = false;

        return hud;
    }

    /// <summary>Circular mask + render texture + N/S marker + bezel.</summary>
    private static void BuildMinimapWidgets(HudRefs hud, RenderTexture rt)
    {
        var container = UI("Minimap", hud.root.transform);
        Layout(container, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
               new Vector2(224f, 224f), new Vector2(-136f, -136f));

        var maskGo = UI("Mask", container.transform);
        Layout(maskGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(MiniRadius * 2f, MiniRadius * 2f),
               Vector2.zero);
        var maskImg = maskGo.AddComponent<Image>();
        maskImg.sprite = LoadSprite("Assets/Art/UI/minimap_mask.png");
        maskImg.color = new Color(1f, 1f, 1f, 1f);
        maskImg.raycastTarget = false;
        var mask = maskGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;          // clip only; the bezel draws the rim

        var mc = UI("MapContainer", maskGo.transform);
        var mcRt = Layout(mc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0.5f, 0.5f),
                          new Vector2(MiniRadius * 2f, MiniRadius * 2f), Vector2.zero);

        var mapGo = UI("Map", mc.transform);
        Layout(mapGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(MiniRadius * 2f, MiniRadius * 2f),
               Vector2.zero);
        var raw = mapGo.AddComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;
        raw.color = Color.white;

        var markerGo = UI("Marker", mc.transform);
        var markerRt = Layout(markerGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              new Vector2(0.5f, 0.5f), new Vector2(18f, 18f), Vector2.zero);

        var arrowGo = UI("Arrow", markerGo.transform);
        var arrowRt = Layout(arrowGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0.5f, 0.5f), new Vector2(18f, 18f), Vector2.zero);
        var arrowImg = arrowGo.AddComponent<Image>();
        arrowImg.sprite = LoadSprite("Assets/Art/UI/minimap_arrow.png");
        arrowImg.color = Color.white;
        arrowImg.raycastTarget = false;

        // Bezel goes last so it paints over the clipped map edge.
        var bezel = NewImage("Bezel", container.transform,
                             LoadSprite("Assets/Art/UI/minimap_bezel.png"), Color.white);
        Layout(bezel.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(224f, 224f), Vector2.zero);

        hud.mapContainerRt = mcRt;
        hud.markerRt = markerRt;
        hud.arrowRt = arrowRt;
        hud.mapImage = raw;
    }

    /// <summary>Region banner, compass readout, zoom readout, minimap caption.</summary>
    private static void BuildHudText(HudRefs hud)
    {
        // --- Region banner, top-centre, faded in by MapRegion ---
        var rp = UI("RegionPanel", hud.root.transform);
        Layout(rp, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
               new Vector2(760f, 116f), new Vector2(0f, -24f));
        var bg = rp.AddComponent<Image>();
        bg.sprite = LoadSprite("Assets/Art/UI/hud_panel.png");
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;
        hud.regionGroup = rp.AddComponent<CanvasGroup>();
        hud.regionGroup.alpha = 0f;
        hud.regionGroup.blocksRaycasts = false;
        hud.regionPanel = rp;

        hud.regionLabel = NewText("RegionLabel", rp.transform, 42f, Color.white,
                                  TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        Layout(hud.regionLabel.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(700f, 52f), new Vector2(0f, 20f));

        hud.subLabel = NewText("RegionSub", rp.transform, 22f,
                               new Color(0.85f, 0.88f, 0.95f, 0.95f),
                               TextAlignmentOptions.Center, TextWrappingModes.NoWrap);
        Layout(hud.subLabel.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(700f, 34f), new Vector2(0f, -28f));

        // --- Compass + zoom, top-left ---
        hud.compassLabel = NewText("Compass", hud.root.transform, 30f, Color.white,
                                   TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap);
        Layout(hud.compassLabel.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
               new Vector2(0f, 1f), new Vector2(620f, 44f), new Vector2(32f, -32f));

        hud.zoomLabel = NewText("ZoomReadout", hud.root.transform, 24f,
                                new Color(0.8f, 0.85f, 0.95f, 0.9f),
                                TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap);
        Layout(hud.zoomLabel.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
               new Vector2(0f, 1f), new Vector2(620f, 36f), new Vector2(32f, -76f));

        // --- Caption under the minimap ---
        hud.mapPosLabel = NewText("MapPosition", hud.root.transform, 20f,
                                  new Color(0.8f, 0.85f, 0.95f, 0.9f),
                                  TextAlignmentOptions.TopRight, TextWrappingModes.NoWrap);
        Layout(hud.mapPosLabel.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
               new Vector2(1f, 1f), new Vector2(300f, 32f), new Vector2(-24f, -264f));
    }

    /// <summary>Bottom-centre dialogue box with speaker + body lines.</summary>
    private static GameObject BuildDialogue(HudRefs hud)
    {
        var p = UI("Dialogue", hud.root.transform);
        Layout(p, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
               new Vector2(1180f, 190f), new Vector2(0f, 40f));

        var img = p.AddComponent<Image>();
        img.sprite = LoadSprite("Assets/Art/UI/hud_panel.png");
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = false;

        var cg = p.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        hud.dialogueGroup = cg;

        hud.speakerText = NewText("Speaker", p.transform, 30f,
                                  new Color(1f, 0.88f, 0.5f, 1f),
                                  TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap);
        Layout(hud.speakerText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(1080f, 40f), new Vector2(0f, 62f));

        hud.bodyText = NewText("Body", p.transform, 30f, Color.white,
                               TextAlignmentOptions.TopLeft, TextWrappingModes.Normal);
        Layout(hud.bodyText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0.5f, 0.5f), new Vector2(1080f, 104f), new Vector2(0f, -14f));

        return p;
    }

    // ----------------------------------------------------- regions and gates

    /// <summary>Three horizontal bands so the region label changes as you travel north.</summary>
    private static void BuildRegions(TerrainMap m)
    {
        var root = new GameObject("Regions").transform;

        int r1 = Mathf.RoundToInt(m.rows * 0.34f);
        int r2 = Mathf.RoundToInt(m.rows * 0.67f);

        AddRegion(root, m, "VERDANT APPROACH", "The rock narrows", 0, r1, 0, "visited_pass");
        AddRegion(root, m, "STONERIDGE PASS", "Tall grass, open sky", r1 + 1, r2, 1, "visited_mid");
        AddRegion(root, m, "MIRRORWATER SHALLOWS", "Where you began", r2 + 1, m.rows - 1, 2,
                  "route1_entered");
    }

    private static void AddRegion(Transform parent, TerrainMap m, string name, string sub,
                                  int row0, int row1, int priority, string flag)
    {
        // Convert CSV rows (top-down) into a world-space Y band (bottom-up).
        float yBottom = (m.rows - 1 - row1) * 2f;
        float yTop = (m.rows - row0 + 1) * 2f;

        var go = new GameObject("Region_" + name.Replace(' ', '_'));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(WorldBounds.center.x,
                                            (yBottom + yTop) * 0.5f, 0f);

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(m.cols * 2f, yTop - yBottom);

        var region = go.AddComponent<MapRegion>();
        region.regionName = name;
        region.subLabel = sub;
        region.priority = priority;
        region.flagOnEnter = flag;
    }

    /// <summary>
    /// Find a corridor cell (a Path tile with exactly two walkable neighbours)
    /// so the gate spans the walkable width instead of cutting across open ground.
    /// </summary>
    private static bool FindChokepoint(TerrainMap m, int startRow, int endRow,
                                       out int col, out int row)
    {
        for (int r = startRow; r <= endRow && r < m.rows; r++)
        {
            for (int c = 1; c < m.cols - 1; c++)
            {
                if (m.cell[r, c] != 2)                 // Path
                    continue;

                int open = 0;
                if (!IsBlocked(m, c - 1, r)) open++;
                if (!IsBlocked(m, c + 1, r)) open++;
                if (!IsBlocked(m, c, r - 1)) open++;
                if (!IsBlocked(m, c, r + 1)) open++;

                if (open == 2)
                {
                    col = c;
                    row = r;
                    return true;
                }
            }
        }
        col = row = 0;
        return false;
    }

    /// <summary>
    /// Two gates with deliberately different outcomes, placed so the player can
    /// never be soft-locked: gate 1 sits *inside* the middle band (so the band's
    /// flag fires before you reach it and the gate opens), gate 2 seals the
    /// northern band and needs a gym, which does not exist yet, so it stays shut
    /// and readable.
    /// </summary>
    private static void BuildGates(TerrainMap m)
    {
        var root = new GameObject("Gates").transform;
        int col, row;

        // Rows 16..20 sit in the lower half of the middle band; rows 21+ stay
        // reachable so the band's flag is set before the player reaches the gate.
        if (FindChokepoint(m, 16, 20, out col, out row))
        {
            AddGate(root, m.ToWorld(col, row), "visited_mid", false,
                    CreatureType.None, "The pass opens for those who have walked it.");
        }
        else
            Debug.LogWarning("[ProjectSetup] no chokepoint for gate 1 in rows 16..21");

        // Rows 11..15 straddle the boundary into the northern band.
        if (FindChokepoint(m, 11, 15, out col, out row))
        {
            AddGate(root, m.ToWorld(col, row), "", true,
                    CreatureType.None, "You are not ready for what lies beyond the pass.");
        }
        else
            Debug.LogWarning("[ProjectSetup] no chokepoint for gate 2 in rows 11..15");
    }

    private static void AddGate(Transform parent, Vector2 world, string flag,
                                bool needsGym, CreatureType type, string lockedMessage)
    {
        var go = new GameObject("Gate_" + (int)world.x + "_" + (int)world.y);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(world.x, world.y, 0f);

        var gateSprite = LoadSprite("Assets/Art/World/gate.png");

        // Trigger volume the player walks into.
        var trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(3f, 3.4f);
        trigger.offset = new Vector2(0f, 1.5f);

        // The physical barrier: a child, disabled once the gate opens.
        var barrierGo = new GameObject("Barrier");
        barrierGo.transform.SetParent(go.transform, false);
        barrierGo.transform.localPosition = Vector3.zero;

        var sr = barrierGo.AddComponent<SpriteRenderer>();
        sr.sprite = gateSprite;
        sr.sortingOrder = 10;
        barrierGo.AddComponent<SpriteYSort>();

        var block = barrierGo.AddComponent<BoxCollider2D>();
        block.size = new Vector2(2f, 2.2f);
        block.offset = new Vector2(0f, 1.1f);

        var gate = go.AddComponent<RequirementGate>();
        gate.barrier = barrierGo;
        gate.requireAll = true;
        gate.openMessage = lockedMessage;
        gate.messageOnceWhileTouching = true;
        gate.messageCooldown = 1.5f;
        gate.hideBarrierWhenOpen = true;
        gate.oneShot = false;

        var reqs = new System.Collections.Generic.List<Requirement>();
        if (!string.IsNullOrEmpty(flag))
            reqs.Add(new Requirement
            {
                kind = RequirementKind.Flag,
                key = flag,
                invert = false
            });
        if (needsGym)
            reqs.Add(new Requirement
            {
                kind = RequirementKind.GymsCleared,
                amount = 1
            });
        if (type != CreatureType.None)
            reqs.Add(new Requirement
            {
                kind = RequirementKind.CreatureType,
                creatureType = type
            });
        gate.requirements = reqs.ToArray();

        Debug.Log("[ProjectSetup] gate at " + world + " requires " + reqs.Count + " condition(s)");
    }

    // ------------------------------------------------------------ orchestrator

    /// <summary>Put Overworld first so it is the scene that opens / builds.</summary>
    private static void AddToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].path != OverworldScene)
                continue;
            if (i != 0)
            {
                list.RemoveAt(i);
                list.Insert(0, new EditorBuildSettingsScene(OverworldScene, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            return;
        }
        list.Insert(0, new EditorBuildSettingsScene(OverworldScene, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    [MenuItem("Tools/PikaGame/5. Build Overworld Scene")]
    public static void BuildOverworldScene()
    {
        EnsureFolders();
        ApplyImportSettings();
        EnsureRenderPipeline();
        BuildTileAssets();

        TerrainMap m = LoadTerrainMap();
        if (m == null)
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject grid;
        BuildGrid(m, out grid);
        BuildProps(m, grid.transform);

        GameObject player = BuildPlayer(m);
        BuildMainCamera(player.transform);
        GameObject minimapCam = BuildMinimapCamera();

        RenderTexture rt = EnsureMinimapRT();
        HudRefs hud = BuildHud(player.transform, minimapCam, rt);

        BuildRegions(m);
        BuildGates(m);

        var managers = new GameObject("Managers");
        var state = managers.AddComponent<GameState>();
        state.persist = true;
        state.startingCreature = CreatureType.Tide;   // gives a starter to a fresh save

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, OverworldScene);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log("[ProjectSetup] scene saved to " + OverworldScene);
    }

    /// <summary>
    /// Headless entry point:
    /// Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///           -executeMethod ProjectSetup.BuildAll -logFile -
    /// </summary>
    [MenuItem("Tools/PikaGame/9. Build All")]
    public static void BuildAll()
    {
        BuildOverworldScene();
        AssetDatabase.Refresh();
        Debug.Log("[ProjectSetup] BuildAll finished");
    }

    private static int failures;
    private static void Check(string label, bool ok)
    {
        if (!ok) failures++;
        Debug.Log("[Validate] " + (ok ? "PASS  " : "FAIL  ") + label);
    }

    private static int CountTiles(Tilemap map)
    {
        if (map == null)
            return -1;
        map.CompressBounds();
        int n = 0;
        foreach (var pos in map.cellBounds.allPositionsWithin)
            if (map.HasTile(pos))
                n++;
        return n;
    }

    /// <summary>Render a camera straight to a PNG. Works in -batchmode (no Game view).</summary>
    private static string RenderCamToPng(Camera cam, int w, int h, string relativePath)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.Create();

        RenderTexture prevTarget = cam.targetTexture;
        bool prevAllowMsaa = cam.allowMSAA;
        cam.targetTexture = rt;
        cam.allowMSAA = false;
        cam.Render();
        cam.targetTexture = prevTarget;
        cam.allowMSAA = prevAllowMsaa;

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        rt.Release();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(abs));
        File.WriteAllBytes(abs, png);
        Debug.Log("[ProjectSetup] wrote " + abs + " (" + png.Length + " bytes)");
        return abs;
    }

    /// <summary>
    /// Still frame of the world camera and the minimap, straight from the scene
    /// as it would look at spawn. Note: Screen Space Overlay UI (the HUD) is not
    /// drawn into a camera RenderTexture, so these show the world only.
    /// </summary>
    [MenuItem("Tools/PikaGame/7. Capture Preview PNG")]
    public static void CapturePreview()
    {
        var scene = EditorSceneManager.OpenScene(OverworldScene, OpenSceneMode.Single);

        Camera world = null, mini = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (c.CompareTag("MainCamera")) world = c;
            else if (c.targetTexture != null) mini = c;
        }

        if (world == null)
        {
            Debug.LogError("[ProjectSetup] no MainCamera in " + scene.name);
            return;
        }

        RenderCamToPng(world, 1600, 900, "Tools/out/preview_world.png");

        if (mini != null)
            RenderCamToPng(mini, 768, 768, "Tools/out/preview_minimap.png");
        else
            Debug.LogWarning("[ProjectSetup] no minimap camera found");

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Headless self-check:
    /// Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///           -executeMethod ProjectSetup.ValidateScene -logFile -
    /// Throws (non-zero exit) if any wiring is wrong.
    /// </summary>
    [MenuItem("Tools/PikaGame/8. Validate Scene")]
    public static void ValidateScene()
    {
        var scene = EditorSceneManager.OpenScene(OverworldScene, OpenSceneMode.Single);
        failures = 0;

        var player = GameObject.Find("Player");
        Check("Player exists", player != null);
        if (player != null)
        {
            Check("Player tag", player.CompareTag("Player"));
            Check("PlayerController", player.GetComponent<PlayerController>() != null);

            var anim = player.GetComponent<PlayerAnimator>();
            Check("PlayerAnimator", anim != null);
            Check("PlayerAnimator has 16 non-null frames",
                  anim != null && anim.frames != null && anim.frames.Length == 16 &&
                  System.Array.TrueForAll(anim.frames, s => s != null));

            var rb = player.GetComponent<Rigidbody2D>();
            Check("Player Rigidbody2D dynamic / no gravity / rotation frozen",
                  rb != null && rb.bodyType == RigidbodyType2D.Dynamic &&
                  rb.gravityScale == 0f &&
                  rb.constraints == RigidbodyConstraints2D.FreezeRotation);

            var box = player.GetComponent<BoxCollider2D>();
            Check("Player BoxCollider2D solid", box != null && !box.isTrigger);
        }

        var ground = GameObject.Find("Ground");
        var blockers = GameObject.Find("Blockers");
        Check("Ground tilemap", ground != null && ground.GetComponent<Tilemap>() != null);
        Check("Blockers tilemap", blockers != null && blockers.GetComponent<Tilemap>() != null);

        if (ground != null && blockers != null)
        {
            Check("Ground painted 1536 cells",
                  CountTiles(ground.GetComponent<Tilemap>()) == 1536);
            Check("Blockers painted 836 cells",
                  CountTiles(blockers.GetComponent<Tilemap>()) == 836);

            var br = blockers.GetComponent<TilemapRenderer>();
            Check("Blockers renderer disabled (invisible)", br != null && !br.enabled);

            var comp = blockers.GetComponent<CompositeCollider2D>();
            var tcol = blockers.GetComponent<TilemapCollider2D>();
            Check("CompositeCollider2D present", comp != null);
            Check("TilemapCollider2D wired to composite", tcol != null && tcol.usedByComposite);
            var brb = blockers.GetComponent<Rigidbody2D>();
            Check("Blockers body static",
                  brb != null && brb.bodyType == RigidbodyType2D.Static);
        }

        ValidateCamera(player);
        ValidateHud(player);
        ValidateRegionsAndGates();
        ValidateSorting();

        Debug.Log("[Validate] scene '" + scene.name + "': " + failures + " failure(s)");
        if (failures > 0)
            throw new System.Exception("[Validate] " + failures + " validation failure(s)");
        Debug.Log("[Validate] ALL CHECKS PASSED");
    }

    private static void ValidateCamera(GameObject player)
    {
        var camGo = GameObject.Find("Main Camera");
        Check("Main Camera tagged MainCamera", camGo != null && camGo.CompareTag("MainCamera"));
        if (camGo == null)
            return;

        var cam = camGo.GetComponent<Camera>();
        Check("Camera orthographic", cam != null && cam.orthographic);

        var rig = camGo.GetComponent<CameraRig>();
        Check("CameraRig present", rig != null);
        if (rig == null)
            return;

        Check("CameraRig targets the player",
              player != null && rig.target == player.transform);
        Check("CameraRig zoom band min < max", rig.minOrthoSize < rig.maxOrthoSize);
        Check("CameraRig world bounds 96x64",
              Mathf.Approximately(rig.worldBounds.width, 96f) &&
              Mathf.Approximately(rig.worldBounds.height, 64f));
        Check("CameraRig tracks mipmapped textures",
              rig.trackedTextures != null && rig.trackedTextures.Length > 0);
    }

    private static void ValidateHud(GameObject player)
    {
        var hud = Object.FindFirstObjectByType<HUDController>();
        Check("HUDController", hud != null);
        if (hud != null)
        {
            Check("HUD region label", hud.regionLabel != null);
            Check("HUD compass label", hud.compassLabel != null);
            Check("HUD zoom label", hud.zoomLabel != null);
            Check("HUD region panel CanvasGroup", hud.regionPanel != null);
            Check("HUD minimap caption", hud.minimapPositionLabel != null);
        }

        var mini = Object.FindFirstObjectByType<MinimapController>();
        Check("MinimapController", mini != null);
        if (mini != null)
        {
            Check("Minimap targets the player",
                  player != null && mini.player == player.transform);
            Check("Minimap camera assigned", mini.minimapCamera != null);
            Check("Minimap camera renders to a texture",
                  mini.minimapCamera != null && mini.minimapCamera.targetTexture != null);
            Check("RawImage shows that texture",
                  mini.mapImage != null && mini.renderTexture != null &&
                  mini.mapImage.texture == mini.renderTexture);
            Check("Minimap marker + arrow assigned",
                  mini.marker != null && mini.markerArrow != null);
            Check("Minimap map container assigned", mini.mapContainer != null);
            Check("Minimap is north-up", mini.northUp);
            Check("Minimap world bounds 96x64",
                  Mathf.Approximately(mini.worldBounds.width, 96f) &&
                  Mathf.Approximately(mini.worldBounds.height, 64f));
        }

        var dialogue = Object.FindFirstObjectByType<DialogueSystem>();
        Check("DialogueSystem", dialogue != null);
        if (dialogue != null)
        {
            Check("Dialogue panel + body text",
                  dialogue.panel != null && dialogue.bodyText != null);
            Check("Dialogue CanvasGroup", dialogue.canvasGroup != null);
        }

        Check("EventSystem present",
              Object.FindFirstObjectByType<EventSystem>() != null);
        Check("GameState present",
              Object.FindFirstObjectByType<GameState>() != null);
    }

    /// <summary>
    /// Regression guard for a bug that hid the player and every prop: SpriteYSort
    /// subtracted (y * step) from a base that was too small, so anything above
    /// y ~3.75 got a NEGATIVE sorting order and drew behind the terrain tilemap.
    /// </summary>
    private static void ValidateSorting()
    {
        int groundOrder = 0;
        var ground = GameObject.Find("Ground");
        var gr = ground != null ? ground.GetComponent<TilemapRenderer>() : null;
        if (gr != null)
            groundOrder = gr.sortingOrder;
        Check("Terrain tilemap sits at sorting order " + groundOrder, groundOrder == 0);

        var sorters = Object.FindObjectsByType<SpriteYSort>(FindObjectsSortMode.None);
        Check("SpriteYSort on sprites (" + sorters.Length + ")", sorters.Length > 100);

        int worst = int.MaxValue;
        foreach (var ys in sorters)
        {
            // Lowest order this component can produce anywhere on the map.
            int lowest = ys.baseOrder -
                         Mathf.CeilToInt(WorldBounds.yMax * ys.unitsPerStep);
            worst = Mathf.Min(worst, lowest);
        }
        Check("Worst-case Y-sort order (" + worst + ") stays above terrain (" +
              groundOrder + ")", worst > groundOrder);
    }

    private static void ValidateRegionsAndGates()
    {
        var regions = Object.FindObjectsByType<MapRegion>(FindObjectsSortMode.None);
        Check("Three MapRegions", regions.Length == 3);
        foreach (var r in regions)
        {
            var col = r.GetComponent<Collider2D>();
            Check("  region '" + r.regionName + "' has a trigger + a flag",
                  col != null && col.isTrigger && !string.IsNullOrEmpty(r.flagOnEnter));
        }

        var gates = Object.FindObjectsByType<RequirementGate>(FindObjectsSortMode.None);
        Check("Two RequirementGates", gates.Length == 2);
        foreach (var g in gates)
        {
            var col = g.GetComponent<Collider2D>();
            Check("  gate '" + g.name + "' has a trigger",
                  col != null && col.isTrigger);
            Check("  gate '" + g.name + "' has a barrier with a collider",
                  g.barrier != null && g.barrier.GetComponent<Collider2D>() != null);
            Check("  gate '" + g.name + "' has requirements",
                  g.requirements != null && g.requirements.Length > 0);
        }

        var sprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        Check("Props present (" + sprites.Length + " sprite renderers)",
              sprites.Length > 100);

        bool inBuild = false;
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path == OverworldScene && s.enabled)
                inBuild = true;
        Check("Overworld scene enabled in Build Settings", inBuild);
    }
}