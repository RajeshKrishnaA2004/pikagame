using UnityEngine;

/// <summary>
/// Zoomable follow camera for the overworld.
///
/// Replaces the old fixed-size CameraFollow. Keeps that script's look-ahead
/// feel but adds a clamped zoom band, world-bounds clamping so you can never
/// zoom or walk past the edge of the map, and a zoom-aware mipMapBias so
/// minified painted art does not shimmer.
///
/// Zoom is driven by: mouse wheel, +/- (equals/minus), or gamepad shoulder
/// buttons, and can also be set directly via SetZoom / ZoomBy.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    [Range(0.02f, 0.6f)] public float smoothTime = 0.16f;
    [Range(0f, 1.5f)] public float lookAheadDistance = 0.35f;
    [Range(0.02f, 0.6f)] public float lookAheadSmoothTime = 0.2f;

    [Header("Zoom")]
    [Tooltip("Smallest orthographic size (most zoomed in).")]
    public float minOrthoSize = 2.6f;
    [Tooltip("Largest orthographic size (most zoomed out).")]
    public float maxOrthoSize = 12f;
    [Tooltip("Multiplier applied per zoom notch.")]
    public float zoomStep = 1.12f;
    [Range(0.02f, 0.6f)] public float zoomSmoothTime = 0.14f;
    public float scrollSensitivity = 6f;

    [Header("World bounds (x/y = min, width/height = size)")]
    public bool clampToBounds = true;
    public Rect worldBounds = new Rect(0f, 0f, 96f, 64f);
    public float boundsPadding = 0.5f;

    [Header("Sampling quality")]
    [Tooltip("Textures whose mipMapBias is driven by how far we are zoomed.")]
    public Texture2D[] trackedTextures;
    public bool dynamicMipBias = true;
    [Tooltip("Pixels-per-unit of trackedTextures, used for the bias maths.")]
    public float trackedPpu = 256f;
    public float staticMipBias = -0.5f;
    public float mipBiasClampMin = -1f;
    public float mipBiasClampMax = 1f;

    [Header("Pixel snapping")]
    [Tooltip("Snap the camera to whole screen pixels. Leave OFF for smooth painted art.")]
    public bool pixelSnap = false;

    private Camera cam;
    private Vector3 camVelocity;
    private Vector2 lookAhead;
    private Vector2 lookAheadVelocity;
    private Vector3 lastTargetPosition;
    private float targetOrtho;
    private float orthoVelocity;
    private float lastMipBias = float.NaN;

    /// <summary>Current orthographic size.</summary>
    public float Zoom { get { return cam != null ? cam.orthographicSize : minOrthoSize; } }

    /// <summary>0 = fully zoomed in, 1 = fully zoomed out.</summary>
    public float ZoomRatio01
    {
        get
        {
            float span = Mathf.Max(0.0001f, maxOrthoSize - minOrthoSize);
            return Mathf.Clamp01((Zoom - minOrthoSize) / span);
        }
    }

    /// <summary>Screen pixels per world unit at the current zoom.</summary>
    public float ScreenPixelsPerUnit
    {
        get
        {
            float h = cam != null ? cam.pixelHeight : Screen.height;
            return h / (2f * Mathf.Max(0.0001f, Zoom));
        }
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        targetOrtho = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
        cam.orthographicSize = targetOrtho;

        if (target != null)
        {
            lastTargetPosition = target.position;
            Vector3 p = Snap(target.position);
            p.z = transform.position.z - 10f;
            transform.position = ClampToBounds(p);
        }
    }

    void Update()
    {
        HandleZoomInput();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // ---- look-ahead in the direction of travel -------------------------
        Vector3 current = target.position;
        Vector2 velocity = (current - lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastTargetPosition = current;

        Vector2 wantedLookAhead = Vector2.ClampMagnitude(velocity * 0.08f, lookAheadDistance);
        lookAhead = Vector2.SmoothDamp(lookAhead, wantedLookAhead, ref lookAheadVelocity,
                                       lookAheadSmoothTime);

        // ---- zoom ----------------------------------------------------------
        targetOrtho = Mathf.Clamp(targetOrtho, minOrthoSize, maxOrthoSize);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetOrtho,
                                                ref orthoVelocity, zoomSmoothTime);

        // ---- follow --------------------------------------------------------
        Vector3 desired = new Vector3(current.x + lookAhead.x, current.y + lookAhead.y,
                                      transform.position.z);
        Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref camVelocity,
                                          smoothTime);
        next.z = transform.position.z;
        transform.position = ClampToBounds(next);

        ApplyMipBias();
    }

    void HandleZoomInput()
    {
        float delta = 0f;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            delta -= scroll * scrollSensitivity;

        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) ||
            Input.GetKey(KeyCode.JoystickButton5))
            delta -= Time.deltaTime * 6f;
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) ||
            Input.GetKey(KeyCode.JoystickButton4))
            delta += Time.deltaTime * 6f;

        if (Mathf.Abs(delta) > 0.0001f)
            ZoomBy(delta);
    }

    /// <summary>Multiply the zoom target by zoomStep^notches (positive = out).</summary>
    public void ZoomBy(float notches)
    {
        SetZoom(targetOrtho * Mathf.Pow(zoomStep, notches));
    }

    /// <summary>Set an absolute orthographic size, clamped to the band.</summary>
    public void SetZoom(float orthoSize)
    {
        targetOrtho = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
    }

    /// <summary>Snap instantly (used when loading a scene or a zoom preset).</summary>
    public void SetZoomImmediate(float orthoSize)
    {
        SetZoom(orthoSize);
        if (cam != null)
            cam.orthographicSize = targetOrtho;
    }

    Vector3 ClampToBounds(Vector3 p)
    {
        if (!clampToBounds || cam == null)
            return p;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = worldBounds.xMin + boundsPadding;
        float maxX = worldBounds.xMax - boundsPadding;
        float minY = worldBounds.yMin + boundsPadding;
        float maxY = worldBounds.yMax - boundsPadding;

        // If the view is wider/taller than the world we centre that axis
        // instead of clamping, otherwise the camera would jitter at the edges.
        p.x = (maxX - minX) <= halfW * 2f
            ? (minX + maxX) * 0.5f
            : Mathf.Clamp(p.x, minX + halfW, maxX - halfW);

        p.y = (maxY - minY) <= halfH * 2f
            ? (minY + maxY) * 0.5f
            : Mathf.Clamp(p.y, minY + halfH, maxY - halfH);

        return p;
    }

    void ApplyMipBias()
    {
        if (trackedTextures == null || trackedTextures.Length == 0)
            return;

        float bias = staticMipBias;
        if (dynamicMipBias)
        {
            // screenPxPerTexel > 1 => magnifying => want the sharpest mip
            // screenPxPerTexel < 1 => minifying  => want blurrier mips (no shimmer)
            float screenPxPerTexel = ScreenPixelsPerUnit / Mathf.Max(1f, trackedPpu);
            bias = Mathf.Clamp(Mathf.Log(screenPxPerTexel, 2f) - 0.5f,
                               mipBiasClampMin, mipBiasClampMax);
        }

        if (Mathf.Abs(bias - lastMipBias) < 0.02f)
            return;
        lastMipBias = bias;

        for (int i = 0; i < trackedTextures.Length; i++)
        {
            if (trackedTextures[i] != null)
                trackedTextures[i].mipMapBias = bias;
        }
    }

    Vector3 Snap(Vector3 p)
    {
        if (!pixelSnap)
            return p;
        float ppu = Mathf.Max(1f, ScreenPixelsPerUnit);
        p.x = Mathf.Round(p.x * ppu) / ppu;
        p.y = Mathf.Round(p.y * ppu) / ppu;
        return p;
    }

    void OnDrawGizmosSelected()
    {
        if (!clampToBounds)
            return;
        Gizmos.color = new Color(0.3f, 0.9f, 0.7f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(worldBounds.center.x, worldBounds.center.y, 0f),
                            new Vector3(worldBounds.width, worldBounds.height, 0.1f));
    }
}