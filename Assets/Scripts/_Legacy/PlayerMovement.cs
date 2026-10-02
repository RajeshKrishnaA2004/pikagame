
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