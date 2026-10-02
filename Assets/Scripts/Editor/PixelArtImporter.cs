using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pixel-art importer for the hand-made pack at <see cref="PackRoot"/>.
///
/// This is deliberately SEPARATE from <c>ProjectSetup.ApplyImportSettings</c>,
/// which targets the old painted art and enables mipmaps + bilinear filtering.
/// Those settings smear crisp pixel art, so this importer applies the opposite
/// profile: Point filtering, no mipmaps, no anisotropy, no compression.
///
/// Scope: ONLY <c>Assets/Art/_Mine/Sprites</c>. Nothing else is touched, and
/// <c>ProjectSetup.ApplyImportSettings</c> skips this folder.
///
/// PPU is 64 everywhere so that a 64x64 tile occupies exactly 1 Unity unit.
/// That is a fixed design decision - do not change it per-folder.
/// </summary>
public static class PixelArtImporter
{
    public const string PackRoot = "Assets/Art/_Mine/Sprites";

    /// <summary>Fixed: 64 px = 1 world unit = 1 tile.</summary>
    public const float PixelsPerUnit = 64f;

    private const int MaxTextureSize = 2048;

    // 9-slice borders (left, right, bottom, top) so Image.Type.Sliced stretches
    // cleanly. Kept well inside each frame's decorative edge.
    private static readonly Vector4 DialogBoxBorder = new Vector4(40f, 40f, 40f, 40f);
    private static readonly Vector4 MenuPanelBorder = new Vector4(48f, 48f, 48f, 48f);
    private static readonly Vector4 StatusBarBorder = new Vector4(24f, 24f, 20f, 20f);

    [MenuItem("Tools/BorrowedTime/1. Import Art Pack (_Mine)")]
    public static void ImportArtPack()
    {
        if (!Directory.Exists(PackRoot))
        {
            Debug.LogError("[BorrowedTime] art pack not found at " + PackRoot);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PackRoot });
        int changed = 0, failed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                failed++;
                continue;
            }

            ApplyProfile(path, importer);
            importer.SaveAndReimport();
            changed++;
        }

        Debug.Log(string.Format(
            "[BorrowedTime] art pack imported: {0} textures ({1} failed) from {2}",
            changed, failed, PackRoot));
    }

    private static void ApplyProfile(string path, TextureImporter importer)
    {
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        string file = Path.GetFileName(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;

        // CRISP: point sampling, no mip chain, no anisotropy, no compression.
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.anisoLevel = 0;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.maxTextureSize = MaxTextureSize;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;

        // Pivot: everything that stands on the ground is bottom-centre so it
        // can be positioned by its feet; ground tiles are centre; UI is centre.
        importer.spritePivot = ChoosePivot(dir);

        importer.spriteBorder = ChooseBorder(dir, file);
    }

    private static Vector2 ChoosePivot(string dir)
    {
        if (dir.EndsWith("/Environment/Tiles"))
            return new Vector2(0.5f, 0.5f);           // tiles must be centre-pivoted

        if (dir.EndsWith("/Player") || dir.EndsWith("/GymLeader") ||
            dir.EndsWith("/Creatures") ||
            dir.EndsWith("/Environment/Trees") ||
            dir.EndsWith("/Environment/Buildings") ||
            dir.EndsWith("/Environment/Props"))
            return new Vector2(0.5f, 0.0f);           // anchor at the feet/base

        return new Vector2(0.5f, 0.5f);               // UI + backgrounds
    }

    private static Vector4 ChooseBorder(string dir, string file)
    {
        if (!dir.EndsWith("/UI"))
            return Vector4.zero;

        switch (file)
        {
            case "DialogBox.png": return DialogBoxBorder;
            case "MenuPanel.png": return MenuPanelBorder;
            case "StatusBar_Diamond.png":
            case "StatusBar_Round.png": return StatusBarBorder;
            default: return Vector4.zero;
        }
    }
}
