using UnityEngine;

/// <summary>
/// All player physics: walking, jumping, dashing, grounded state.
/// Knows nothing about input devices or the command queue.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpVelocity = 14f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.15f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] private float groundCheckOffset = 0.5f;

    private Rigidbody2D body;
    private float moveInput;
    private float dashTimeLeft;
    private int dashDirection;
    private float defaultGravityScale;

    public bool IsGrounded { get; private set; }
    public bool IsDashing => dashTimeLeft > 0f;

    /// <summary>+1 facing right, -1 facing left. Never 0.</summary>
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        defaultGravityScale = body.gravityScale;
    }

    public void SetMoveInput(float direction)
    {
        moveInput = Mathf.Clamp(direction, -1f, 1f);
        if (moveInput > 0.01f) FacingDirection = 1;
        else if (moveInput < -0.01f) FacingDirection = -1;
    }

    /// <summary>Unconditional upward impulse. Validity lives in the caller (see JumpCommand).</summary>
    public void Jump()
    {
        CancelDash(); // spec: any command executing cancels an active dash first
        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
    }

    /// <summary>Horizontal burst with gravity suspended. Re-dashing restarts the timer.</summary>
    public void Dash(int direction)
    {
        CancelDash();
        dashDirection = direction;
        dashTimeLeft = dashDuration;
        body.gravityScale = 0f;
    }

    private void CancelDash()
    {
        if (!IsDashing) return;
        dashTimeLeft = 0f;
        body.gravityScale = defaultGravityScale;
    }

    private void FixedUpdate()
    {
        Vector2 checkCenter = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        IsGrounded = Physics2D.OverlapBox(checkCenter, groundCheckSize, 0f, groundLayer) != null;

        if (IsDashing)
        {
            // Move input is intentionally ignored while dashing (spec).
            body.linearVelocity = new Vector2(dashSpeed * dashDirection, 0f);
            dashTimeLeft -= Time.fixedDeltaTime;
            if (!IsDashing) body.gravityScale = defaultGravityScale;
        }
        else
        {
            body.linearVelocity = new Vector2(moveInput * moveSpeed, body.linearVelocity.y);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 checkCenter = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        Gizmos.DrawWireCube(checkCenter, groundCheckSize);
    }
}
