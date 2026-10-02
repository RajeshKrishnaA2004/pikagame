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