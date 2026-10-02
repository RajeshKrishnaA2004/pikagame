using UnityEngine;

/// <summary>
/// Locked, non-zooming, pixel-snapped camera for the tile scenes.
///
/// Replaces <see cref="CameraRig"/> (which owns a zoom band, smoothing and a
/// mip-map bias - none of which apply here). Behaviour:
///
///   * orthographic size 4.5 => 9 tiles tall, always
///   * letterboxed to 10 x 9 tiles (10:9); wider screens get black side bars
///   * follows the target EXACTLY (no damping, no smoothing, no look-ahead)
///   * clamped so the view never shows outside the map rectangle
///   * position snapped to the 1/PPU grid so tile edges stay on texel
///   * no zoom, ever
/// </summary>
[RequireComponent(typeof(Camera))]
public class LockedCamera : MonoBehaviour
{
    [Tooltip("Player transform to follow.")]
    public Transform target;

    [Tooltip("Half-height in world units. 4.5 with 1 unit/tile = 9 tiles tall.")]
    public float orthoSize = 4.5f;

    [Tooltip("Map rectangle in world units, used for clamping.")]
    public Rect mapBounds = new Rect(0f, 0f, 24f, 18f);

    [Tooltip("Snap the camera position to the texel grid.")]
    public bool pixelSnap = true;

    [Tooltip("Must match the pack's PPU (64).")]
    public float pixelsPerUnit = 64f;

    [Tooltip("Visible area as width:height in TILES. 10/9 with orthoSize 4.5 " +
             "shows 10 x 9 tiles instead of 16 x 9.")]
    public float tileAspect = 10f / 9f;

    [Tooltip("Colour of the bars on screens wider than the target aspect.")]
    public Color letterboxColor = Color.black;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();

        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.nearClipPlane = -100f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = letterboxColor;         // black letterbox
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;

        ApplyLetterbox();
    }

    /// <summary>
    /// Shrink the camera viewport to the target aspect, centred, so anything
    /// wider than 10:9 gets black bars at the sides instead of extra map.
    /// Re-applied every frame because Screen can change (window resize, Game
    /// view aspect presets).
    /// </summary>
    private void ApplyLetterbox()
    {
        if (Screen.height <= 0 || Screen.width <= 0)
            return;

        float screenAspect = (float)Screen.width / Screen.height;
        float w = tileAspect / screenAspect;
        float h = 1f;
        if (w > 1f)                       // screen narrower than target: bar top/bottom
        {
            h = 1f / w;
            w = 1f;
        }
        cam.rect = new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
    }

    /// <summary>Aspect actually rendered: the target, or the screen if it is narrower.</summary>
    public float EffectiveAspect
    {
        get
        {
            if (Screen.height <= 0 || Screen.width <= 0)
                return tileAspect;
            float screenAspect = (float)Screen.width / Screen.height;
            return screenAspect < tileAspect ? screenAspect : tileAspect;
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        ApplyLetterbox();

        Vector3 p = target.position;

        float halfH = orthoSize;
        float halfW = orthoSize * EffectiveAspect;

        // Clamp per axis. If the view is wider/taller than the map, centre it
        // on that axis instead of clamping (otherwise it would jitter at the
        // edge where min > max).
        float minX = mapBounds.xMin + halfW;
        float maxX = mapBounds.xMax - halfW;
        p.x = minX <= maxX ? Mathf.Clamp(p.x, minX, maxX) : mapBounds.center.x;

        float minY = mapBounds.yMin + halfH;
        float maxY = mapBounds.yMax - halfH;
        p.y = minY <= maxY ? Mathf.Clamp(p.y, minY, maxY) : mapBounds.center.y;

        if (pixelSnap && pixelsPerUnit > 0f)
        {
            float s = 1f / pixelsPerUnit;
            p.x = Mathf.Round(p.x / s) * s;
            p.y = Mathf.Round(p.y / s) * s;
        }

        p.z = -10f;
        transform.position = p;
    }

    /// <summary>Used by the builder and the validator to configure without Awake.</summary>
    public void Configure(Transform follow, Rect bounds, float size)
    {
        target = follow;
        mapBounds = bounds;
        orthoSize = size;
    }
}
