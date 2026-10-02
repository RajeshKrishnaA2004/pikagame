
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float lookAheadDistance = 1f;

    private PlayerMovement playerMovement;
    private Vector3 lastPosition;

    void Start()
    {
        if (target != null)
        {
            playerMovement = target.GetComponent<PlayerMovement>();
            lastPosition = target.position;
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 movement = target.position - lastPosition;
        Vector3 lookAhead = Vector3.zero;

        if (movement.sqrMagnitude > 0.0001f)
            lookAhead = movement.normalized * lookAheadDistance;

        Vector3 desiredPosition =
            target.position + offset + lookAhead;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        lastPosition = target.position;
    }
}
  