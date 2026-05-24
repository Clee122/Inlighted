using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController2D : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 25f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private bool isGrounded;
    private float moveInput;
    private float currentSpeed;
    private bool jumpQueued;

    public PauseManager Pauser;
    private bool IsPaused = false;

    private void Reset()
    {
        // This helps set up the script quickly when it is first added to the Player.
        // It reduces the chance of forgetting to assign the Rigidbody2D manually in the Inspector.
        rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        // The controller depends on Rigidbody2D for movement, so this fallback keeps the script usable
        // even if the reference was not assigned in the Inspector.
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // The pause manager is found at runtime so the Player Input pause action can talk to the pause system
        // without needing a manual reference every time the player prefab is placed in a scene.
        Pauser = FindFirstObjectByType<PauseManager>();
    }

    private void Update()
    {
        // Ground checking happens every frame so jump input can respond based on the player's current state.
        // This prevents the player from jumping unless they are actually touching the intended ground layer.
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (jumpQueued)
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }

            // The queue is cleared immediately because it is only meant to bridge input timing
            // between Update and physics, not store a jump until the player lands later.
            jumpQueued = false;
        }
    }

    private void FixedUpdate()
    {
        float targetSpeed = moveInput * moveSpeed;

        // Acceleration and deceleration make movement feel less abrupt than directly setting speed.
        // This gives the player smoother control while still keeping the prototype responsive.
        if (Mathf.Abs(moveInput) > 0.01f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.fixedDeltaTime);
        }

        // Horizontal movement is controlled here while keeping the current vertical velocity.
        // This avoids interfering with gravity, jumping, and falling.
        rb.linearVelocity = new Vector2(currentSpeed, rb.linearVelocity.y);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        // The movement input is stored instead of applied directly because physics movement should happen
        // inside FixedUpdate for more stable Rigidbody behaviour.
        Vector2 input = context.ReadValue<Vector2>();
        moveInput = input.x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        // Only allow jump input while grounded so a mid-air button press does not get stored
        // and accidentally trigger a second jump as soon as the player lands.
        if (isGrounded)
        {
            jumpQueued = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        // The ground check gizmo helps tune the radius and position in the editor.
        // This is useful because a slightly misplaced ground check can make jumping feel unreliable.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    public void Pause(InputAction.CallbackContext context)
    {
        Debug.Log("Pause button pressed");

        if (context.started)
        {
            // The player input owns the pause toggle here because it is the object receiving the input action.
            // It then delegates the actual menu/time-scale behaviour to PauseManager.
            if (!IsPaused)
            {
                Pauser.Pause();
            }
            else
            {
                Pauser.UnPause();
            }

            IsPaused = !IsPaused;
        }
    }

    public void ResetMovementInput()
    {
        // Respawn needs to clear stored movement values so input from before death
        // does not carry into the moment the controller is enabled again.
        moveInput = 0f;
        currentSpeed = 0f;
        jumpQueued = false;

        // Clearing Rigidbody velocity here gives respawn an extra safety reset,
        // especially if the player died while running, jumping, or falling.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}