using UnityEngine;

/// <summary>
/// Drives the 4-frame walk cycle for a direction from FOUR SEPARATE PNGs
/// (Player_down_0..3 etc.) rather than a sliced sheet, because the hand-made
/// art pack ships one file per frame.
///
/// Row order matches the rest of the project:
///   0 = Down, 1 = Left, 2 = Right, 3 = Up
/// so index = direction * 4 + frame.
///
/// The frame is derived from the step's own progress, so the animation always
/// completes exactly one 4-frame cycle per tile - it can never desync from the
/// movement, even if stepDuration changes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GridPlayerAnimator : MonoBehaviour
{
    [Tooltip("16 entries: 4 directions x 4 frames, in Down,Left,Right,Up order.")]
    public Sprite[] frames = new Sprite[0];

    public int framesPerDirection = 4;

    [Tooltip("Frame held while standing.")]
    public int idleFrame = 0;

    [Tooltip("Normalised step progress at which frame 1 begins (keeps a beat on frame 0).")]
    [Range(0f, 1f)]
    public float firstFrameAt = 0.15f;

    private SpriteRenderer spriteRenderer;
    private GridPlayerController controller;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        controller = GetComponent<GridPlayerController>();
        if (controller == null)
            controller = GridPlayerController.Instance;
    }

    void LateUpdate()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0)
            return;

        int perDir = Mathf.Max(1, framesPerDirection);
        int dir = 0;
        bool moving = false;
        float progress = 0f;

        if (controller != null)
        {
            dir = Mathf.Clamp(controller.FacingIndex, 0, 3);
            moving = controller.IsMoving;
            progress = controller.StepProgress;
        }

        int frame;
        if (!moving)
        {
            frame = Mathf.Clamp(idleFrame, 0, perDir - 1);
        }
        else
        {
            float span = 1f - firstFrameAt;
            float t = span > 0f ? Mathf.Clamp01((progress - firstFrameAt) / span) : progress;
            frame = Mathf.Clamp(Mathf.FloorToInt(t * perDir), 0, perDir - 1);
        }

        int index = dir * perDir + frame;
        if (index < 0 || index >= frames.Length)
            index = Mathf.Clamp(index, 0, frames.Length - 1);

        Sprite wanted = frames[index];
        if (wanted != null && spriteRenderer.sprite != wanted)
            spriteRenderer.sprite = wanted;
    }

    /// <summary>Assign the 16 frames at build time.</summary>
    public void SetFrames(Sprite[] newFrames, int perDirection)
    {
        frames = newFrames;
        framesPerDirection = Mathf.Max(1, perDirection);
    }
}
