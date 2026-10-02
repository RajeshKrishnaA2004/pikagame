
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