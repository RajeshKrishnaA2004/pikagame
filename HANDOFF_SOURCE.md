# HANDOFF_SOURCE - full source for pikagame-main

> Companion to `HANDOFF.md`. Every runtime script, legacy script and
> Python tool, in reading order. Generated; regenerate rather than edit.

---

## Assets\Scripts\Core\CreatureType.cs

_44 lines_

```csharp
/// <summary>
/// Creature types. These follow the design document (Borrowed Time), which
/// uses Flame / Tide / Leaf / Stone / Spark rather than the classic
/// Fire / Water / Grass set.
/// </summary>
public enum CreatureType
{
    None = 0,
    Flame = 1,
    Tide = 2,
    Leaf = 3,
    Stone = 4,
    Spark = 5
}

public static class CreatureTypeUtil
{
    public static string DisplayName(CreatureType t)
    {
        switch (t)
        {
            case CreatureType.Flame: return "Flame";
            case CreatureType.Tide: return "Tide";
            case CreatureType.Leaf: return "Leaf";
            case CreatureType.Stone: return "Stone";
            case CreatureType.Spark: return "Spark";
            default: return "None";
        }
    }

    /// <summary>Parse a type name (case/space insensitive).</summary>
    public static CreatureType Parse(string s)
    {
        if (string.IsNullOrEmpty(s))
            return CreatureType.None;
        switch (s.Trim().ToLowerInvariant())
        {
            case "flame": case "fire": return CreatureType.Flame;
            case "tide": case "water": return CreatureType.Tide;
            case "leaf": case "grass": return CreatureType.Leaf;
            case "stone": case "rock": case "ground": return CreatureType.Stone;
            case "spark": case "electric": return CreatureType.Spark;
            default: return CreatureType.None;
        }
    }
}
```

---

## Assets\Scripts\Core\GameState.cs

_207 lines_

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for world progress: flags, the party's creature
/// types, inventory items and upgrades.
///
/// Deliberately small. It exists so the world layer (gates, regions, the HUD)
/// can ask "does the player satisfy this?" without depending on the full
/// battle/party systems, which are still to be built. Replace the party and
/// inventory members with the real systems later; the query API stays.
///
/// Persisted through PlayerPrefs so a gate opened in one session stays open.
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    private const string Prefix = "bt.";

    private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<CreatureType> partyTypes = new List<CreatureType>();
    private readonly Dictionary<string, int> items = new Dictionary<string, int>(StringComparer.Ordinal);

    [Tooltip("Write state to PlayerPrefs whenever it changes.")]
    public bool persist = true;

    [Tooltip("Starting creature type for a fresh save (mirrors the starter pick).")]
    public CreatureType startingCreature = CreatureType.None;

    public int Upgrades { get; private set; }
    public int GymsCleared { get; private set; }

    /// <summary>Raised whenever a flag is set or cleared.</summary>
    public event Action<string, bool> FlagChanged;
    /// <summary>Raised whenever the party or inventory changes.</summary>
    public event Action RosterChanged;

    public ICollection<string> Flags { get { return flags; } }
    public IList<CreatureType> PartyTypes { get { return partyTypes; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------ flags

    public bool HasFlag(string flag)
    {
        return !string.IsNullOrEmpty(flag) && flags.Contains(flag);
    }

    public void SetFlag(string flag, bool value = true)
    {
        if (string.IsNullOrEmpty(flag))
            return;

        bool changed = value ? flags.Add(flag) : flags.Remove(flag);
        if (!changed)
            return;

        Save();
        if (FlagChanged != null)
            FlagChanged(flag, value);
    }

    public void ClearFlag(string flag)
    {
        SetFlag(flag, false);
    }

    // ----------------------------------------------------------- party / items

    public bool HasCreatureType(CreatureType type)
    {
        return partyTypes.Contains(type);
    }

    public int CountCreatureType(CreatureType type)
    {
        int n = 0;
        for (int i = 0; i < partyTypes.Count; i++)
            if (partyTypes[i] == type)
                n++;
        return n;
    }

    public void AddCreature(CreatureType type)
    {
        if (type == CreatureType.None)
            return;
        partyTypes.Add(type);
        Save();
        if (RosterChanged != null)
            RosterChanged();
    }

    public bool RemoveCreature(CreatureType type)
    {
        bool removed = partyTypes.Remove(type);
        if (removed)
        {
            Save();
            if (RosterChanged != null)
                RosterChanged();
        }
        return removed;
    }

    public int ItemCount(string itemId)
    {
        int n;
        return !string.IsNullOrEmpty(itemId) && items.TryGetValue(itemId, out n) ? n : 0;
    }

    public void AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount == 0)
            return;
        int total = ItemCount(itemId) + amount;
        if (total <= 0)
            items.Remove(itemId);
        else
            items[itemId] = total;
        Save();
        if (RosterChanged != null)
            RosterChanged();
    }

    public bool ConsumeItem(string itemId, int amount = 1)
    {
        if (ItemCount(itemId) < amount)
            return false;
        AddItem(itemId, -amount);
        return true;
    }

    // ------------------------------------------------------------ progression

    public void SetUpgrades(int value)
    {
        Upgrades = Mathf.Max(0, value);
        Save();
    }

    public void SetGymsCleared(int value)
    {
        GymsCleared = Mathf.Max(0, value);
        Save();
    }

    // ------------------------------------------------------------ persistence

    public void Save()
    {
        if (!persist)
            return;

        PlayerPrefs.SetString(Prefix + "flags", string.Join("|", ToArray(flags)));

        var types = new List<string>();
        foreach (CreatureType t in partyTypes)
            types.Add(t.ToString());
        PlayerPrefs.SetString(Prefix + "party", string.Join("|", types.ToArray()));

        var kv = new List<string>();
        foreach (KeyValuePair<string, int> pair in items)
            kv.Add(pair.Key + "=" + pair.Value);
        PlayerPrefs.SetString(Prefix + "items", string.Join("|", kv.ToArray()));

        PlayerPrefs.SetInt(Prefix + "upgrades", Upgrades);
        PlayerPrefs.SetInt(Prefix + "gyms", GymsCleared);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        flags.Clear();
        partyTypes.Clear();
        items.Clear();

        foreach (string s in Split(PlayerPrefs.GetString(Prefix + "flags", "")))
            flags.Add(s);

        foreach (string s in Split(PlayerPrefs.GetString(Prefix + "party", "")))
        {
            CreatureType t = CreatureTypeUtil.Parse(s);
            if (t != CreatureType.None)
                partyTypes.Add(t);
        }

        foreach (string s in Split(PlayerPrefs.GetString(Prefix + "items", "")))
        {
            int eq = s.IndexOf('=');
            if (eq <= 0)
                continue;
            int n;
            if (int.TryParse(s.Substring(eq + 1), out n))
                items[s.Substring(0, eq)] = n;
        }

        Upgrades = PlayerPrefs.GetInt(Prefix + "upgrades", 0);
        GymsCleared = PlayerPrefs.GetInt(Prefix + "gyms", 0);

        if (partyTypes.Count == 0 && startingCreature != CreatureType.None)
            partyTypes.Add(startingCreature);
    }

    /// <summary>Wipe all progress (dev helper and "new game").</summary>
    public void ResetAll()
    {
        flags.Clear();
        partyTypes.Clear();
        items.Clear();
        Upgrades = 0;
        GymsCleared = 0;

        if (startingCreature != CreatureType.None)
            partyTypes.Add(startingCreature);

        Save();
    }

    private static string[] ToArray(ICollection<string> set)
    {
        var a = new string[set.Count];
        set.CopyTo(a, 0);
        return a;
    }

    private static string[] Split(string s)
    {
        if (string.IsNullOrEmpty(s))
            return new string[0];
        return s.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
```

---

## Assets\Scripts\Player\PlayerController.cs

_148 lines_

```csharp
using UnityEngine;

/// <summary>
/// Top-down player movement.
///
/// Replaces <see cref="PlayerMovement"/>: same 4-way input but it also tracks
/// facing (for the walk animation and the minimap arrow), exposes an Instance
/// for other systems, and can be frozen while a message box is up.
///
/// Movement is 8-directional, but facing snaps to one of four cardinal
/// directions so the sprite rows line up (Down, Left, Right, Up).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    public float moveSpeed = 4.5f;
    [Tooltip("Multiplier while the run button (Left Shift) is held.")]
    public float runMultiplier = 1.6f;
    [Tooltip("Ignore input while true. Set by dialogue.")]
    public bool frozen = false;

    [Header("Facing")]
    [Tooltip("Dead-zone: how much a diagonal must win by before facing flips.")]
    public float facingBias = 1.08f;

    private Rigidbody2D body;
    private Vector2 movement;
    private Vector2 facing = Vector2.down;
    private float speedScale = 1f;

    // 0 = Down, 1 = Left, 2 = Right, 3 = Up  (matches the sprite sheet rows)
    private int facingIndex;

    /// <summary>Last non-zero movement direction, normalised.</summary>
    public Vector2 Facing { get { return facing; } }

    /// <summary>0 = Down, 1 = Left, 2 = Right, 3 = Up.</summary>
    public int FacingIndex { get { return facingIndex; } }

    /// <summary>True on frames where the player is actually moving.</summary>
    public bool IsMoving { get { return movement.sqrMagnitude > 0.0001f && !frozen; } }

    /// <summary>Current movement input (unscaled by speed).</summary>
    public Vector2 Movement { get { return movement; } }

    /// <summary>Angle in degrees for the minimap arrow (0 = north).</summary>
    public float FacingAngle
    {
        get { return Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg * -1f; }
    }

    /// <summary>"N", "NE", "E" ... for the minimap readout.</summary>
    public string Cardinal
    {
        get
        {
            if (facing.sqrMagnitude < 0.0001f)
                return "S";

            float angle = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            angle = Mathf.Repeat(angle + 360f, 360f);

            if (angle >= 337.5f || angle < 22.5f) return "N";
            if (angle < 67.5f) return "NE";
            if (angle < 112.5f) return "E";
            if (angle < 157.5f) return "SE";
            if (angle < 202.5f) return "S";
            if (angle < 247.5f) return "SW";
            if (angle < 292.5f) return "W";
            return "NW";
        }
    }

    void Awake()
    {
        Instance = this;
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (frozen)
        {
            movement = Vector2.zero;
            return;
        }

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        movement = new Vector2(x, y);
        if (movement.sqrMagnitude > 1f)
            movement.Normalize();

        UpdateFacing(x, y);

        speedScale = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            ? Mathf.Max(0.1f, runMultiplier)
            : 1f;
    }

    void FixedUpdate()
    {
        if (body == null)
            return;

        if (frozen)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        body.MovePosition(body.position + movement * (moveSpeed * speedScale) *
                                          Time.fixedDeltaTime);
    }

    private void UpdateFacing(float x, float y)
    {
        if (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f)
            return;

        if (Mathf.Abs(x) * facingBias > Mathf.Abs(y))
            facingIndex = x > 0f ? 2 : 1;          // Right : Left
        else if (Mathf.Abs(y) > Mathf.Abs(x) * facingBias)
            facingIndex = y > 0f ? 3 : 0;          // Up : Down
        // else: keep the previous facing on a true diagonal

        switch (facingIndex)
        {
            case 0: facing = Vector2.down; break;
            case 1: facing = Vector2.left; break;
            case 2: facing = Vector2.right; break;
            default: facing = Vector2.up; break;
        }
    }

    /// <summary>Teleport without physics artefacts (used when entering a scene).</summary>
    public void Teleport(Vector2 position)
    {
        if (body != null)
            body.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    /// <summary>Face a direction without moving (used by cutscenes).</summary>
    public void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            facingIndex = direction.x > 0f ? 2 : 1;
        else
            facingIndex = direction.y > 0f ? 3 : 0;

        switch (facingIndex)
        {
            case 0: facing = Vector2.down; break;
            case 1: facing = Vector2.left; break;
            case 2: facing = Vector2.right; break;
            default: facing = Vector2.up; break;
        }
    }
}
```

---

## Assets\Scripts\Player\PlayerAnimator.cs

_96 lines_

```csharp
using UnityEngine;

/// <summary>
/// Drives the player's walk animation directly from a sliced sprite sheet.
///
/// Why not an Animator/AnimationClip setup? Because the sheet is generated by
/// Tools/GenArt.py and sliced by Tools' editor step; keeping the frames as a
/// plain Sprite[] means there is no .controller asset to keep in sync and the
/// animation cannot silently break if a clip is reimported.
///
/// `frames` is laid out as [direction * framesPerDirection + frame], where
/// direction is 0 = Down, 1 = Left, 2 = Right, 3 = Up.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("16 entries: 4 rows (Down,Left,Right,Up) x 4 walk frames.")]
    public Sprite[] frames = new Sprite[0];
    public int framesPerDirection = 4;

    [Header("Idle")]
    [Tooltip("Which frame index to hold while standing still.")]
    public int idleFrame = 0;

    [Header("Timing")]
    [Tooltip("Walk frames per second.")]
    public float walkFps = 8f;
    [Tooltip("Extra playback speed while running.")]
    public float runFpsMultiplier = 1.5f;

    [Header("Bobbing (optional)")]
    [Tooltip("Vertical bob amplitude in world units while walking. 0 disables.")]
    public float bobAmplitude = 0f;
    public float bobFps = 8f;

    private SpriteRenderer spriteRenderer;
    private PlayerController controller;
    private int direction;
    private float timer;
    private int frame;
    private Vector3 baseLocalPosition;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        controller = GetComponentInParent<PlayerController>();
        if (controller == null)
            controller = PlayerController.Instance;
        baseLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (spriteRenderer == null)
            return;

        if (controller != null)
            direction = Mathf.Clamp(controller.FacingIndex, 0, 3);

        bool moving = controller != null && controller.IsMoving;
        bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float fps = Mathf.Max(0.01f, walkFps * (running ? runFpsMultiplier : 1f));

        if (moving && framesPerDirection > 0)
        {
            timer += Time.deltaTime * fps;
            int span = Mathf.Max(1, framesPerDirection);
            while (timer >= 1f)
            {
                timer -= 1f;
                frame = (frame + 1) % span;
            }
        }
        else
        {
            timer = 0f;
            frame = Mathf.Clamp(idleFrame, 0, Mathf.Max(0, framesPerDirection - 1));
        }

        ApplySprite();

        if (bobAmplitude > 0f)
        {
            float phase = moving
                ? Mathf.Abs(Mathf.Sin(Time.time * bobFps * Mathf.PI)) * bobAmplitude
                : 0f;
            transform.localPosition = baseLocalPosition + new Vector3(0f, phase, 0f);
        }
    }

    private void ApplySprite()
    {
        if (frames == null || frames.Length == 0)
            return;

        int index = direction * Mathf.Max(1, framesPerDirection) + frame;
        if (index < 0 || index >= frames.Length)
            index = Mathf.Clamp(index, 0, frames.Length - 1);

        Sprite wanted = frames[index];
        if (wanted != null && spriteRenderer.sprite != wanted)
            spriteRenderer.sprite = wanted;
    }

    /// <summary>Assign a whole sheet's frames at once (used by the editor builder).</summary>
    public void SetFrames(Sprite[] newFrames, int perDirection)
    {
        frames = newFrames;
        framesPerDirection = Mathf.Max(1, perDirection);
    }
}
```

---

## Assets\Scripts\World\CameraRig.cs

_208 lines_

```csharp
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
```

---

## Assets\Scripts\World\RequirementGate.cs

_226 lines_

```csharp
using UnityEngine;

/// <summary>
/// Blocks a path until the player satisfies a set of requirements.
///
/// Setup on a path tile:
///   1. Put this component on an empty GameObject at the choke point.
///   2. Add a trigger BoxCollider2D ("Is Trigger") sized to roughly one tile
///      so the gate can detect a player standing in the doorway.
///   3. Add a child GameObject with a NON-trigger BoxCollider2D and the
///      barrier artwork (Assets/Art/World/gate.png). This child is what
///      physically stops the player, and it is what gets disabled on open.
///   4. Fill in `requirements` and (optionally) `lockedMessage`.
///
/// The gate can also set a flag when it opens so the state survives a reload.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RequirementGate : MonoBehaviour
{
    [Header("Requirements")]
    [Tooltip("All must be met (unless requireAll is off, then any one is enough).")]
    public bool requireAll = true;
    public Requirement[] requirements = new Requirement[0];

    [Header("Barrier")]
    [Tooltip("The child that physically blocks movement. Disabled when open.")]
    public GameObject barrier;
    [Tooltip("Extra objects to disable on open (e.g. the closed artwork).")]
    public GameObject[] extraBlockers = new GameObject[0];

    [Header("Feedback")]
    [Tooltip("Overrides the auto-generated message when the gate is locked.")]
    [TextArea(2, 4)] public string lockedMessage = "";
    public string openMessage = "";
    [Tooltip("Only show the locked message once per entry, not every frame.")]
    public bool messageOnceWhileTouching = true;
    [Tooltip("Seconds before the locked message can be shown again.")]
    public float messageCooldown = 1.5f;

    [Header("State")]
    [Tooltip("Set this flag in GameState when the gate opens. Leave blank for none.")]
    public string openFlag = "";
    [Tooltip("Start already open if this flag is present in GameState.")]
    public string preOpenedFlag = "";

    [Header("Behaviour")]
    public bool oneShot = true;
    public bool hideBarrierWhenOpen = true;

    private bool isOpen;
    private bool playerTouching;
    private float lastMessageTime = -99f;
    private Collider2D trigger;

    /// <summary>True when the gate is currently open.</summary>
    public bool IsOpen { get { return isOpen; } }

    void Awake()
    {
        trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    void Start()
    {
        GameState state = GameState.Instance;

        // Already unlocked in a previous session?
        bool preOpened = state != null && !string.IsNullOrEmpty(preOpenedFlag) &&
                         state.HasFlag(preOpenedFlag);
        bool satisfied = preOpened || RequirementsMet(state);

        if (satisfied)
            Open(false);
        else
            SetLocked(true);
    }

    void FixedUpdate()
    {
        if (isOpen && oneShot)
            return;
        if (!playerTouching)
            return;

        GameState state = GameState.Instance;
        if (RequirementsMet(state))
            Open(true);
        else if (messageOnceWhileTouching)
            ShowLockedMessage(state);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerTouching = true;

        if (isOpen && oneShot)
            return;

        GameState state = GameState.Instance;
        if (RequirementsMet(state))
            Open(true);
        else
            ShowLockedMessage(state);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
            playerTouching = false;
    }

    /// <summary>True if the configured requirements are satisfied.</summary>
    public bool RequirementsMet(GameState state)
    {
        if (requirements == null || requirements.Length == 0)
            return true;

        for (int i = 0; i < requirements.Length; i++)
        {
            if (requirements[i] == null)
                continue;

            bool met = requirements[i].IsMet(state);
            if (requireAll && !met)
                return false;
            if (!requireAll && met)
                return true;
        }
        return requireAll;
    }

    /// <summary>Auto-generate a readable "you need X" line.</summary>
    public string BuildLockedMessage()
    {
        if (!string.IsNullOrEmpty(lockedMessage))
            return lockedMessage;

        if (requirements == null || requirements.Length == 0)
            return "The way is blocked.";

        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < requirements.Length; i++)
        {
            if (requirements[i] == null)
                continue;
            if (requirements[i].kind == RequirementKind.AlwaysOpen)
                continue;
            parts.Add(requirements[i].Describe());
        }

        if (parts.Count == 0)
            return "The way is blocked.";

        string list;
        if (parts.Count == 1)
            list = parts[0];
        else
            list = string.Join(", ", parts.ToArray(), 0, parts.Count - 1) +
                   (requireAll ? " and " : " or ") + parts[parts.Count - 1];

        return "You need " + list + " to pass.";
    }

    private void ShowLockedMessage(GameState state)
    {
        if (Time.time - lastMessageTime < Mathf.Max(0f, messageCooldown))
            return;
        lastMessageTime = Time.time;

        DialogueSystem dialogue = DialogueSystem.Instance;
        if (dialogue != null)
            dialogue.ShowMessage(BuildLockedMessage());
        else
            Debug.Log("[Gate] " + BuildLockedMessage());
    }

    private void Open(bool announce)
    {
        if (isOpen && oneShot)
            return;

        isOpen = true;
        SetLocked(false);

        if (!string.IsNullOrEmpty(openFlag))
        {
            GameState state = GameState.Instance;
            if (state != null)
                state.SetFlag(openFlag);
        }

        if (announce)
        {
            if (!string.IsNullOrEmpty(openMessage))
            {
                DialogueSystem dialogue = DialogueSystem.Instance;
                if (dialogue != null)
                    dialogue.ShowMessage(openMessage);
            }
            else
            {
                Debug.Log("[Gate] '" + name + "' opened.");
            }
        }
    }

    private void SetLocked(bool locked)
    {
        if (barrier != null)
        {
            if (locked)
            {
                barrier.SetActive(true);
            }
            else if (hideBarrierWhenOpen)
            {
                barrier.SetActive(false);
            }
            else
            {
                var col = barrier.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = false;
                var sr = barrier.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = new Color(1f, 1f, 1f, 0.25f);
            }
        }

        if (!locked && extraBlockers != null)
        {
            for (int i = 0; i < extraBlockers.Length; i++)
                if (extraBlockers[i] != null)
                    extraBlockers[i].SetActive(false);
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;
        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag("Player"))
            return true;
        return other.CompareTag("Player");
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        Gizmos.color = Application.isPlaying && isOpen
            ? new Color(0.35f, 0.9f, 0.45f, 0.85f)
            : new Color(0.95f, 0.35f, 0.3f, 0.85f);

        var box = col as BoxCollider2D;
        if (box != null)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset,
                                new Vector3(box.size.x, box.size.y, 0.1f));
    }
}
```

---

## Assets\Scripts\World\GateRequirement.cs

_87 lines_

```csharp
using UnityEngine;

/// <summary>What a gate can ask for.</summary>
public enum RequirementKind
{
    CreatureType = 0,
    CreatureTypeCount = 1,
    Flag = 2,
    Item = 3,
    GymsCleared = 4,
    Upgrades = 5,
    AlwaysOpen = 6
}

/// <summary>
/// One condition a <see cref="RequirementGate"/> checks. Plain serializable
/// class (no asset files) so gates are configured entirely in the inspector
/// and cannot break when an asset GUID moves.
/// </summary>
[System.Serializable]
public class Requirement
{
    [Tooltip("What kind of thing the player must have done/brought.")]
    public RequirementKind kind = RequirementKind.CreatureType;

    [Tooltip("Used by CreatureType / CreatureTypeCount.")]
    public CreatureType creatureType = CreatureType.Tide;

    [Tooltip("Used by Flag (the flag name) and Item (the item id).")]
    public string key = "";

    [Tooltip("How many are required (CreatureTypeCount / Item / GymsCleared / Upgrades).")]
    public int amount = 1;

    [Tooltip("Invert the test: open only while the condition is NOT met.")]
    public bool invert = false;

    /// <summary>Is this requirement satisfied right now?</summary>
    public bool IsMet(GameState state)
    {
        bool met = Evaluate(state);
        return invert ? !met : met;
    }

    private bool Evaluate(GameState state)
    {
        if (state == null)
            return false;

        switch (kind)
        {
            case RequirementKind.CreatureType:
                return state.HasCreatureType(creatureType);

            case RequirementKind.CreatureTypeCount:
                return state.CountCreatureType(creatureType) >= Mathf.Max(1, amount);

            case RequirementKind.Flag:
                return state.HasFlag(key);

            case RequirementKind.Item:
                return state.ItemCount(key) >= Mathf.Max(1, amount);

            case RequirementKind.GymsCleared:
                return state.GymsCleared >= Mathf.Max(1, amount);

            case RequirementKind.Upgrades:
                return state.Upgrades >= Mathf.Max(1, amount);

            case RequirementKind.AlwaysOpen:
            default:
                return true;
        }
    }

    /// <summary>Player-facing text, e.g. "a Tide creature".</summary>
    public string Describe()
    {
        switch (kind)
        {
            case RequirementKind.CreatureType:
                return "a " + CreatureTypeUtil.DisplayName(creatureType) + " creature";

            case RequirementKind.CreatureTypeCount:
                return Mathf.Max(1, amount) + " " + CreatureTypeUtil.DisplayName(creatureType) +
                       " creatures";

            case RequirementKind.Flag:
                return string.IsNullOrEmpty(key) ? "progress" : key;

            case RequirementKind.Item:
                return Mathf.Max(1, amount) + " x " + key;

            case RequirementKind.GymsCleared:
                return Mathf.Max(1, amount) + " gym badge(s)";

            case RequirementKind.Upgrades:
                return Mathf.Max(1, amount) + " upgrade(s)";

            default:
                return "nothing";
        }
    }

    /// <summary>Short label for inspectors and debug output.</summary>
    public override string ToString()
    {
        return (invert ? "NOT " : "") + kind + " " + Describe();
    }
}
```

---

## Assets\Scripts\World\MinimapController.cs

_201 lines_

```csharp
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
```

---

## Assets\Scripts\World\MapRegion.cs

_78 lines_

```csharp
using UnityEngine;

/// <summary>
/// Names a patch of the world. Drop one on a trigger collider over a region;
/// when the player walks in, the HUD's region label updates.
///
/// Regions are also usable as gate conditions (set `flagOnEnter` and have a
/// RequirementGate ask for that flag), so "you must have visited the lab"
/// style gating works without extra code.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapRegion : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Shown on the HUD, e.g. \"NORTH RIDGE\".")]
    public string regionName = "Unnamed";
    [Tooltip("Optional second line, e.g. \"Tall grass\".")]
    public string subLabel = "";
    [Tooltip("Lower numbers win when regions overlap. Use for nested areas.")]
    public int priority = 0;

    [Header("State")]
    [Tooltip("Set this GameState flag the first time the player enters.")]
    public string flagOnEnter = "";

    [Header("Minimap")]
    [Tooltip("Draw a tint over this region on the minimap (optional).")]
    public bool highlightOnMinimap = false;
    public Color minimapTint = new Color(0.4f, 0.9f, 1f, 0.25f);

    private static MapRegion activeRegion;

    /// <summary>The region the player is currently inside (or null).</summary>
    public static MapRegion Active { get { return activeRegion; } }

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        GameState state = GameState.Instance;
        if (state != null && !string.IsNullOrEmpty(flagOnEnter))
            state.SetFlag(flagOnEnter);

        if (activeRegion != null && activeRegion != this &&
            activeRegion.priority > priority)
            return;

        activeRegion = this;

        if (HUDController.Instance != null)
            HUDController.Instance.SetRegion(regionName, subLabel);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        if (activeRegion == this)
        {
            activeRegion = null;
            if (HUDController.Instance != null)
                HUDController.Instance.SetRegion("", "");
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;
        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag("Player"))
            return true;
        return other.CompareTag("Player");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.55f, 0.75f, 1f, 0.5f);
        var box = GetComponent<Collider2D>() as BoxCollider2D;
        if (box != null)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset,
                                new Vector3(box.size.x, box.size.y, 0.1f));
    }
}
```

---

## Assets\Scripts\World\SpriteYSort.cs

_35 lines_

```csharp
using UnityEngine;

/// <summary>
/// Top-down depth sorting without depending on the renderer asset's
/// transparency-sort settings: the further up the screen something is, the
/// earlier it draws, so the player correctly passes behind tree trunks and in
/// front of foreground bushes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(100)]
public class SpriteYSort : MonoBehaviour
{
    [Tooltip("Sub-units of world Y per sorting-order step.")]
    public float unitsPerStep = 4f;

    /// <summary>
    /// Highest order this object can take. The terrain tilemap renders at 0, so
    /// `baseOrder` must exceed (worldHeight * unitsPerStep) or everything at the
    /// top of the map slips behind the ground and vanishes. The world is 64 tall
    /// and the step is 4 => 256 worst case, hence 1000 leaves comfortable slack.
    /// </summary>
    public int baseOrder = 1000;

    [Tooltip("Hard floor so a misconfigured object can never hide behind terrain.")]
    public int minOrder = 1;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (sr == null)
            return;
        int order = baseOrder - Mathf.RoundToInt(transform.position.y * unitsPerStep);
        sr.sortingOrder = Mathf.Max(minOrder, order);
    }
}
```

---

## Assets\Scripts\UI\HUDController.cs

_150 lines_

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overworld HUD: the region label, the compass/position readout and the zoom
/// indicator. Subscribes to <see cref="MinimapController"/> and
/// <see cref="GameState"/> so nothing has to poll.
/// </summary>
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("Text")]
    public TMP_Text regionLabel;
    public TMP_Text subLabel;
    [Tooltip("Shows e.g. \"FACING S   POS S\".")]
    public TMP_Text compassLabel;
    [Tooltip("Shows e.g. \"ZOOM 1.4x\".")]
    public TMP_Text zoomLabel;

    [Header("Minimap")]
    public MinimapController minimap;
    [Tooltip("Optional: the label under the minimap showing where you are.")]
    public TMP_Text minimapPositionLabel;

    [Header("Panels")]
    public CanvasGroup regionPanel;
    public float regionFadeSpeed = 6f;

    [Header("Zoom readout")]
    [Tooltip("Orthographic size the '1.0x' reading corresponds to.")]
    public float referenceOrthoSize = 5.4f;

    private float targetRegionAlpha;
    private string shownRegion = "";

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        Subscribe();

        if (regionPanel != null)
        {
            regionPanel.alpha = 0f;
            targetRegionAlpha = 0f;
        }

        SetRegion("", "");
    }

    private void Subscribe()
    {
        if (minimap == null)
            minimap = MinimapController.Instance;

        if (minimap != null)
        {
            minimap.CardinalChanged += OnCardinalChanged;
            OnCardinalChanged(minimap.Cardinal);
        }

        if (GameState.Instance != null)
            GameState.Instance.RosterChanged += OnRosterChanged;
    }

    private void Unsubscribe()
    {
        if (minimap != null)
            minimap.CardinalChanged -= OnCardinalChanged;

        if (GameState.Instance != null)
            GameState.Instance.RosterChanged -= OnRosterChanged;
    }

    void Update()
    {
        if (regionPanel != null)
        {
            regionPanel.alpha = Mathf.MoveTowards(regionPanel.alpha, targetRegionAlpha,
                                                  Time.deltaTime * regionFadeSpeed);
        }

        UpdatePositionLabels();
        UpdateZoomLabel();
    }

    /// <summary>Called by <see cref="MapRegion"/> on entry/exit.</summary>
    public void SetRegion(string name, string subtitle)
    {
        shownRegion = name ?? "";

        if (regionLabel != null)
            regionLabel.text = shownRegion.ToUpperInvariant();

        if (subLabel != null)
        {
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            subLabel.gameObject.SetActive(hasSub);
            if (hasSub)
                subLabel.text = subtitle;
        }

        targetRegionAlpha = string.IsNullOrEmpty(shownRegion) ? 0f : 1f;
    }

    /// <summary>Show a transient message (delegates to DialogueSystem if present).</summary>
    public void ShowToast(string message, float duration = 3f)
    {
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowMessage(message, duration);
        else
            Debug.Log("[HUD] " + message);
    }

    private void OnCardinalChanged(string cardinal)
    {
        UpdateCompassLabel(cardinal);
    }

    private void OnRosterChanged()
    {
        // Reserved: the party/upgrade readout will hang off this once the
        // Borrowed Time party system exists.
    }

    private void UpdateCompassLabel(string cardinal)
    {
        if (compassLabel == null)
            return;

        string vertical = minimap != null ? minimap.VerticalPosition : "Center";
        compassLabel.text = "FACING " + cardinal + "   POS " + vertical;
    }

    private void UpdatePositionLabels()
    {
        if (minimapPositionLabel == null || minimap == null)
            return;

        Vector2 n = minimap.NormalisedPosition;
        minimapPositionLabel.text = string.Format("x{0:0}%  y{1:0}%",
                                                  n.x * 100f, n.y * 100f);
    }

    private void UpdateZoomLabel()
    {
        if (zoomLabel == null)
            return;

        CameraRig rig = FindRig();
        if (rig == null)
            return;

        float factor = referenceOrthoSize / Mathf.Max(0.01f, rig.Zoom);
        zoomLabel.text = string.Format("ZOOM {0:0.0}x", factor);
    }

    private CameraRig cachedRig;

    private CameraRig FindRig()
    {
        if (cachedRig == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                cachedRig = cam.GetComponent<CameraRig>();
            if (cachedRig == null)
                cachedRig = Object.FindFirstObjectByType<CameraRig>();
        }
        return cachedRig;
    }
}
```

---

## Assets\Scripts\UI\DialogueSystem.cs

_180 lines_

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Lightweight overworld message box used by gates, regions and pickups.
///
/// Deliberately separate from the Lab's <see cref="DialogueManager"/> (which
/// drives the starter-choosing conversation and changes scenes). This one is a
/// non-blocking toast/queue any world script can push text into without caring
/// whether a dialogue UI exists -- with no panel assigned it logs instead.
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public TMP_Text bodyText;
    [Tooltip("Optional speaker line drawn above the body.")]
    public TMP_Text speakerText;
    public CanvasGroup canvasGroup;

    [Header("Behaviour")]
    [Tooltip("Default seconds a message stays on screen. 0 = stay until replaced.")]
    public float defaultDuration = 3f;
    public float fadeInTime = 0.12f;
    public float fadeOutTime = 0.25f;
    [Tooltip("Queue messages instead of overwriting the current one.")]
    public bool queueMessages = false;

    [Tooltip("Play a short blip whenever a message appears.")]
    public AudioSource blipSource;
    public AudioClip blipClip;

    private readonly Queue<Message> pending = new Queue<Message>();
    private Message current;
    private Coroutine routine;
    private bool showing;

    private struct Message
    {
        public string speaker;
        public string body;
        public float duration;
        public Action onComplete;
    }

    public bool IsVisible { get { return showing; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null && panel != null)
            canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panel != null)
            panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Show a one-shot line, replacing whatever is on screen.</summary>
    public void ShowMessage(string body, float duration = -1f, Action onComplete = null)
    {
        ShowMessage(null, body, duration, onComplete);
    }

    /// <summary>Show a line attributed to a speaker.</summary>
    public void ShowMessage(string speaker, string body, float duration = -1f,
                            Action onComplete = null)
    {
        if (string.IsNullOrEmpty(body))
            return;

        var msg = new Message
        {
            speaker = speaker,
            body = body,
            duration = duration < 0f ? defaultDuration : duration,
            onComplete = onComplete
        };

        if (queueMessages || !showing)
        {
            pending.Enqueue(msg);
            if (routine == null)
                routine = StartCoroutine(Pump());
        }
        else
        {
            // Interrupt so gameplay feedback is never queued behind itself.
            pending.Clear();
            pending.Enqueue(msg);
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Pump());
        }
    }

    /// <summary>Hide immediately and drop anything queued.</summary>
    public void Clear()
    {
        pending.Clear();
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        current = default(Message);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panel != null)
            panel.SetActive(false);
        showing = false;
    }

    private IEnumerator Pump()
    {
        while (pending.Count > 0)
        {
            current = pending.Dequeue();
            yield return Present(current);
        }
        routine = null;
    }

    private IEnumerator Present(Message msg)
    {
        if (bodyText == null && panel == null)
        {
            Debug.Log("[Dialogue] " + msg.body);
            if (msg.onComplete != null)
                msg.onComplete();
            yield break;
        }

        if (panel != null)
            panel.SetActive(true);
        if (bodyText != null)
            bodyText.text = msg.body;
        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(msg.speaker);
            speakerText.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker)
                speakerText.text = msg.speaker;
        }

        if (blipSource != null && blipClip != null)
            blipSource.PlayOneShot(blipClip);

        showing = true;
        yield return Fade(0f, 1f, fadeInTime);

        if (msg.duration > 0f)
            yield return new WaitForSeconds(msg.duration);
        else
            while (pending.Count == 0)
                yield return null;

        yield return Fade(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutTime);

        if (panel != null)
            panel.SetActive(false);
        showing = false;

        if (msg.onComplete != null)
            msg.onComplete();
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        if (canvasGroup == null && panel != null)
            canvasGroup = panel.GetComponent<CanvasGroup>();

        if (canvasGroup == null || time <= 0f)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = to;
            yield return null;
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / time));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
```

---

## Assets\Scripts\Editor\ProjectSetup.cs

_1293 lines_

```csharp
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
```

---

## Assets\Scripts\_Legacy\CameraFollow.cs

_58 lines_

```csharp

using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Camera Movement")]
    [Range(0.05f, 0.5f)]
    public float smoothTime = 0.18f;

    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Look Ahead")]
    public float lookAheadDistance = 0.4f;
    public float lookAheadSmoothTime = 0.2f;

    private Vector3 cameraVelocity;
    private Vector2 lookAhead;
    private Vector2 lookAheadVelocity;
    private Vector3 lastPlayerPosition;
    private Vector2 playerVelocity;

    void Start()
    {
        if (player == null)
            return;

        lastPlayerPosition = player.position;
        transform.position = player.position + offset;
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // Calculate player movement
        Vector3 currentPosition = player.position;

        playerVelocity = (currentPosition - lastPlayerPosition)
            / Mathf.Max(Time.deltaTime, 0.0001f);

        lastPlayerPosition = currentPosition;

        // Smooth look-ahead in the direction of movement
        Vector2 targetLookAhead = Vector2.ClampMagnitude(
            playerVelocity * 0.08f,
            lookAheadDistance
        );

        lookAhead = Vector2.SmoothDamp(
            lookAhead,
            targetLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime
        );

        // Smoothly follow the player
        Vector3 targetPosition = new Vector3(
            player.position.x + offset.x + lookAhead.x,
            player.position.y + offset.y + lookAhead.y,
            offset.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref cameraVelocity,
            smoothTime
        );
    }
}
```

---

## Assets\Scripts\_Legacy\DialogueManager.cs

_85 lines_

```csharp

using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    public TMP_Text dialogueText;
    public GameObject selectionPanel;
    public string nextSceneName = "Route1";

    [TextArea(2, 4)]
    public string[] dialogueLines;

    private int currentLine = 0;
    private bool isStarterDialogue = false;

    void Start()
    {
        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        currentLine = 0;
        ShowLine();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            NextLine();
        }
    }

    public void StartStarterDialogue(
        string creatureName,
        string creatureType,
        string weakAgainst)
    {
        isStarterDialogue = true;

        dialogueLines = new string[]
        {
            "Congratulations on choosing " + creatureName +
            "! May you and your new companion have a wonderful journey together!",

            "Remember, " + creatureName + "'s " + creatureType +
            " type is weak to " + weakAgainst + "!"
        };

        currentLine = 0;

        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        gameObject.SetActive(true);
        ShowLine();
    }

    void ShowLine()
    {
        if (dialogueLines == null ||
            dialogueLines.Length == 0 ||
            currentLine >= dialogueLines.Length)
            return;

        dialogueText.text =
            "<align=left><color=#F5C66B><b>PROFESSOR OXE</b></color></align>\n\n" +
            "<align=left>" + dialogueLines[currentLine] + "</align>\n\n" +
            "<align=right><color=#93C5FD>PRESS ENTER ▼</color></align>";
    }

    void NextLine()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
            return;

        if (currentLine < dialogueLines.Length - 1)
        {
            currentLine++;
            ShowLine();
        }
        else
        {
            if (isStarterDialogue)
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }

            if (selectionPanel == null)
            {
                Debug.LogError("SelectionPanel is not assigned!");
                return;
            }

            selectionPanel.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
```

---

## Assets\Scripts\_Legacy\MainMenuManager.cs

_9 lines_

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("Lab");
    }
}
```

---

## Assets\Scripts\_Legacy\PlayerMovement.cs

_26 lines_

```csharp

using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        movement = new Vector2(x, y).normalized;
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            rb.MovePosition(
                rb.position + movement * moveSpeed * Time.fixedDeltaTime
            );
        }
    }
}
```

---

## Assets\Scripts\_Legacy\StarterSelection.cs

_69 lines_

```csharp

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StarterSelection : MonoBehaviour
{
    public Button[] creatureButtons;
    public string[] creatureNames;
    public string[] creatureTypes;

    public TMP_Text selectedCreatureText;
    public Button confirmButton;
    public DialogueManager dialogueManager;

    private int selectedIndex = -1;

    void Start()
    {
        confirmButton.interactable = false;

        for (int i = 0; i < creatureButtons.Length; i++)
        {
            int index = i;
            creatureButtons[i].onClick.AddListener(
                () => SelectCreature(index)
            );
        }

        confirmButton.onClick.AddListener(ConfirmChoice);
        selectedCreatureText.text = "Selected: None";
    }

    void SelectCreature(int index)
    {
        selectedIndex = index;
        selectedCreatureText.text =
            "Selected: " + creatureNames[index];

        confirmButton.interactable = true;
    }

    string GetWeakness(string type)
    {
        switch (type.Trim().ToLower())
        {
            case "fire":
                return "Water";
            case "grass":
                return "Fire";
            case "electric":
                return "Ground";
            default:
                return "Unknown";
        }
    }

    void ConfirmChoice()
    {
        if (selectedIndex < 0)
            return;

        if (selectedIndex >= creatureNames.Length ||
            selectedIndex >= creatureTypes.Length)
        {
            Debug.LogError("Creature names or types are missing!");
            return;
        }

        string name = creatureNames[selectedIndex];
        string type = creatureTypes[selectedIndex];
        string weakness = GetWeakness(type);

        PlayerPrefs.SetString("StarterCreature", name);
        PlayerPrefs.Save();

        foreach (Button button in creatureButtons)
            button.interactable = false;

        confirmButton.interactable = false;

        dialogueManager.StartStarterDialogue(
            name, type, weakness
        );
    }
}
```

---

## Tools\artlib.py

_553 lines_

```python
"""artlib.py -- stdlib-only 2D art toolkit for the PikaGame remake.

No third-party dependencies (no Pillow). Contains:
  * read_png / write_png          (8/16-bit, colour types 0/2/3/4/6, non-interlaced)
  * Canvas                        (RGBA surface with analytic-AA SDF shape filling)
  * periodic value noise / fBm    (so every generated tile tiles seamlessly)
  * palette helpers + colour-space conversions

Everything is deterministic: the same source always regenerates identical bytes.
"""

import math
import struct
import zlib

_PNG_SIG = b"\x89PNG\r\n\x1a\n"


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def _unfilter(raw, height, stride, bpp):
    """Reverse the five PNG scanline filters. Returns one flat bytearray."""
    out = bytearray(height * stride)
    pos = 0
    prev = bytearray(stride)
    for row in range(height):
        ft = raw[pos]
        pos += 1
        line = bytearray(raw[pos:pos + stride])
        pos += stride
        if ft == 1:                        # Sub
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 0xFF
        elif ft == 2:                      # Up
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:                      # Average
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif ft == 4:                      # Paeth
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                c = prev[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + _paeth(a, prev[i], c)) & 0xFF
        elif ft != 0:
            raise ValueError("bad PNG filter type %d" % ft)
        out[row * stride:(row + 1) * stride] = line
        prev = line
    return out


def read_png(path):
    """Return (width, height, bytearray RGBA). Raises on unsupported variants."""
    data = open(path, "rb").read()
    if data[:8] != _PNG_SIG:
        raise ValueError("%s is not a PNG" % path)

    width = height = depth = ctype = 0
    interlace = 0
    palette = b""
    trns = b""
    idat = []
    pos = 8
    while pos < len(data):
        (ln,) = struct.unpack(">I", data[pos:pos + 4])
        name = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if name == b"IHDR":
            width, height, depth, ctype, _c, _f, interlace = struct.unpack(
                ">IIBBBBB", body)
        elif name == b"PLTE":
            palette = body
        elif name == b"tRNS":
            trns = body
        elif name == b"IDAT":
            idat.append(body)
        elif name == b"IEND":
            break

    if interlace:
        raise ValueError("interlaced PNG not supported (%s)" % path)
    if depth not in (8, 16):
        raise ValueError("unsupported bit depth %d (%s)" % (depth, path))

    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    step = 2 if depth == 16 else 1
    bpp = channels * step
    stride = width * bpp
    raw = _unfilter(zlib.decompress(b"".join(idat)), height, stride, bpp)

    out = bytearray(width * height * 4)
    for y in range(height):
        base = y * stride
        o = y * width * 4
        if ctype == 6:
            for x in range(width):
                i = base + x * bpp
                out[o:o + 4] = bytes((raw[i], raw[i + step], raw[i + 2 * step],
                                      raw[i + 3 * step]))
                o += 4
        elif ctype == 2:
            for x in range(width):
                i = base + x * bpp
                out[o:o + 4] = bytes((raw[i], raw[i + step], raw[i + 2 * step], 255))
                o += 4
        elif ctype == 0:
            for x in range(width):
                v = raw[base + x * bpp]
                out[o:o + 4] = bytes((v, v, v, 255))
                o += 4
        elif ctype == 4:
            for x in range(width):
                i = base + x * bpp
                v = raw[i]
                out[o:o + 4] = bytes((v, v, v, raw[i + step]))
                o += 4
        else:  # palette
            for x in range(width):
                idx = raw[base + x]
                p = idx * 3
                out[o:o + 4] = bytes((palette[p], palette[p + 1], palette[p + 2],
                                      trns[idx] if idx < len(trns) else 255))
                o += 4
    return width, height, out


def write_png(path, width, height, rgba):
    """Write an 8-bit RGBA PNG using filter type 0 on every scanline."""
    stride = width * 4
    raw = bytearray(height * (stride + 1))
    for y in range(height):
        raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)] = \
            rgba[y * stride:(y + 1) * stride]
    comp = zlib.compress(bytes(raw), 6)

    def chunk(tag, body):
        return (struct.pack(">I", len(body)) + tag + body +
                struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF))

    blob = (_PNG_SIG + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", comp) + chunk(b"IEND", b""))
    open(path, "wb").write(blob)


# --------------------------------------------------------------------------- #
#  Deterministic hash + periodic value noise (seamless tiling)
# --------------------------------------------------------------------------- #

_M64 = 0xFFFFFFFFFFFFFFFF


def _hash2(x, y, seed):
    n = (x * 0x9E3779B185EBCA87 + y * 0xC2B2AE3D27D4EB4F
         + seed * 0x165667B19E3779F9)
    n &= _M64
    n = ((n ^ (n >> 29)) * 0xBF58476D1CE4E5B9) & _M64
    n = ((n ^ (n >> 32)) * 0x94D049BB133111EB) & _M64
    n ^= n >> 31
    return (n & 0xFFFF) / 65535.0


def pnoise(x, y, period, seed):
    """Value noise that repeats exactly every `period` units in x and y."""
    xi = int(math.floor(x))
    yi = int(math.floor(y))
    xf = x - xi
    yf = y - yi
    x0 = xi % period
    y0 = yi % period
    x1 = (x0 + 1) % period
    y1 = (y0 + 1) % period
    v00 = _hash2(x0, y0, seed)
    v10 = _hash2(x1, y0, seed)
    v01 = _hash2(x0, y1, seed)
    v11 = _hash2(x1, y1, seed)
    u = xf * xf * (3.0 - 2.0 * xf)
    v = yf * yf * (3.0 - 2.0 * yf)
    return ((v00 * (1.0 - u) + v10 * u) * (1.0 - v)
            + (v01 * (1.0 - u) + v11 * u) * v)


def pfbm(x, y, period, octaves, seed, gain=0.5):
    """Periodic fractal noise. `x`,`y` are already in period units."""
    total = 0.0
    amp = 1.0
    norm = 0.0
    p = period
    for o in range(octaves):
        total += amp * pnoise(x * p, y * p, p, seed + o * 977)
        norm += amp
        amp *= gain
        p *= 2
    return total / norm


def phash(i, j, seed):
    """Stable per-cell hash -- used to scatter props without overlap."""
    return _hash2(i, j, seed)


# --------------------------------------------------------------------------- #
#  Colour helpers
# --------------------------------------------------------------------------- #

def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else (hi if v > hi else v)


def mix(a, b, t):
    t = clamp(t)
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def rgba(c, a=255):
    return c if len(c) == 4 else (c[0], c[1], c[2], a)


def shade(c, k):
    """Multiply an RGB(A) colour by k, preserving alpha."""
    c = rgba(c)
    return (int(clamp(c[0] * k, 0, 255)), int(clamp(c[1] * k, 0, 255)),
            int(clamp(c[2] * k, 0, 255)), c[3])


def over(dst, src):
    """src-over composite of two RGBA tuples; returns RGBA."""
    sa = src[3] / 255.0
    if sa <= 0.0:
        return dst
    da = dst[3] / 255.0
    oa = sa + da * (1.0 - sa)
    if oa <= 0.0:
        return (0, 0, 0, 0)
    return tuple(int(round((src[i] * sa + dst[i] * da * (1.0 - sa)) / oa))
                 for i in range(3)) + (int(round(oa * 255)),)


def hsv(h, s, v, a=255):
    """h in [0,1), s/v in [0,1] -> RGBA tuple."""
    h = h % 1.0
    i = int(h * 6.0) % 6
    f = h * 6.0 - int(h * 6.0)
    p = v * (1.0 - s)
    q = v * (1.0 - s * f)
    t = v * (1.0 - s * (1.0 - f))
    r, g, b = [(v, t, p), (q, v, p), (p, v, t),
               (p, q, v), (t, p, v), (v, p, q)][i]
    return (int(r * 255), int(g * 255), int(b * 255), a)


# --------------------------------------------------------------------------- #
#  Canvas: RGBA surface with analytic anti-aliased SDF filling
# --------------------------------------------------------------------------- #

class Canvas(object):
    def __init__(self, width, height, fill=None):
        self.w = width
        self.h = height
        self.px = bytearray(width * height * 4)
        if fill and rgba(fill)[3]:
            self.rect(0, 0, width, height, fill)

    # ---- raw pixel access ------------------------------------------------- #
    def get(self, x, y):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return (0, 0, 0, 0)
        o = (y * self.w + x) * 4
        return (self.px[o], self.px[o + 1], self.px[o + 2], self.px[o + 3])

    def put(self, x, y, c):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return
        o = (y * self.w + x) * 4
        c = rgba(c)
        self.px[o:o + 4] = bytes(c)

    def blend(self, x, y, c):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return
        o = (y * self.w + x) * 4
        cur = (self.px[o], self.px[o + 1], self.px[o + 2], self.px[o + 3])
        self.px[o:o + 4] = bytes(over(cur, rgba(c)))

    def rect(self, x0, y0, x1, y1, c):
        c = rgba(c)
        for y in range(max(0, int(y0)), min(self.h, int(y1))):
            for x in range(max(0, int(x0)), min(self.w, int(x1))):
                self.blend(x, y, c)

    # ---- SDF filling ------------------------------------------------------ #
    def fill_sdf(self, sdf, c, x0, y0, x1, y1, aa=1.0):
        """Fill {sdf(x,y) <= 0}, anti-aliased across `aa` pixels."""
        c = rgba(c)
        if c[3] == 0:
            return
        for y in range(max(0, int(y0)), min(self.h, int(y1) + 1)):
            fy = y + 0.5
            for x in range(max(0, int(x0)), min(self.w, int(x1) + 1)):
                d = sdf(x + 0.5, fy)
                if d > aa:
                    continue
                cov = clamp(0.5 - d / aa)
                if cov <= 0.0:
                    continue
                self.blend(x, y, (c[0], c[1], c[2], int(round(c[3] * cov))))

    # ---- primitives ------------------------------------------------------- #
    def circle(self, cx, cy, r, c):
        self.fill_sdf(lambda x, y: math.hypot(x - cx, y - cy) - r, c,
                      cx - r - 2, cy - r - 2, cx + r + 2, cy + r + 2)

    def ring(self, cx, cy, r, w, c):
        self.fill_sdf(lambda x, y: abs(math.hypot(x - cx, y - cy) - r) - w * 0.5,
                      c, cx - r - w, cy - r - w, cx + r + w, cy + r + w)

    def ellipse(self, cx, cy, rx, ry, c):
        rr = max(rx, ry)

        def d(x, y):
            dx = (x - cx) / rx
            dy = (y - cy) / ry
            k = math.hypot(dx, dy)
            if k < 1e-6:
                return -min(rx, ry)
            return (k - 1.0) * min(rx, ry) / k
        self.fill_sdf(d, c, cx - rr - 2, cy - rr - 2, cx + rr + 2, cy + rr + 2)

    def rrect(self, x0, y0, x1, y1, r, c):
        def d(x, y):
            dx = max(x0 + r - x, 0.0, x - (x1 - r))
            dy = max(y0 + r - y, 0.0, y - (y1 - r))
            return math.hypot(dx, dy) - r
        self.fill_sdf(d, c, x0 - 2, y0 - 2, x1 + 2, y1 + 2)

    def capsule(self, ax, ay, bx, by, r, c):
        def d(x, y):
            vx, vy = bx - ax, by - ay
            wx, wy = x - ax, y - ay
            L = vx * vx + vy * vy
            t = 0.0 if L == 0.0 else clamp((wx * vx + wy * vy) / L)
            return math.hypot(wx - vx * t, wy - vy * t) - r
        self.fill_sdf(d, c, min(ax, bx) - r - 2, min(ay, by) - r - 2,
                      max(ax, bx) + r + 2, max(ay, by) + r + 2)

    def line(self, ax, ay, bx, by, w, c):
        self.capsule(ax, ay, bx, by, w * 0.5, c)

    def poly(self, pts, c):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        n = len(pts)

        def d(x, y):
            inside = False
            j = n - 1
            for i in range(n):
                xi, yi = pts[i]
                xj, yj = pts[j]
                if (yi > y) != (yj > y):
                    if x < xi + (y - yi) * (xj - xi) / (yj - yi):
                        inside = not inside
                j = i
            if inside:
                return -1.0
            best = 1e9
            j = n - 1
            for i in range(n):
                xi, yi = pts[i]
                xj, yj = pts[j]
                vx, vy = xj - xi, yj - yi
                L = vx * vx + vy * vy
                t = 0.0 if L == 0.0 else clamp(((x - xi) * vx + (y - yi) * vy) / L)
                best = min(best, math.hypot(x - xi - vx * t, y - yi - vy * t))
                j = i
            return best
        self.fill_sdf(d, c, min(xs) - 2, min(ys) - 2, max(xs) + 2, max(ys) + 2)


# ---- compositing ------------------------------------------------------ #
    def blit(self, src, dx, dy, alpha=1.0):
        for y in range(src.h):
            ty = dy + y
            if ty < 0 or ty >= self.h:
                continue
            base = y * src.w * 4
            for x in range(src.w):
                tx = dx + x
                if tx < 0 or tx >= self.w:
                    continue
                o = base + x * 4
                a = src.px[o + 3]
                if a == 0:
                    continue
                if alpha < 1.0:
                    a = int(a * alpha)
                self.blend(tx, ty, (src.px[o], src.px[o + 1], src.px[o + 2], a))

    def crop(self, x, y, w, h):
        c = Canvas(w, h)
        for yy in range(h):
            sy = y + yy
            if sy < 0 or sy >= self.h:
                continue
            row = (sy * self.w + x) * 4
            drow = yy * w * 4
            for xx in range(w):
                sx = x + xx
                if sx < 0 or sx >= self.w:
                    continue
                so = row + sx * 4
                do = drow + xx * 4
                c.px[do:do + 4] = self.px[so:so + 4]
        return c

    def save(self, path):
        write_png(path, self.w, self.h, self.px)

    # ---- post-process ----------------------------------------------------- #
    def outline(self, c, width=2.0):
        """Trace an outline around every opaque region."""
        src = bytes(self.px)
        w, h = self.w, self.h
        r = int(math.ceil(width))
        rad = width * width

        def solid(x, y):
            return (0 <= x < w and 0 <= y < h
                    and src[(y * w + x) * 4 + 3] > 8)

        for y in range(h):
            for x in range(w):
                if solid(x, y):
                    continue
                near = False
                for dy in range(-r, r + 1):
                    for dx in range(-r, r + 1):
                        if dx * dx + dy * dy <= rad and solid(x + dx, y + dy):
                            near = True
                            break
                    if near:
                        break
                if near:
                    self.blend(x, y, c)

    def shadow_drop(self, dx=0, dy=6, blur=4, alpha=90):
        """Drop a soft shadow using the current alpha as the silhouette."""
        src = bytes(self.px)
        w, h = self.w, self.h
        for y in range(h):
            for x in range(w):
                a = 0
                for by in range(-blur, blur + 1):
                    sy = y - dy + by
                    if sy < 0 or sy >= h:
                        continue
                    for bx in range(-blur, blur + 1):
                        sx = x - dx + bx
                        if sx < 0 or sx >= w:
                            continue
                        if by * by + bx * bx > blur * blur:
                            continue
                        s = src[(sy * w + sx) * 4 + 3]
                        if s > a:
                            a = s
                if a > 0:
                    self.blend(x, y, (12, 14, 22, int(a * alpha / 255.0)))

    def despeckle(self):
        """Remove isolated pixels (kills stray single-pixel noise)."""
        src = bytes(self.px)
        w, h = self.w, self.h
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                o = (y * w + x) * 4
                if src[o + 3] == 0:
                    continue
                n = 0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if (dx or dy) and src[((y + dy) * w + (x + dx)) * 4 + 3] > 8:
                            n += 1
                if n == 0:
                    self.px[o:o + 4] = b"\x00\x00\x00\x00"

    def scale_alpha(self, factor):
        for i in range(3, len(self.px), 4):
            self.px[i] = int(clamp(self.px[i] * factor, 0, 255))


def tint(c, target, amount):
    """Blend an RGB colour toward `target` by `amount`."""
    return mix(rgba(c)[:3], rgba(target)[:3], amount)


# --------------------------------------------------------------------------- #
#  Fast periodic noise field (precomputed lattices: ~10x faster than _hash2)
# --------------------------------------------------------------------------- #

class NoiseField(object):
    """Seamlessly tiling fBm noise built from precomputed periodic lattices."""

    def __init__(self, seed, octaves=3, base_period=4, gain=0.5):
        self.seed = seed
        self.gain = gain
        self.layers = []
        amp = 1.0
        norm = 0.0
        p = base_period
        for o in range(octaves):
            self.layers.append((p, amp, self._lattice(p, seed + o * 7919)))
            norm += amp
            amp *= gain
            p *= 2
        self.norm = norm or 1.0

    @staticmethod
    def _lattice(p, seed):
        return [_hash2(i, j, seed) for j in range(p) for i in range(p)]

    def at(self, u, v):
        """u,v in tile units [0,1) -> noise in [0,1]; repeats every 1.0."""
        acc = 0.0
        for p, amp, lat in self.layers:
            x = u * p
            y = v * p
            xi = int(x)
            yi = int(y)
            xf = x - xi
            yf = y - yi
            x0 = xi % p
            y0 = yi % p
            x1 = (x0 + 1) % p
            y1 = (y0 + 1) % p
            u2 = xf * xf * (3.0 - 2.0 * xf)
            v2 = yf * yf * (3.0 - 2.0 * yf)
            r0 = y0 * p
            r1 = y1 * p
            a = lat[r0 + x0]
            b = lat[r0 + x1]
            c = lat[r1 + x0]
            d = lat[r1 + x1]
            acc += amp * ((a * (1.0 - u2) + b * u2) * (1.0 - v2)
                          + (c * (1.0 - u2) + d * u2) * v2)
        return acc / self.norm

# --------------------------------------------------------------------------- #
#  Resampling + image analysis (used to derive terrain from the painted map)
# --------------------------------------------------------------------------- #

def _catmull(p0, p1, p2, p3, t):
    return (p1 + 0.5 * t * (p2 - p0 + t * (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3
                                         + t * (3.0 * (p1 - p2) + p3 - p0))))


def resample_bicubic(w, h, rgba, nw, nh):
    """Separable Catmull-Rom resample of an RGBA bytearray -> new bytearray."""
    # --- horizontal pass: w -> nw
    tmp = bytearray(nw * h * 4)
    xr = float(w) / nw
    for y in range(h):
        srow = y * w * 4
        drow = y * nw * 4
        for x in range(nw):
            sx = (x + 0.5) * xr - 0.5
            i1 = int(math.floor(sx))
            t = sx - i1
            for ch in range(4):
                p0 = rgba[srow + max(0, i1 - 1) * 4 + ch]
                p1 = rgba[srow + max(0, i1) * 4 + ch]
                p2 = rgba[srow + min(w - 1, i1 + 1) * 4 + ch]
                p3 = rgba[srow + min(w - 1, i1 + 2) * 4 + ch]
                v = _catmull(p0, p1, p2, p3, t)
                tmp[drow + x * 4 + ch] = int(clamp(v, 0, 255))
    # --- vertical pass: h -> nh
    out = bytearray(nw * nh * 4)
    yr = float(h) / nh
    for y in range(nh):
        sy = (y + 0.5) * yr - 0.5
        j1 = int(math.floor(sy))
        t = sy - j1
        drow = y * nw * 4
        for x in range(nw):
            for ch in range(4):
                p0 = tmp[(max(0, j1 - 1) * nw + x) * 4 + ch]
                p1 = tmp[(max(0, j1) * nw + x) * 4 + ch]
                p2 = tmp[(min(h - 1, j1 + 1) * nw + x) * 4 + ch]
                p3 = tmp[(min(h - 1, j1 + 2) * nw + x) * 4 + ch]
                v = _catmull(p0, p1, p2, p3, t)
                out[drow + x * 4 + ch] = int(clamp(v, 0, 255))
    return out


def block_mean(w, h, rgba, bx, by):
    """Return a grid of (mean_r, mean_g, mean_b) for bx-by-by pixel blocks."""
    cols = w // bx
    rows = h // by
    grid = []
    for r in range(rows):
        row = []
        for c in range(cols):
            sr = sg = sb = 0
            for y in range(r * by, (r + 1) * by):
                base = y * w * 4
                for x in range(c * bx, (c + 1) * bx):
                    o = base + x * 4
                    sr += rgba[o]
                    sg += rgba[o + 1]
                    sb += rgba[o + 2]
            n = bx * by
            row.append((sr // n, sg // n, sb // n))
        grid.append(row)
    return grid


def palette_histogram(w, h, rgba, step=4, bits=4):
    """Coarse colour histogram (quantised per channel) -- for art matching."""
    shift = 8 - bits
    counts = {}
    for y in range(0, h, step):
        base = y * w * 4
        for x in range(0, w, step):
            o = base + x * 4
            key = (rgba[o] >> shift, rgba[o + 1] >> shift, rgba[o + 2] >> shift)
            counts[key] = counts.get(key, 0) + 1
    top = sorted(counts.items(), key=lambda kv: -kv[1])[:24]
    scale = 255.0 / (2 ** bits - 1)
    return [((int(k[0] * scale), int(k[1] * scale), int(k[2] * scale)), v)
            for k, v in top]
```

---

## Tools\GenArt.py

_1013 lines_

```python
"""GenArt.py -- deterministic procedural art generator for the PikaGame remake.

Stdlib only (no Pillow, no network). Produces:

  Assets/Art/Tiles/terrain_tileset.png     10 seamlessly tiling terrain tiles
  Assets/Art/Characters/player_walk.png    4 directions x 4 walk frames
  Assets/Art/Characters/player_idle.png    4 idle frames
  Assets/Art/Characters/professor.png      NPC portrait sprite
  Assets/Art/Creatures/<name>.png          12 creatures + evolved forms
  Assets/Art/UI/minimap_mask.png           circular minimap mask
  Assets/Art/UI/minimap_bezel.png          circular bezel + compass ring
  Assets/Art/UI/hud_panel.png              9-slice HUD panel
  Assets/Art/World/props.png               trees / rocks / signs / gate markers
  Tools/out/terrain_map.json               terrain grid derived from map.png

Usage:
    python Tools/GenArt.py              # everything
    python Tools/GenArt.py --only tiles # one group
    python Tools/GenArt.py --tile 256   # smaller tiles (faster preview)
"""

import json
import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import artlib                                                        # noqa: E402
from artlib import (Canvas, NoiseField, clamp, hsv, mix, over,        # noqa: E402
                    rgba, shade)

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJ, "Assets", "Art")
OUT = os.path.join(PROJ, "Tools", "out")

# Texture pixels per tile, and pixels-per-unit.  A tile is always 2 world
# units, so the effective PPU is TILE/2 -- this is what keeps the map crisp
# across the whole zoom band (see README-REMAKE.md for the math).
TILE = 512
PPU = TILE // 2

# --------------------------------------------------------------------------- #
#  Palette -- sampled directly from the original painted map.png so the new
#  tiles sit next to the existing art without a colour clash.
# --------------------------------------------------------------------------- #

PAL = {
    "grass_a": (58, 146, 54), "grass_b": (36, 112, 38),
    "grass_dark": (24, 86, 28), "grass_hi": (86, 176, 74),
    "tall_a": (34, 104, 40), "tall_b": (18, 72, 30), "tall_hi": (72, 150, 66),
    "path_a": (255, 221, 119), "path_b": (231, 193, 96),
    "path_dot": (208, 168, 74),
    "sand_a": (255, 234, 168), "sand_b": (240, 208, 124),
    "water_deep": (0, 68, 140), "water_a": (0, 136, 255), "water_b": (0, 153, 255),
    "foam": (186, 228, 255),
    "forest_a": (0, 85, 51), "forest_b": (0, 68, 34), "forest_hi": (30, 124, 66),
    "trunk": (74, 52, 32), "trunk_hi": (112, 80, 50),
    "rock_a": (136, 85, 51), "rock_b": (102, 68, 51), "rock_hi": (178, 126, 80),
    "wall_a": (116, 108, 100), "wall_b": (76, 70, 64), "wall_hi": (156, 148, 138),
    "floor_a": (208, 192, 166), "floor_b": (176, 158, 132),
    "plank_a": (168, 116, 66), "plank_b": (124, 82, 46),
    "ink": (30, 24, 22),
    "gate_locked": (206, 78, 62), "gate_open": (86, 190, 120),
}

# --- terrain codes --------------------------------------------------------- #
T_GRASS, T_TALL, T_PATH, T_SAND, T_WATER = 0, 1, 2, 3, 4
T_FOREST, T_ROCK, T_WALL, T_FLOOR, T_BRIDGE = 5, 6, 7, 8, 9

TERRAIN_NAMES = ["Grass", "TallGrass", "Path", "Sand", "Water",
                 "Forest", "Rock", "Wall", "Floor", "Bridge"]
WALKABLE = {T_GRASS, T_TALL, T_PATH, T_SAND, T_FLOOR, T_BRIDGE}


# --------------------------------------------------------------------------- #
#  Cached noise: one grid per seed, sampled by index (fast + still seamless)
# --------------------------------------------------------------------------- #

_NOISE_CACHE = {}


def noise_grid(size, seed, octaves=3):
    key = (size, seed, octaves)
    if key not in _NOISE_CACHE:
        nf = NoiseField(seed, octaves=octaves)
        inv = 1.0 / size
        g = [0] * (size * size)
        i = 0
        for y in range(size):
            v = y * inv
            for _x in range(size):
                g[i] = int(nf.at(_x * inv, v) * 255.999)
                i += 1
        _NOISE_CACHE[key] = g
    return _NOISE_CACHE[key]


def ramp(c0, c1):
    """256-entry RGB lookup table lerping c0 -> c1."""
    return [mix(c0, c1, i / 255.0) for i in range(256)]


def base_pass(canvas, lut, grid, ox=0, oy=0):
    """Fill the whole tile from a noise grid through a colour ramp."""
    T = canvas.w
    px = canvas.px
    for y in range(T):
        srow = ((y + oy) % T) * T
        drow = y * T * 4
        for x in range(T):
            c = lut[grid[srow + ((x + ox) % T)]]
            o = drow + x * 4
            px[o] = c[0]
            px[o + 1] = c[1]
            px[o + 2] = c[2]
            px[o + 3] = 255


def wrap_draw(fn, T):
    """Call fn(dx,dy) for the 9 wrapped positions so shapes cross tile edges."""
    for dy in (-T, 0, T):
        for dx in (-T, 0, T):
            fn(dx, dy)


# --------------------------------------------------------------------------- #
#  Terrain tile painters
# --------------------------------------------------------------------------- #

def scatter(T, count, seed, margin=0.0):
    """Deterministic pseudo-random points inside a tile."""
    pts = []
    span = T - 2.0 * margin
    for i in range(count):
        x = margin + artlib._hash2(i, 1, seed) * span
        y = margin + artlib._hash2(i, 2, seed) * span
        pts.append((x, y))
    return pts


def blend_pass(canvas, grid, colour, lo, hi, alpha=255, soft=12):
    """Blend `colour` where grid is within [lo,hi], with soft edges."""
    T = canvas.w
    col = rgba(colour, alpha)
    for y in range(T):
        srow = y * T
        drow = y * T * 4
        for x in range(T):
            v = grid[srow + x]
            if v < lo - soft or v > hi + soft:
                continue
            if lo <= v <= hi:
                k = 1.0
            else:
                k = clamp((soft - abs(v - (lo if v < lo else hi))) / float(soft))
            if k <= 0.0:
                continue
            o = drow + x * 4
            if k >= 1.0:
                canvas.px[o:o + 4] = bytes(col)
            else:
                cur = (canvas.px[o], canvas.px[o + 1], canvas.px[o + 2], 255)
                canvas.px[o:o + 4] = bytes(
                    over(cur, (col[0], col[1], col[2], int(col[3] * k))))


def tile_grass(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["grass_b"], PAL["grass_a"]), noise_grid(T, seed))
    blend_pass(c, noise_grid(T, seed + 311), PAL["grass_dark"], 0, 92, 200, 14)
    blend_pass(c, noise_grid(T, seed + 617), PAL["grass_hi"], 176, 255, 150, 14)
    # blade tufts
    for i, (x, y) in enumerate(scatter(T, 190, seed + 5)):
        h = 7.0 + artlib._hash2(i, 7, seed) * 9.0
        lean = (artlib._hash2(i, 8, seed) - 0.5) * 6.0
        col = PAL["grass_hi"] if artlib._hash2(i, 9, seed) > 0.5 else PAL["grass_dark"]
        wrap_draw(lambda dx, dy, x=x, y=y, h=h, lean=lean, col=col:
                  c.line(x + dx, y + dy, x + dx + lean, y + dy - h, 2.4, col), T)
    return c


def tile_tallgrass(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["tall_b"], PAL["tall_a"]), noise_grid(T, seed + 41))
    blend_pass(c, noise_grid(T, seed + 733), PAL["grass_dark"], 0, 96, 210, 14)
    # dense clumps of pointed blades
    for i, (x, y) in enumerate(scatter(T, 34, seed + 11)):
        n = int(14 + artlib._hash2(i, 3, seed) * 10)
        for b in range(n):
            ax = x + (artlib._hash2(i * 31 + b, 4, seed) - 0.5) * 34.0
            ay = y + (artlib._hash2(i * 31 + b, 5, seed) - 0.5) * 30.0
            hh = 20.0 + artlib._hash2(i * 31 + b, 6, seed) * 22.0
            ln = (artlib._hash2(i * 31 + b, 12, seed) - 0.5) * 9.0
            col = PAL["tall_hi"] if artlib._hash2(b, i, seed) > 0.45 else PAL["tall_b"]
            wrap_draw(lambda dx, dy, ax=ax, ay=ay, hh=hh, ln=ln, col=col:
                      c.line(ax + dx, ay + dy, ax + dx + ln, ay + dy - hh, 3.0, col), T)
    return c


def tile_path(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["path_b"], PAL["path_a"]), noise_grid(T, seed + 97))
    blend_pass(c, noise_grid(T, seed + 151), PAL["path_dot"], 0, 88, 120, 16)
    # pebbles + worn track marks
    for i, (x, y) in enumerate(scatter(T, 46, seed + 23)):
        r = 2.0 + artlib._hash2(i, 5, seed) * 3.4
        col = PAL["path_dot"] if i % 3 else shade(PAL["path_dot"], 0.82)
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, col=col: c.circle(x + dx, y + dy, r, col), T)
    for i, (x, y) in enumerate(scatter(T, 22, seed + 29)):
        w = 10.0 + artlib._hash2(i, 2, seed) * 26.0
        wrap_draw(lambda dx, dy, x=x, y=y, w=w:
                  c.ellipse(x + dx, y + dy, w, 3.0 + artlib._hash2(0, int(w), seed) * 2.5,
                            shade(PAL["path_b"], 0.93)), T)
    return c


def tile_sand(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["sand_b"], PAL["sand_a"]), noise_grid(T, seed + 191))
    blend_pass(c, noise_grid(T, seed + 233), PAL["sand_b"], 0, 92, 130, 16)
    # wind ripples
    for i, (x, y) in enumerate(scatter(T, 18, seed + 37)):
        for k in range(3):
            yy = y + k * 9.0
            w = 26.0 + artlib._hash2(i, k, seed) * 40.0
            wrap_draw(lambda dx, dy, x=x, yy=yy, w=w:
                      c.ellipse(x + dx, yy + dy, w, 2.6, shade(PAL["sand_b"], 0.93)), T)
    return c


def tile_water(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["water_deep"], PAL["water_b"]), noise_grid(T, seed + 53))
    blend_pass(c, noise_grid(T, seed + 401), PAL["water_deep"], 0, 96, 190, 18)
    # rolling ripple bands
    for i, (x, y) in enumerate(scatter(T, 40, seed + 59)):
        w = 20.0 + artlib._hash2(i, 4, seed) * 46.0
        h = 1.8 + artlib._hash2(i, 6, seed) * 2.2
        a = 40 + int(artlib._hash2(i, 8, seed) * 70)
        wrap_draw(lambda dx, dy, x=x, y=y, w=w, h=h, a=a:
                  c.ellipse(x + dx, y + dy, w, h, rgba(PAL["foam"], a)), T)
    # sparse specular glints keep the surface from looking flat
    for i, (x, y) in enumerate(scatter(T, 12, seed + 67)):
        r = 2.2 + artlib._hash2(i, 3, seed) * 3.2
        wrap_draw(lambda dx, dy, x=x, y=y, r=r:
                  c.ellipse(x + dx, y + dy, r * 2.2, r, rgba(PAL["foam"], 120)), T)
    return c


def tile_forest(T, seed):
    c = Canvas(T, T)
    # dark undergrowth so seams between forest tiles never show
    base_pass(c, ramp(PAL["forest_b"], PAL["forest_a"]), noise_grid(T, seed + 71))
    blend_pass(c, noise_grid(T, seed + 811), PAL["forest_b"], 0, 90, 200, 16)
    # canopy: overlapping blobs, wrapped so the tile joins seamlessly
    for i, (x, y) in enumerate(scatter(T, 30, seed + 73, margin=-30)):
        r = 26.0 + artlib._hash2(i, 5, seed) * 26.0
        tone = [PAL["forest_a"], PAL["forest_hi"], PAL["forest_b"]][i % 3]
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, tone=tone:
                  c.circle(x + dx, y + dy + r * 0.25, r, shade(tone, 0.72)), T)
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, tone=tone:
                  c.circle(x + dx, y + dy, r, tone), T)
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, tone=tone:
                  c.ellipse(x + dx - r * 0.28, y + dy - r * 0.34, r * 0.46, r * 0.32,
                            shade(tone, 1.22)), T)
    # a couple of trunk glimpses
    for i, (x, y) in enumerate(scatter(T, 5, seed + 79)):
        wrap_draw(lambda dx, dy, x=x, y=y:
                  c.rrect(x + dx - 5, y + dy - 30, x + dx + 5, y + dy + 22, 3,
                          PAL["trunk"]), T)
    return c


def tile_rock(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["rock_b"], PAL["rock_a"]), noise_grid(T, seed + 83))
    blend_pass(c, noise_grid(T, seed + 887), PAL["rock_b"], 0, 94, 170, 16)
    for i, (x, y) in enumerate(scatter(T, 14, seed + 89, margin=-40)):
        s = 34.0 + artlib._hash2(i, 7, seed) * 46.0
        rot = artlib._hash2(i, 8, seed) * math.tau
        pts = []
        for k in range(6):
            a = rot + k * math.tau / 6.0
            rr = s * (0.72 + 0.34 * artlib._hash2(i * 7 + k, 9, seed))
            pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
        wrap_draw(lambda dx, dy, pts=pts:
                  c.poly([(px + dx, py + dy) for px, py in pts],
                         shade(PAL["rock_b"], 0.7)), T)
        wrap_draw(lambda dx, dy, pts=pts, s=s:
                  c.poly([(px + dx, py + dy - s * 0.16) for px, py in pts],
                         PAL["rock_a"]), T)
        top = [pts[5], pts[0], pts[1]]
        wrap_draw(lambda dx, dy, top=top, s=s:
                  c.poly([(px + dx, py + dy - s * 0.30) for px, py in top],
                         PAL["rock_hi"]), T)
    return c


def tile_wall(T, seed):
    c = Canvas(T, T)
    c.rect(0, 0, T, T, PAL["wall_b"])
    rows = 5
    bh = T / float(rows)
    cols = 4
    bw = T / float(cols)
    for r in range(rows):
        y0 = r * bh
        offs = (bw * 0.5) if r % 2 else 0.0
        for k in range(-1, cols + 1):
            x0 = k * bw + offs
            tone = mix(PAL["wall_b"], PAL["wall_a"],
                       artlib._hash2(r, k, seed + 97))
            c.rrect(x0 + 3, y0 + 3, x0 + bw - 3, y0 + bh - 3, 5, tone)
            c.rrect(x0 + 3, y0 + 3, x0 + bw - 3, y0 + bh * 0.4, 5,
                    shade(tone, 1.18))
            c.rrect(x0 + 3, y0 + bh - 10, x0 + bw - 3, y0 + bh - 3, 4,
                    shade(tone, 0.74))
    return c


def tile_floor(T, seed):
    c = Canvas(T, T)
    c.rect(0, 0, T, T, shade(PAL["floor_b"], 0.86))
    n = 4
    s = T / float(n)
    for r in range(n):
        for k in range(n):
            tone = mix(PAL["floor_b"], PAL["floor_a"],
                       artlib._hash2(r, k, seed + 131))
            c.rrect(k * s + 2, r * s + 2, (k + 1) * s - 2, (r + 1) * s - 2, 4, tone)
            c.ellipse(k * s + s * 0.32, r * s + s * 0.3, s * 0.3, s * 0.16,
                      shade(tone, 1.08))
    return c


def tile_bridge(T, seed):
    c = tile_water(T, seed + 500)
    c.rect(0, T * 0.14, T, T * 0.86, PAL["plank_b"])
    planks = 7
    ph = (T * 0.72) / planks
    for k in range(planks):
        y0 = T * 0.14 + k * ph
        tone = mix(PAL["plank_b"], PAL["plank_a"],
                   0.35 + 0.65 * artlib._hash2(k, 3, seed + 149))
        c.rrect(0, y0 + 2, T, y0 + ph - 2, 3, tone)
        c.rect(0, y0 + 2, T, y0 + 5, shade(tone, 1.2))
    nail = shade(PAL["plank_b"], 0.6)
    for k in range(planks):
        yc = T * 0.14 + (k + 0.5) * ph
        for side in (0.06, 0.5, 0.94):
            wrap_draw(lambda dx, dy, yc=yc, side=side:
                      c.circle(dx + T * side, dy + yc, 3.2, nail), T)
    c.rect(0, T * 0.14, T, T * 0.176, shade(PAL["ink"], 0.85))
    c.rect(0, T * 0.844, T, T * 0.86, shade(PAL["ink"], 0.85))
    return c


TILE_PAINTERS = [tile_grass, tile_tallgrass, tile_path, tile_sand, tile_water,
                 tile_forest, tile_rock, tile_wall, tile_floor, tile_bridge]


def build_terrain_tileset(tile=TILE, cols=5, seed=1337):
    """Return (atlas Canvas, tile rects, rows). Tile index order matches the
    terrain codes, laid out left-to-right, top-to-bottom in Unity sprite
    order (row 0 = the *last* painted row, because Unity enumerates sprite
    rects bottom-up)."""
    rows = (len(TILE_PAINTERS) + cols - 1) // cols
    sheet = Canvas(cols * tile, rows * tile)
    rects = []
    for idx, painter in enumerate(TILE_PAINTERS):
        t0 = time.time()
        sub = painter(tile, seed + idx * 4099)
        col = idx % cols
        row = idx // cols
        x0 = col * tile
        y0 = (rows - 1 - row) * tile
        sheet.blit(sub, x0, y0)
        rects.append({"index": idx, "name": TERRAIN_NAMES[idx],
                      "col": col, "row": row, "x": x0, "y": y0})
        print("    tile %-10s %dx%d  %.1fs" % (TERRAIN_NAMES[idx], tile, tile,
                                               time.time() - t0))
    return sheet, rects, rows


# --------------------------------------------------------------------------- #
#  Terrain classification: derive the world layout from the original map.png
# --------------------------------------------------------------------------- #

def classify_pixel(r, g, b):
    """Map one sampled colour to a terrain code using the sampled palette."""
    if b > 140 and b > r + 30 and b > g + 20:
        return T_WATER
    if r > 195 and g > 160 and b < 190 and (r - b) > 40:
        return T_PATH                      # warm pale sand / path
    if r < 70 and g > 45 and b < 90 and g >= b:
        return T_FOREST                    # dark green canopy
    if r > b + 34 and g < 150 and r < 195:
        return T_ROCK                      # brown rock / cliff
    if g >= r and g > 90:
        return T_GRASS
    if r > 150 and g > 130:
        return T_SAND
    return T_GRASS


def classify_map(path, cols=48, rows=32):
    w, h, px = artlib.read_png(path)
    bx = w // cols
    by = h // rows
    grid = artlib.block_mean(w, h, px, bx, by)
    out = []
    for r in range(rows):
        row = []
        for c in range(cols):
            mr, mg, mb = grid[r][c]
            code = classify_pixel(mr, mg, mb)
            row.append(code)
        out.append(row)
    return out, (w, h)


def ensure_connected(grid, prefer=(T_PATH, T_SAND)):
    """Flood-fill the walkable region so the player can never be walled in."""
    rows = len(grid)
    cols = len(grid[0])
    walk = [[grid[r][c] in WALKABLE for c in range(cols)] for r in range(rows)]

    seen = [[False] * cols for _ in range(rows)]
    best = []
    for sr in range(rows):
        for sc in range(cols):
            if not walk[sr][sc] or seen[sr][sc]:
                continue
            stack = [(sr, sc)]
            seen[sr][sc] = True
            comp = []
            while stack:
                r, c = stack.pop()
                comp.append((r, c))
                for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nr, nc = r + dr, c + dc
                    if (0 <= nr < rows and 0 <= nc < cols and walk[nr][nc]
                            and not seen[nr][nc]):
                        seen[nr][nc] = True
                        stack.append((nr, nc))
            if len(comp) > len(best):
                best = comp

    keep = set(best)
    converted = 0
    for r in range(rows):
        for c in range(cols):
            if walk[r][c] and (r, c) not in keep:
                grid[r][c] = T_GRASS
                converted += 1
    return grid, keep, converted


def spawn_point(grid, keep):
    """Pick the southernmost walkable tile on the main landmass as spawn."""
    best = None
    for (r, c) in keep:
        if grid[r][c] == T_GRASS:
            if best is None or r > best[0]:
                best = (r, c)
    if best is None and keep:
        best = max(keep)
    return best or (len(grid) - 1, len(grid[0]) // 2)


# --------------------------------------------------------------------------- #
#  Entry point
# --------------------------------------------------------------------------- #

def ensure_dirs():
    for d in (ART, os.path.join(ART, "Tiles"), os.path.join(ART, "Characters"),
              os.path.join(ART, "Creatures"), os.path.join(ART, "UI"),
              os.path.join(ART, "World"), OUT):
        if not os.path.isdir(d):
            os.makedirs(d)


def gen_tiles(tile):
    print("  terrain tileset (%dx%d px per tile, %.0f px/unit)" % (tile, tile, tile / 2.0))
    sheet, rects, rows = build_terrain_tileset(tile, cols=5)
    path = os.path.join(ART, "Tiles", "terrain_tileset.png")
    sheet.save(path)
    meta = {"tile_px": tile, "ppu": tile // 2, "cols": 5, "rows": rows,
            "tiles": rects, "ppu_note": "tile is 2 world units"}
    with open(os.path.join(OUT, "tileset.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    print("  -> %s (%dx%d)" % (path, sheet.w, sheet.h))
    return meta


def gen_terrain_map(cols, rows):
    src = os.path.join(PROJ, "Assets", "Sprites", "Environment", "map.png")
    print("  classifying %s into a %dx%d grid" % (os.path.basename(src), cols, rows))
    grid, (w, h) = classify_map(src, cols, rows)
    grid, keep, converted = ensure_connected(grid)
    sr, sc = spawn_point(grid, keep)
    counts = {}
    for r in range(rows):
        for c in range(cols):
            counts[TERRAIN_NAMES[grid[r][c]]] = \
                counts.get(TERRAIN_NAMES[grid[r][c]], 0) + 1
    data = {"source": "Assets/Sprites/Environment/map.png", "source_size": [w, h],
            "cols": cols, "rows": rows, "walkable": sorted(WALKABLE),
            "terrain_names": TERRAIN_NAMES,
            "spawn": {"col": sc, "row": sr},
            "unreachable_removed": converted,
            "counts": counts, "grid": grid}
    with open(os.path.join(OUT, "terrain_map.json"), "w") as fh:
        json.dump(data, fh, indent=1)

    # A flat CSV alongside the JSON: the Unity editor builder parses this with
    # plain string splitting, so there is no JSON dependency in C#.
    with open(os.path.join(OUT, "terrain_map.csv"), "w") as fh:
        fh.write("# cols=%d rows=%d spawn_col=%d spawn_row=%d\n" % (cols, rows, sc, sr))
        fh.write("# walkable=%s\n" % ",".join(str(t) for t in sorted(WALKABLE)))
        fh.write("# names=%s\n" % "|".join(TERRAIN_NAMES))
        for r in range(rows):
            fh.write(",".join(str(v) for v in grid[r]) + "\n")

    print("  -> %s  spawn=(col %d,row %d)  terrain: %s"
          % (os.path.join(OUT, "terrain_map.json"), sc, sr,
             ", ".join("%s=%d" % kv for kv in sorted(counts.items()))))
    return data


def main(argv):
    global TILE, PPU
    only = "all"
    tile = TILE
    for i, a in enumerate(argv):
        if a == "--only" and i + 1 < len(argv):
            only = argv[i + 1]
        if a == "--tile" and i + 1 < len(argv):
            tile = int(argv[i + 1])
    TILE = tile
    PPU = tile // 2

    t0 = time.time()
    ensure_dirs()
    groups = ["terrain", "tiles", "split", "chars", "creatures", "ui", "world"]
    want = groups if only == "all" else [only]

    print("GenArt: tile=%d ppu=%d" % (tile, PPU))
    if "terrain" in want:
        gen_terrain_map(48, 32)
    if "tiles" in want:
        gen_tiles(tile)
    if "split" in want:
        print("  split sprites (Sprite:Single -- no slicing)")
        gen_split_tiles(tile)
        gen_split_characters()
    if "chars" in want:
        print("  characters")
        gen_chars()
    if "creatures" in want:
        print("  creatures")
        gen_creatures()
    if "ui" in want:
        print("  ui")
        gen_ui()
    if "world" in want:
        print("  world props")
        gen_props()
    print("done in %.1fs" % (time.time() - t0))
    return 0


# --------------------------------------------------------------------------- #
#  Characters -- player (4 dir x 4 walk frames), idle, professor, NPCs
# --------------------------------------------------------------------------- #

CH_W, CH_H = 192, 240          # 192 wide at PPU 192 => 1.0 x 1.25 world units
CH_PPU = CH_W

DIR_DOWN, DIR_LEFT, DIR_RIGHT, DIR_UP = 0, 1, 2, 3
DIR_NAMES = ["Down", "Left", "Right", "Up"]

PLAYER_CFG = {
    "skin": (240, 198, 160), "skin_shade": (214, 168, 130),
    "hair": (58, 40, 30),
    "shirt": (72, 132, 214), "shirt_dark": (50, 98, 168),
    "pants": (54, 62, 92), "shoe": (44, 40, 44),
    "cap": (216, 74, 74), "cap_dark": (176, 52, 52),
    "ink": (30, 24, 22),
}

PROF_CFG = {
    "skin": (238, 196, 156), "skin_shade": (210, 164, 126),
    "hair": (196, 196, 200),
    "shirt": (238, 240, 244), "shirt_dark": (198, 202, 210),
    "pants": (86, 82, 96), "shoe": (52, 48, 52),
    "cap": (236, 238, 242), "cap_dark": (196, 200, 208),
    "ink": (30, 24, 22),
}


def npc_cfg(hue, hair=(58, 40, 30)):
    """Build an NPC palette from a shirt hue."""
    return {
        "skin": (238, 196, 156), "skin_shade": (212, 166, 128),
        "hair": hair,
        "shirt": hsv(hue, 0.55, 0.82), "shirt_dark": hsv(hue, 0.62, 0.62),
        "pants": (62, 66, 84), "shoe": (44, 40, 44),
        "cap": hsv((hue + 0.5) % 1.0, 0.5, 0.78),
        "cap_dark": hsv((hue + 0.5) % 1.0, 0.6, 0.6),
        "ink": (30, 24, 22),
    }


def draw_character(c, cfg, direction, phase, cx=None, ground=None, scale=1.0):
    """Draw one character frame. `phase` 0..3 drives the walk cycle."""
    cx = c.w * 0.5 if cx is None else cx
    ground = c.h * 0.965 if ground is None else ground
    k = scale
    skin = rgba(cfg["skin"])
    skin_s = rgba(cfg["skin_shade"])
    hair = rgba(cfg["hair"])
    shirt = rgba(cfg["shirt"])
    shirt_d = rgba(cfg["shirt_dark"])
    pants = rgba(cfg["pants"])
    shoe = rgba(cfg["shoe"])
    cap = rgba(cfg["cap"])
    cap_d = rgba(cfg["cap_dark"])
    ink = rgba(cfg["ink"])

    def y(v):
        return ground - v * k

    leg_top, torso_bot, torso_top = 40.0, 46.0, 96.0
    head_r = 30.0
    head_cy = torso_top + head_r * 0.86

    bob = -3.0 * k if phase in (1, 3) else 0.0
    swing = 1.0 if phase == 1 else (-1.0 if phase == 3 else 0.0)
    leg_fwd = 10.0 * swing

    # --- legs ------------------------------------------------------------ #
    for sgn in (-1, 1):
        off = leg_fwd if sgn > 0 else -leg_fwd
        lx = cx + sgn * 13.0 * k
        c.capsule(lx, y(leg_top), lx + off, y(4.0), 8.0 * k, pants)
        c.rrect(lx + off - 10.0 * k, y(9.0), lx + off + 10.0 * k, y(0.0),
                4.0 * k, shoe)

    # --- torso ----------------------------------------------------------- #
    c.rrect(cx - 27.0 * k, y(torso_top) + bob, cx + 27.0 * k,
            y(torso_bot) + bob, 10.0 * k, shirt)
    c.rrect(cx - 27.0 * k, y(torso_top) + bob, cx - 8.0 * k,
            y(torso_bot) + bob, 10.0 * k, shirt_d)

    # --- arms ------------------------------------------------------------ #
    for sgn in (-1, 1):
        ax = cx + sgn * 30.0 * k
        off = -leg_fwd * 0.8 if sgn > 0 else leg_fwd * 0.8
        c.capsule(ax, y(torso_top - 8.0) + bob, ax + off, y(torso_bot + 6.0) + bob,
                  7.5 * k, shirt_d)
        c.circle(ax + off, y(torso_bot + 4.0) + bob, 8.0 * k, skin)

    # --- head ------------------------------------------------------------ #
    hy = y(head_cy) + bob
    c.circle(cx, hy, head_r * k, skin)
    c.ellipse(cx, hy + head_r * 0.34 * k, head_r * 0.94 * k, head_r * 0.5 * k, skin_s)

    if direction == DIR_DOWN:
        c.poly([(cx - head_r * 1.04 * k, hy - head_r * 0.14 * k),
                (cx - head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 1.04 * k, hy - head_r * 0.14 * k)], cap)
        c.rrect(cx - head_r * 1.12 * k, hy - head_r * 0.32 * k,
                cx + head_r * 1.12 * k, hy + head_r * 0.04 * k, 4.0 * k, cap_d)
        for sgn in (-1, 1):
            ex = cx + sgn * head_r * 0.38 * k
            c.ellipse(ex, hy + head_r * 0.16 * k, 6.2 * k, 7.6 * k, ink)
            c.circle(ex - 2.0 * k, hy + head_r * 0.05 * k, 2.4 * k, (255, 255, 255))
            c.ellipse(ex - sgn * 5.0 * k, hy + head_r * 0.54 * k, 7.0 * k, 4.0 * k,
                      skin_s)
        c.capsule(cx - 3.2 * k, hy + head_r * 0.74 * k, cx + 3.2 * k,
                  hy + head_r * 0.74 * k, 2.4 * k, ink)
    elif direction == DIR_UP:
        c.poly([(cx - head_r * 1.04 * k, hy - head_r * 0.14 * k),
                (cx - head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 1.04 * k, hy - head_r * 0.14 * k)], cap)
        c.rrect(cx - head_r * 1.12 * k, hy - head_r * 0.32 * k,
                cx + head_r * 1.12 * k, hy - head_r * 0.02 * k, 4.0 * k, cap_d)
        c.rrect(cx - head_r * 0.84 * k, hy - head_r * 0.06 * k,
                cx + head_r * 0.84 * k, hy + head_r * 0.44 * k, 3.0 * k, hair)
    else:
        sgn = -1.0 if direction == DIR_LEFT else 1.0
        c.poly([(cx - head_r * sgn * 0.96 * k, hy - head_r * 0.18 * k),
                (cx - head_r * sgn * 0.58 * k, hy - head_r * 1.1 * k),
                (cx + head_r * sgn * 0.5 * k, hy - head_r * 1.1 * k),
                (cx + head_r * sgn * 1.04 * k, hy - head_r * 0.18 * k)], cap)
        c.rrect(cx + head_r * sgn * 0.18 * k, hy - head_r * 0.36 * k,
                cx + head_r * sgn * 1.18 * k, hy + head_r * 0.02 * k, 4.0 * k, cap_d)
        c.ellipse(cx + head_r * sgn * 0.24 * k, hy + head_r * 0.16 * k, 5.6 * k,
                  7.0 * k, ink)
        c.circle(cx + head_r * sgn * 0.36 * k, hy + head_r * 0.03 * k, 2.2 * k,
                 (255, 255, 255))
        c.ellipse(cx + head_r * sgn * 0.72 * k, hy + head_r * 0.52 * k, 5.0 * k,
                  3.4 * k, skin_s)
    return c


def character_frame(cfg, direction, phase, w=CH_W, h=CH_H, scale=1.0):
    c = Canvas(w, h)
    draw_character(c, cfg, direction, phase, scale=scale)
    c.despeckle()
    c.outline(rgba(cfg["ink"], 235), 2.2)
    return c


def build_character_sheet(cfg, frames=4, w=CH_W, h=CH_H, scale=1.0):
    """4 columns (walk frames) x 4 rows (Down, Left, Right, Up)."""
    sheet = Canvas(frames * w, 4 * h)
    for d in range(4):
        for f in range(frames):
            fr = character_frame(cfg, d, f, w, h, scale)
            sheet.blit(fr, f * w, (3 - d) * h)      # Unity rows count bottom-up
    return sheet


def gen_chars():
    """Player + professor + a few NPC recolours, plus a 1-frame portrait each."""
    jobs = [("player_walk", PLAYER_CFG), ("professor_walk", PROF_CFG)]
    for i, hue in enumerate((0.02, 0.33, 0.58, 0.77, 0.11, 0.92)):
        jobs.append(("npc_%02d_walk" % i, npc_cfg(hue)))

    out = os.path.join(ART, "Characters")
    for name, cfg in jobs:
        t0 = time.time()
        sheet = build_character_sheet(cfg)
        sheet.save(os.path.join(out, name + ".png"))
        print("    %-18s %dx%d (4x4 frames)  %.1fs"
              % (name, sheet.w, sheet.h, time.time() - t0))

    # a single idle frame (facing south) for menus / dialogue portraits
    for name, cfg in (("player_idle", PLAYER_CFG), ("professor_idle", PROF_CFG)):
        fr = character_frame(cfg, DIR_DOWN, 0, CH_W, CH_H, 1.6)
        fr.save(os.path.join(out, name + ".png"))
    print("    idle frames saved (192x240 @1.6x -> %dx%d)"
          % (int(CH_W * 1.6), int(CH_H * 1.6)))

    meta = {"frame_w": CH_W, "frame_h": CH_H, "ppu": CH_PPU, "cols": 4, "rows": 4,
            "rows_order": DIR_NAMES[::-1],
            "note": "4 walk frames per row; rows bottom-up = Down,Left,Right,Up"}
    with open(os.path.join(OUT, "characters.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    return meta


# --------------------------------------------------------------------------- #
#  Creatures -- 12 designs x (base, evolved).  Types follow the design doc:
#  Flame, Tide, Leaf, Stone, Spark.
# --------------------------------------------------------------------------- #

CREATURE_PX = 384
CREATURE_PPU = 192            # 384/192 = 2.0 x 2.0 world units

TYPE_SPECS = {
    "Flame": {"body": (232, 96, 52), "belly": (255, 198, 128),
              "trim": (176, 44, 36), "accent": (255, 216, 96)},
    "Tide": {"body": (58, 148, 226), "belly": (186, 232, 255),
             "trim": (30, 96, 172), "accent": (140, 226, 240)},
    "Leaf": {"body": (86, 176, 78), "belly": (206, 240, 168),
             "trim": (46, 116, 50), "accent": (250, 232, 120)},
    "Stone": {"body": (168, 138, 108), "belly": (214, 196, 170),
              "trim": (110, 88, 66), "accent": (140, 140, 148)},
    "Spark": {"body": (242, 200, 62), "belly": (255, 244, 190),
              "trim": (196, 146, 26), "accent": (120, 210, 255)},
}

# (name, type, stage, seed) -- 12 base forms plus one evolution each.
CREATURES = [
    ("Cindle", "Flame", 1, 11), ("Cindrake", "Flame", 2, 12),
    ("Emberling", "Flame", 1, 13), ("Emberwyrm", "Flame", 2, 14),
    ("Drippa", "Tide", 1, 21), ("Drippool", "Tide", 2, 22),
    ("Ripple", "Tide", 1, 23), ("Tidalisk", "Tide", 2, 24),
    ("Mossling", "Leaf", 1, 31), ("Mosshulk", "Leaf", 2, 32),
    ("Sproutle", "Leaf", 1, 33), ("Sproutree", "Leaf", 2, 34),
    ("Pebbit", "Stone", 1, 41), ("Pebboulder", "Stone", 2, 42),
    ("Shale", "Stone", 1, 43), ("Shaleguard", "Stone", 2, 44),
    ("Zapkin", "Spark", 1, 51), ("Zapzilla", "Spark", 2, 52),
    ("Voltike", "Spark", 1, 53), ("Voltivault", "Spark", 2, 54),
    ("Glomoth", "Leaf", 1, 61), ("Glomothra", "Leaf", 2, 62),
    ("Bramble", "Leaf", 1, 63), ("Bramblethorn", "Leaf", 2, 64),
]


def creature_frame(name, ctype, stage, seed, px=CREATURE_PX):
    """Draw one creature. Stage 2 forms are broader, horned and meaner."""
    spec = TYPE_SPECS[ctype]
    body = rgba(spec["body"])
    belly = rgba(spec["belly"])
    trim = rgba(spec["trim"])
    accent = rgba(spec["accent"])
    ink = (34, 28, 26, 255)

    c = Canvas(px, px)
    cx = px * 0.5
    scale = 0.74 if stage == 1 else 0.88
    R = px * 0.30 * scale
    ground_y = px * 0.93          # ankle line: feet straddle it
    seed = seed + sum(ord(ch) for ch in name) % 101   # per-creature variation

    # --- shadow + feet (body is anchored just above the ankles) ------------ #
    c.ellipse(cx, ground_y + R * 0.10, R * 0.98, R * 0.20, (18, 20, 28, 90))
    for sgn in (-1, 1):
        c.ellipse(cx + sgn * R * 0.44, ground_y - R * 0.04, R * 0.30, R * 0.19, trim)

    by = ground_y - R * 1.05
    c.ellipse(cx, by, R, R * (0.96 if stage == 1 else 1.06), body)
    c.ellipse(cx, by + R * 0.28, R * 0.62, R * 0.56, belly)
    for sgn in (-1, 1):
        c.circle(cx + sgn * R * 0.94, by + R * 0.06,
                 R * 0.24 * (0.9 + 0.2 * stage), body)

    # --- head + eyes ------------------------------------------------------ #
    hy = by - R * (0.92 if stage == 1 else 1.0)
    hr = R * (0.72 if stage == 1 else 0.8)
    c.circle(cx, hy, hr, body)
    c.ellipse(cx, hy + hr * 0.36, hr * 0.62, hr * 0.42, belly)
    for sgn in (-1, 1):
        ex = cx + sgn * hr * 0.42
        ey = hy - hr * 0.08
        c.ellipse(ex, ey, hr * 0.30, hr * 0.34, (252, 252, 255, 255))
        c.circle(ex + sgn * hr * 0.05, ey + hr * 0.05, hr * 0.16, ink)
        c.circle(ex - sgn * hr * 0.06, ey - hr * 0.09, hr * 0.06,
                 (255, 255, 255, 255))

    draw_creature_features(c, ctype, stage, seed, cx, by, hy, R, hr,
                           body, belly, trim, accent)

    c.despeckle()
    c.outline(ink, 3.0)
    return c


def draw_creature_features(c, ctype, stage, seed, cx, by, hy, R, hr,
                           body, belly, trim, accent):
    """Per-type silhouette details so each type reads at a glance."""
    if ctype == "Flame":
        for k in range(3):
            fh = hr * (1.0 - k * 0.24)
            fx = cx + (artlib._hash2(k, seed, 7) - 0.5) * hr * 0.9
            c.poly([(fx - hr * 0.32, hy - hr * 0.5),
                    (fx, hy - hr * 0.5 - fh),
                    (fx + hr * 0.32, hy - hr * 0.5)],
                   accent if k % 2 == 0 else body)
        c.ellipse(cx, by + R * 0.1, R * 0.3, R * 0.5, rgba(accent, 150))
    elif ctype == "Tide":
        c.poly([(cx - R * 0.36, by - R * 0.9), (cx, by - R * 1.5),
                (cx + R * 0.36, by - R * 0.9)], accent)
        for sgn in (-1, 1):
            c.ellipse(cx + sgn * hr * 0.78, hy + hr * 0.5, hr * 0.22, hr * 0.3,
                      accent)
        c.ellipse(cx, by + R * 0.34, R * 0.46, R * 0.3, rgba(accent, 120))
    elif ctype == "Leaf":
        c.capsule(cx, hy - hr * 0.9, cx, hy - hr * 1.28, hr * 0.09, trim)
        for sgn in (-1, 1):
            c.ellipse(cx + sgn * hr * 0.44, hy - hr * 1.14, hr * 0.42, hr * 0.22,
                      accent)
        c.ellipse(cx, hy - hr * 1.30, hr * 0.24, hr * 0.18, accent)
        if stage == 2:
            for sgn in (-1, 1):
                c.ellipse(cx + sgn * R * 0.86, by + R * 0.34, R * 0.34, R * 0.2,
                          accent)
    elif ctype == "Stone":
        for k in range(3):
            a = math.pi * (0.18 + 0.32 * k)
            pxc = cx + math.cos(a) * R * 0.62
            pyc = by + math.sin(a) * R * 0.5 - R * 0.1
            s = R * 0.26
            c.poly([(pxc, pyc - s), (pxc + s * 0.9, pyc - s * 0.3),
                    (pxc + s * 0.6, pyc + s * 0.7), (pxc - s * 0.8, pyc + s * 0.5),
                    (pxc - s * 0.9, pyc - s * 0.4)], trim)
        if stage == 2:
            for sgn in (-1, 1):
                c.poly([(cx + sgn * hr * 0.66, hy - hr * 0.7),
                        (cx + sgn * hr * 1.3, hy - hr * 1.5),
                        (cx + sgn * hr * 0.98, hy - hr * 0.4)], trim)
    else:  # Spark
        for k in range(5):
            a = -math.pi * 0.5 + (k - 2) * 0.5
            c.poly([(cx + math.cos(a) * hr * 0.7, hy + math.sin(a) * hr * 0.7),
                    (cx + math.cos(a) * hr * 1.5, hy + math.sin(a) * hr * 1.5),
                    (cx + math.cos(a + 0.32) * hr * 0.74,
                     hy + math.sin(a + 0.32) * hr * 0.74)], accent)
        c.poly([(cx - hr * 0.4, by + R * 0.5), (cx + hr * 0.12, by + R * 0.86),
                (cx - hr * 0.1, by + R * 0.86), (cx + hr * 0.42, by + R * 1.24),
                (cx + hr * 0.02, by + R * 0.78), (cx + hr * 0.24, by + R * 0.78)],
               accent)

    if stage == 2:
        # horns + a heavier build so evolutions read as "bigger, meaner"
        for sgn in (-1, 1):
            c.poly([(cx + sgn * hr * 0.52, hy - hr * 0.66),
                    (cx + sgn * hr * 0.96, hy - hr * 1.42),
                    (cx + sgn * hr * 1.12, hy - hr * 0.6)], trim)
        c.ellipse(cx, by + R * 0.42, R * 0.5, R * 0.16, rgba(accent, 110))


def gen_creatures():
    out = os.path.join(ART, "Creatures")
    index = []
    for name, ctype, stage, seed in CREATURES:
        t0 = time.time()
        fr = creature_frame(name, ctype, stage, seed)
        fr.save(os.path.join(out, name + ".png"))
        index.append({"name": name, "type": ctype, "stage": stage,
                      "file": name + ".png", "px": CREATURE_PX,
                      "ppu": CREATURE_PPU})
        print("    %-14s %-6s stage%d  %dx%d  %.1fs"
              % (name, ctype, stage, fr.w, fr.h, time.time() - t0))
    with open(os.path.join(OUT, "creatures.json"), "w") as fh:
        json.dump({"px": CREATURE_PX, "ppu": CREATURE_PPU, "creatures": index},
                  fh, indent=2)
    return index


# --------------------------------------------------------------------------- #
#  UI -- minimap mask/bezel, HUD panel, gate icons, map arrow
# --------------------------------------------------------------------------- #

UI = 512


def gen_ui():
    out = os.path.join(ART, "UI")
    meta = {}

    # --- circular mask (white disc, transparent outside) ------------------ #
    mask = Canvas(UI, UI)
    mask.circle(UI * 0.5, UI * 0.5, UI * 0.5 - 1.0, (255, 255, 255, 255))
    mask.save(os.path.join(out, "minimap_mask.png"))

    # --- bezel + compass ring --------------------------------------------- #
    bz = Canvas(UI, UI)
    R = UI * 0.5 - 2.0
    bz.ring(UI * 0.5, UI * 0.5, R - 9.0, 20.0, (28, 34, 46, 235))
    bz.ring(UI * 0.5, UI * 0.5, R - 19.0, 3.0, (196, 214, 240, 200))
    # cardinal ticks: longer at N/S, shorter at E/W
    for k in range(8):
        a = -math.pi * 0.5 + k * math.pi / 4.0
        major = (k % 2 == 0)
        rad_out = R - 21.0
        rad_in = rad_out - (26.0 if major else 14.0)
        col = (255, 236, 176, 255) if k == 0 else (206, 220, 238, 210)
        bz.line(UI * 0.5 + math.cos(a) * rad_in, UI * 0.5 + math.sin(a) * rad_in,
                UI * 0.5 + math.cos(a) * rad_out, UI * 0.5 + math.sin(a) * rad_out,
                6.0 if major else 4.0, col)
    # north marker: a filled triangle sitting on the ring
    tip = UI * 0.5 - R + 12.0
    bz.poly([(UI * 0.5, tip - 26.0), (UI * 0.5 - 17.0, tip + 12.0),
             (UI * 0.5 + 17.0, tip + 12.0)], (236, 92, 78, 255))
    bz.poly([(UI * 0.5, tip - 26.0), (UI * 0.5 - 17.0, tip + 12.0),
             (UI * 0.5, tip + 12.0)], (186, 58, 48, 255))
    bz.save(os.path.join(out, "minimap_bezel.png"))

    # --- player arrow marker ---------------------------------------------- #
    ar = Canvas(128, 128)
    ac = 64.0
    ar.poly([(ac, ac - 30.0), (ac + 24.0, ac + 26.0), (ac, ac + 14.0),
             (ac - 24.0, ac + 26.0)], (255, 246, 214, 255))
    ar.poly([(ac, ac - 30.0), (ac + 24.0, ac + 26.0), (ac, ac + 14.0)],
            (232, 190, 92, 255))
    ar.outline((28, 34, 46, 240), 3.0)
    ar.save(os.path.join(out, "minimap_arrow.png"))

    # --- 9-slice HUD panel ------------------------------------------------- #
    P = 128
    pn = Canvas(P, P)
    pn.rect(0, 0, P, P, (18, 22, 32, 214))
    pn.rrect(1.0, 1.0, P - 1.0, P - 1.0, 12.0, (18, 22, 32, 214))
    pn.rrect(3.0, 3.0, P - 3.0, P - 3.0, 11.0, (60, 72, 96, 190))
    pn.rrect(5.0, 5.0, P - 5.0, P - 5.0, 10.0, (18, 22, 32, 214))
    pn.save(os.path.join(out, "hud_panel.png"))
    meta["hud_panel_border"] = 20

    # --- gate state icons -------------------------------------------------- #
    for name, open_state in (("gate_locked", False), ("gate_open", True)):
        g = Canvas(256, 256)
        body_col = (86, 190, 120, 255) if open_state else (206, 78, 62, 255)
        g.rrect(66.0, 118.0, 190.0, 224.0, 16.0, body_col)
        g.rrect(72.0, 124.0, 184.0, 218.0, 12.0, shade(body_col, 1.18))
        if open_state:
            # shackle swung open to the right
            g.ring(176.0, 96.0, 40.0, 16.0, (196, 204, 216, 255))
            g.rect(60.0, 96.0, 96.0, 128.0, (0, 0, 0, 0))
        else:
            g.ring(128.0, 96.0, 40.0, 16.0, (196, 204, 216, 255))
            g.rect(60.0, 96.0, 196.0, 122.0, (0, 0, 0, 0))
        g.circle(128.0, 168.0, 15.0, (30, 34, 44, 255))
        g.rrect(122.0, 168.0, 134.0, 200.0, 5.0, (30, 34, 44, 255))
        g.outline((24, 28, 36, 240), 3.0)
        g.save(os.path.join(out, name + ".png"))

    # --- type badge discs (used on creature cards and gates) -------------- #
    for ctype, spec in TYPE_SPECS.items():
        b = Canvas(192, 192)
        b.circle(96.0, 96.0, 88.0, rgba(spec["trim"], 255))
        b.circle(96.0, 96.0, 78.0, rgba(spec["body"], 255))
        b.ellipse(74.0, 70.0, 34.0, 22.0, rgba(spec["belly"], 190))
        b.save(os.path.join(out, "type_%s.png" % ctype.lower()))

    with open(os.path.join(OUT, "ui.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    print("    minimap mask/bezel/arrow, hud panel, gate icons, 5 type badges")
    return meta


# --------------------------------------------------------------------------- #
#  World props -- trees, bushes, rocks, signs, flowers, gate barriers
# --------------------------------------------------------------------------- #

def prop_tree(w=256, h=320, seed=7):
    c = Canvas(w, h)
    trunk_w = w * 0.11
    c.capsule(w * 0.5 - trunk_w, h * 0.72, w * 0.5 + trunk_w, h * 0.99,
              trunk_w, PAL["trunk"])
    c.capsule(w * 0.5 - trunk_w * 0.4, h * 0.78, w * 0.5 + trunk_w * 0.4, h * 0.96,
              trunk_w * 0.5, PAL["trunk_hi"])
    blobs = [(0.50, 0.40, 0.30), (0.28, 0.52, 0.22), (0.72, 0.52, 0.22),
             (0.40, 0.26, 0.22), (0.63, 0.27, 0.21), (0.50, 0.60, 0.19)]
    for i, (bx, by, br) in enumerate(blobs):
        tone = [PAL["forest_a"], PAL["forest_hi"], shade(PAL["forest_a"], 0.82)][i % 3]
        c.circle(w * bx, h * by, w * br, tone)
    for i, (bx, by, br) in enumerate(blobs):
        c.ellipse(w * bx - w * br * 0.22, h * by - h * br * 0.24,
                  w * br * 0.42, h * br * 0.3, shade(PAL["forest_hi"], 1.14))
    c.outline(rgba(PAL["ink"], 225), 3.0)
    return c


def prop_bush(w=192, h=160, seed=8):
    c = Canvas(w, h)
    for i, (bx, by, br) in enumerate([(0.32, 0.62, 0.32), (0.62, 0.60, 0.30),
                                      (0.48, 0.42, 0.30)]):
        c.circle(w * bx, h * by, w * br,
                 [PAL["forest_a"], PAL["forest_hi"]][i % 2])
    for i in range(5):
        a = artlib._hash2(i, 1, seed)
        c.circle(w * (0.22 + a * 0.56), h * (0.34 + a * 0.18), w * 0.055,
                 rgba(PAL["forest_b"], 210))
    c.outline(rgba(PAL["ink"], 215), 3.0)
    return c


def prop_rock(w=192, h=160, seed=9):
    c = Canvas(w, h)
    c.poly([(w * 0.12, h * 0.92), (w * 0.06, h * 0.5), (w * 0.34, h * 0.18),
            (w * 0.7, h * 0.16), (w * 0.94, h * 0.5), (w * 0.88, h * 0.92)],
           PAL["rock_b"])
    c.poly([(w * 0.12, h * 0.92), (w * 0.26, h * 0.5), (w * 0.5, h * 0.22),
            (w * 0.7, h * 0.16), (w * 0.5, h * 0.62), (w * 0.44, h * 0.92)],
           PAL["rock_a"])
    c.poly([(w * 0.3, h * 0.28), (w * 0.62, h * 0.2), (w * 0.56, h * 0.36),
            (w * 0.34, h * 0.4)], PAL["rock_hi"])
    c.outline(rgba(PAL["ink"], 225), 3.0)
    return c


def prop_sign(w=160, h=192, seed=10):
    c = Canvas(w, h)
    c.capsule(w * 0.5, h * 0.55, w * 0.5, h * 0.99, w * 0.055, PAL["trunk"])
    c.rrect(w * 0.1, h * 0.1, w * 0.9, h * 0.6, 10.0, PAL["plank_b"])
    c.rrect(w * 0.13, h * 0.13, w * 0.87, h * 0.57, 9.0, PAL["plank_a"])
    for k in range(3):
        y = h * (0.24 + k * 0.11)
        c.rrect(w * 0.22, y, w * (0.5 + 0.26 * (k % 2)), y + h * 0.045, 4.0,
                shade(PAL["plank_b"], 0.72))
    c.outline(rgba(PAL["ink"], 225), 3.0)
    return c


def prop_flowers(w=128, h=128, seed=11):
    c = Canvas(w, h)
    for i in range(7):
        a = artlib._hash2(i, 1, seed)
        b = artlib._hash2(i, 2, seed)
        fx = w * (0.14 + a * 0.72)
        fy = h * (0.30 + b * 0.6)
        col = hsv((a * 0.8 + 0.02) % 1.0, 0.7, 0.95)
        for k in range(5):
            ang = k * math.tau / 5.0
            c.circle(fx + math.cos(ang) * w * 0.05, fy + math.sin(ang) * w * 0.05,
                     w * 0.038, col)
        c.circle(fx, fy, w * 0.028, (255, 236, 150, 255))
    c.outline(rgba(PAL["ink"], 200), 2.2)
    return c


def prop_gate(w=640, h=224, seed=12):
    """A two-tile barrier with a padlock plate -- the visual for a locked path."""
    c = Canvas(w, h)
    for x in (w * 0.06, w * 0.5, w * 0.94):
        c.rrect(x - w * 0.035, h * 0.16, x + w * 0.035, h * 0.98, 6.0,
                PAL["trunk"])
        c.rrect(x - w * 0.026, h * 0.18, x + w * 0.014, h * 0.96, 4.0,
                PAL["trunk_hi"])
    for k, y in enumerate((0.4, 0.66)):
        tone = mix(PAL["plank_b"], PAL["plank_a"], 0.4 + 0.3 * k)
        c.rrect(w * 0.03, h * y, w * 0.97, h * (y + 0.11), 5.0, tone)
        c.rect(w * 0.05, h * y + 3, w * 0.95, h * y + 7, shade(tone, 1.18))
    c.rrect(w * 0.36, h * 0.30, w * 0.64, h * 0.74, 12.0, PAL["gate_locked"])
    c.rrect(w * 0.375, h * 0.32, w * 0.625, h * 0.72, 10.0,
            shade(PAL["gate_locked"], 1.16))
    c.ring(w * 0.5, h * 0.40, w * 0.05, 11.0, (208, 214, 226, 255))
    c.circle(w * 0.5, h * 0.54, 12.0, shade(PAL["ink"], 0.9))
    c.outline(rgba(PAL["ink"], 230), 3.5)
    return c


def gen_props():
    out = os.path.join(ART, "World")
    jobs = [("tree", prop_tree()), ("bush", prop_bush()), ("rock", prop_rock()),
            ("sign", prop_sign()), ("flowers", prop_flowers()), ("gate", prop_gate())]
    for name, cvs in jobs:
        cvs.save(os.path.join(out, name + ".png"))
        print("    %-10s %dx%d" % (name, cvs.w, cvs.h))
    meta = {"props": [{"name": n, "w": c.w, "h": c.h, "ppu": 128} for n, c in jobs]}
    with open(os.path.join(OUT, "props.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    return meta


def gen_split_tiles(tile):
    """One PNG per terrain type -- lets Unity import every tile as Sprite:Single,
    which avoids the fragile grid-slicing API entirely."""
    out = os.path.join(ART, "Tiles")
    for idx, painter in enumerate(TILE_PAINTERS):
        fr = painter(tile, 1337 + idx * 4099)
        fr.save(os.path.join(out, "tile_%02d_%s.png" % (idx, TERRAIN_NAMES[idx].lower())))
    print("    wrote %d individual tile PNGs" % len(TILE_PAINTERS))

    # Collision-only tile: a plain opaque square. It is painted into an
    # invisible "Blockers" tilemap whose renderer is disabled, so it only ever
    # contributes collider geometry. Kept small (32px) to stay cheap.
    coll = Canvas(32, 32, (255, 255, 255, 255))
    coll.save(os.path.join(out, "tile_collision.png"))


def gen_split_characters():
    """One PNG per (direction, frame) so the walk cycle needs no slicing."""
    out = os.path.join(ART, "Characters")
    jobs = [("player", PLAYER_CFG), ("professor", PROF_CFG)]
    for i, hue in enumerate((0.02, 0.33, 0.58, 0.77, 0.11, 0.92)):
        jobs.append(("npc_%02d" % i, npc_cfg(hue)))

    count = 0
    for prefix, cfg in jobs:
        for d in range(4):
            for f in range(4):
                fr = character_frame(cfg, d, f, CH_W, CH_H, 1.0)
                fr.save(os.path.join(out, "%s_%s_%d.png"
                                     % (prefix, DIR_NAMES[d].lower(), f)))
                count += 1
    print("    wrote %d individual character frames" % count)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
```

