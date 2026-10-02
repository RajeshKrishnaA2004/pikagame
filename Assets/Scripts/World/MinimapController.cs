using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Circular minimap.
///
/// Two ways to fill the circle:
///   WholeWorld : a second orthographic camera frames the entire map; the
///                marker moves around the circle so you can read where you are
///                north/south of the middle of the world.
///   Follow     : the camera follows the player and the marker stays centred.
///
/// The world stays north-up by default (north = top of the circle), which is
/// what makes the N/S reading trustworthy. Set northUp = false to rotate the
/// map with the player instead.
///
/// Setup: create a "Minimap" layer, put the world tilemap/props on it, assign
/// that layer to `minimapCullingMask`, and point a second camera at it.
/// </summary>
public class MinimapController : MonoBehaviour
{
    public enum Mode { WholeWorld, Follow }

    public static MinimapController Instance { get; private set; }

    [Header("References")]
    public Transform player;
    public PlayerController playerController;
    public Camera minimapCamera;
    public RenderTexture renderTexture;
    [Tooltip("The RawImage that shows the render texture.")]
    public RawImage mapImage;
    [Tooltip("RectTransform holding the map image AND the marker; rotated when northUp is off.")]
    public RectTransform mapContainer;
    [Tooltip("Marker transform; moved around the circle in WholeWorld mode.")]
    public RectTransform marker;
    [Tooltip("Arrow child of the marker; rotated to the player's facing.")]
    public RectTransform markerArrow;

    [Header("World")]
    [Tooltip("x/y = min corner, width/height = size, in world units.")]
    public Rect worldBounds = new Rect(0f, 0f, 96f, 64f);
    [Tooltip("Layer mask rendered by the minimap camera.")]
    public LayerMask minimapCullingMask = ~0;
    public Mode mode = Mode.WholeWorld;
    [Tooltip("Extra margin so the whole world fits inside the circle.")]
    public float fitPadding = 1.06f;
    [Tooltip("World-units radius shown in Follow mode.")]
    public float followRadius = 12f;

    [Header("Circle")]
    public bool northUp = true;
    [Tooltip("Half the minimap diameter, in canvas pixels.")]
    public float circleRadiusPixels = 96f;
    [Tooltip("Keep the marker inside the circle instead of letting it clip out.")]
    public bool clampMarkerToCircle = true;

    /// <summary>Raised when the player's compass direction changes.</summary>
    public event Action<string> CardinalChanged;

    private string lastCardinal = "";

    /// <summary>"N", "S", "E", "W" ... based on the player's facing.</summary>
    public string Cardinal
    {
        get { return playerController != null ? playerController.Cardinal : "S"; }
    }

    /// <summary>"N", "Center" or "S" -- the player's north/south position in the world.</summary>
    public string VerticalPosition
    {
        get
        {
            if (player == null || worldBounds.height <= 0.0001f)
                return "Center";

            float v = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, player.position.y);
            if (v > 0.58f) return "N";
            if (v < 0.42f) return "S";
            return "Center";
        }
    }

    /// <summary>Player position as 0..1 across the world bounds.</summary>
    public Vector2 NormalisedPosition
    {
        get
        {
            if (player == null)
                return new Vector2(0.5f, 0.5f);

            return new Vector2(
                worldBounds.width > 0.0001f
                    ? Mathf.Clamp01((player.position.x - worldBounds.xMin) / worldBounds.width)
                    : 0.5f,
                worldBounds.height > 0.0001f
                    ? Mathf.Clamp01((player.position.y - worldBounds.yMin) / worldBounds.height)
                    : 0.5f);
        }
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        ConfigureCamera();
    }

    void LateUpdate()
    {
        UpdateCamera();
        UpdateMarker();
        UpdateCardinal();
    }

    /// <summary>Frame the world (WholeWorld) or the player (Follow).</summary>
    private void ConfigureCamera()
    {
        if (minimapCamera == null)
            return;

        minimapCamera.orthographic = true;
        minimapCamera.cullingMask = minimapCullingMask;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 1f);
        minimapCamera.nearClipPlane = -100f;
        minimapCamera.farClipPlane = 100f;

        if (renderTexture != null)
            minimapCamera.targetTexture = renderTexture;

        if (mapImage != null && renderTexture != null)
            mapImage.texture = renderTexture;
    }

    private void UpdateCamera()
    {
        if (minimapCamera == null)
            return;

        if (mode == Mode.WholeWorld)
        {
            // A square render target shows 2*ortho units; use the larger world axis.
            float extent = Mathf.Max(worldBounds.width, worldBounds.height) * 0.5f;
            minimapCamera.orthographicSize = Mathf.Max(0.01f, extent * fitPadding);
            minimapCamera.transform.position = new Vector3(
                worldBounds.center.x, worldBounds.center.y, -10f);
        }
        else
        {
            minimapCamera.orthographicSize = Mathf.Max(0.01f, followRadius);
            Vector3 p = player != null ? player.position : worldBounds.center;
            minimapCamera.transform.position = new Vector3(p.x, p.y, -10f);
        }
    }

    private void UpdateMarker()
    {
        if (marker == null)
            return;

        if (mode == Mode.WholeWorld)
        {
            Vector2 n = NormalisedPosition;
            Vector2 pos = new Vector2((n.x - 0.5f) * 2f, (n.y - 0.5f) * 2f) *
                          circleRadiusPixels;

            if (clampMarkerToCircle)
            {
                float maxR = circleRadiusPixels * 0.88f;
                if (pos.sqrMagnitude > maxR * maxR)
                    pos = pos.normalized * maxR;
            }
            marker.anchoredPosition = pos;
        }
        else
        {
            marker.anchoredPosition = Vector2.zero;
        }

        // Rotate the arrow to show facing. Unity UI positive Z turns CCW and
        // PlayerController.FacingAngle is already mirrored for that, so a
        // south-facing player points the arrow down the screen.
        if (markerArrow != null && playerController != null)
            markerArrow.localRotation = Quaternion.Euler(0f, 0f, playerController.FacingAngle);

        // Rotate the whole disc when "player up" is wanted instead of north-up.
        if (mapContainer != null)
            mapContainer.localRotation = Quaternion.Euler(
                0f, 0f, (northUp || playerController == null) ? 0f : playerController.FacingAngle);
    }

    private void UpdateCardinal()
    {
        string card = Cardinal;
        if (card == lastCardinal)
            return;
        lastCardinal = card;
        if (CardinalChanged != null)
            CardinalChanged(card);
    }

    /// <summary>Convert a world point to canvas space inside the circle.</summary>
    public Vector2 WorldToCircle(Vector2 world)
    {
        if (mode == Mode.WholeWorld)
        {
            float u = worldBounds.width > 0.0001f
                ? (world.x - worldBounds.xMin) / worldBounds.width : 0.5f;
            float v = worldBounds.height > 0.0001f
                ? (world.y - worldBounds.yMin) / worldBounds.height : 0.5f;
            return new Vector2((u - 0.5f) * 2f, (v - 0.5f) * 2f) * circleRadiusPixels;
        }

        if (player == null)
            return Vector2.zero;

        Vector2 rel = world - (Vector2)player.position;
        float scale = circleRadiusPixels / Mathf.Max(0.01f, followRadius);
        return rel * scale;
    }
}