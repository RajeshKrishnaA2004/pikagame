using UnityEngine;

/// <summary>
/// Locked, non-zooming, pixel-snapped camera for the tile scenes.
///
/// Replaces <see cref="CameraRig"/> (which owns a zoom band, smoothing and a
/// mip-map bias - none of which apply here). Behaviour:
///
///   * orthographic size 4.5 => 9 tiles tall, always
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

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();

        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.nearClipPlane = -100f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 p = target.position;

        float halfH = orthoSize;
        float halfW = orthoSize * Mathf.Max(0.01f, cam.aspect);

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
