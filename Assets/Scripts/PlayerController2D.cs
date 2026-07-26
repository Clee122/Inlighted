using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController2D : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 70f;
    [SerializeField] private float turnAcceleration = 100f;

    [Header("Slope Movement")]
    [SerializeField] private float slopeCheckDistance = 0.5f;
    [SerializeField] private float maximumSlopeAngle = 50f;

    [Header("Slope Visual")]
    [SerializeField] private Transform slopeVisual;
    [SerializeField] private float visualRotationSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private bool isGrounded;
    private bool isOnWalkableSlope;
    private bool jumpQueued;

    private float moveInput;
    private float defaultGravityScale;
    private float slopeAngle;

    private Vector2 slopeNormal = Vector2.up;
    private Vector2 slopeDirection = Vector2.right;

    private Quaternion slopeVisualBaseRotation;

    public PauseManager Pauser;
    private bool IsPaused;

    private void Reset()
    {
        // Automatically assigning the Rigidbody reduces setup mistakes when this
        // controller is first placed on the Player.
        rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        // Movement cannot function without a Rigidbody, so this fallback keeps
        // the controller usable if the Inspector reference was left empty.
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        // The normal gravity value is saved because grounded slope movement
        // temporarily disables gravity to prevent unwanted downhill sliding.
        defaultGravityScale = rb.gravityScale;

        // The original visual rotation is preserved so slope rotation is added
        // on top of the character's intended prefab orientation.
        if (slopeVisual != null)
        {
            slopeVisualBaseRotation = slopeVisual.localRotation;
        }

        // Finding the pause manager at runtime avoids needing to reassign it each
        // time the Player prefab is placed in another scene.
        Pauser = FindFirstObjectByType<PauseManager>();
    }

    private void Update()
    {
        UpdateGroundedState();
    }

    private void FixedUpdate()
    {
        DetectSlope();
        ApplyMovement();
        ProcessQueuedJump();
    }

    private void LateUpdate()
    {
        // Applying the visual rotation after normal animation updates reduces the
        // chance of the Animator immediately replacing the slope angle.
        UpdateVisualSlopeRotation();
    }

    private void UpdateGroundedState()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        // The overlap circle determines whether jumping is allowed, while the
        // separate raycast provides the surface angle used for slope movement.
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );
    }

    private void DetectSlope()
    {
        // Slope information is reset every physics frame so an old angle is not
        // retained after reaching flat ground or leaving the platform.
        isOnWalkableSlope = false;
        slopeNormal = Vector2.up;
        slopeDirection = Vector2.right;
        slopeAngle = 0f;

        if (!isGrounded || groundCheck == null)
        {
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            slopeCheckDistance,
            groundLayer
        );

        if (!hit)
        {
            return;
        }

        slopeNormal = hit.normal;

        slopeAngle = Vector2.Angle(
            slopeNormal,
            Vector2.up
        );

        // This tangent points generally towards world-space right. Positive speed
        // moves right along the surface and negative speed moves left.
        slopeDirection = new Vector2(
            slopeNormal.y,
            -slopeNormal.x
        ).normalized;

        // Almost-flat surfaces use ordinary movement, while surfaces above the
        // maximum angle are not treated as walkable slopes.
        isOnWalkableSlope =
            slopeAngle > 0.1f &&
            slopeAngle <= maximumSlopeAngle;
    }

    private void ApplyMovement()
    {
        float targetSpeed = moveInput * moveSpeed;
        bool hasMovementInput = Mathf.Abs(moveInput) > 0.01f;

        if (isGrounded && isOnWalkableSlope)
        {
            ApplySlopeMovement(
                targetSpeed,
                hasMovementInput
            );
        }
        else
        {
            ApplyNormalMovement(
                targetSpeed,
                hasMovementInput
            );
        }
    }

    private void ApplyNormalMovement(
        float targetSpeed,
        bool hasMovementInput
    )
    {
        // Gravity must return to its normal value away from slopes so jumping,
        // falling and flat-ground movement continue to behave normally.
        rb.gravityScale = defaultGravityScale;

        float currentHorizontalSpeed = rb.linearVelocity.x;

        float movementRate = GetMovementRate(
            currentHorizontalSpeed,
            hasMovementInput
        );

        float newHorizontalSpeed = Mathf.MoveTowards(
            currentHorizontalSpeed,
            targetSpeed,
            movementRate * Time.fixedDeltaTime
        );

        // Only horizontal velocity is replaced so vertical movement from gravity,
        // jumping and falling remains intact.
        rb.linearVelocity = new Vector2(
            newHorizontalSpeed,
            rb.linearVelocity.y
        );
    }

    private void ApplySlopeMovement(
        float targetSpeed,
        bool hasMovementInput
    )
    {
        // Gravity would otherwise pull the player downhill even without input,
        // so it is disabled while firmly grounded on a walkable slope.
        rb.gravityScale = 0f;

        if (!hasMovementInput)
        {
            // Removing residual velocity keeps the player stationary at the point
            // where movement input was released.
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float currentSlopeSpeed = Vector2.Dot(
            rb.linearVelocity,
            slopeDirection
        );

        float movementRate = GetMovementRate(
            currentSlopeSpeed,
            hasMovementInput
        );

        float newSlopeSpeed = Mathf.MoveTowards(
            currentSlopeSpeed,
            targetSpeed,
            movementRate * Time.fixedDeltaTime
        );

        // Velocity follows the surface tangent so the player travels uphill and
        // downhill instead of forcing purely horizontal movement into the slope.
        rb.linearVelocity = slopeDirection * newSlopeSpeed;
    }

    private float GetMovementRate(
        float currentSpeed,
        bool hasMovementInput
    )
    {
        if (!hasMovementInput)
        {
            // Strong braking provides a short and predictable stopping distance.
            return deceleration;
        }

        if (
            Mathf.Abs(currentSpeed) > 0.01f &&
            Mathf.Sign(moveInput) != Mathf.Sign(currentSpeed)
        )
        {
            // Reversing must cancel existing momentum before travelling the other
            // way, so it uses a stronger response than normal acceleration.
            return turnAcceleration;
        }

        // Normal acceleration is used when starting or continuing in the same direction.
        return acceleration;
    }

    private void ProcessQueuedJump()
    {
        if (!jumpQueued)
        {
            return;
        }

        if (isGrounded)
        {
            // Gravity is restored before jumping so the player immediately returns
            // to normal airborne physics after leaving a slope.
            rb.gravityScale = defaultGravityScale;

            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                jumpForce
            );
        }

        // Clearing the request after one physics attempt prevents an airborne jump
        // press from being stored until the player lands.
        jumpQueued = false;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        // Input is stored here and applied during FixedUpdate so Rigidbody changes
        // remain synchronised with Unity's physics simulation.
        Vector2 input = context.ReadValue<Vector2>();
        moveInput = input.x;

        // This controller does not modify scale or left/right facing. The existing
        // animation or facing system remains responsible for that behaviour.
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        // Jump requests are accepted only while grounded so a mid-air press cannot
        // unexpectedly trigger another jump immediately after landing.
        if (isGrounded)
        {
            jumpQueued = true;
        }
    }

    private void UpdateVisualSlopeRotation()
    {
        if (slopeVisual == null)
        {
            Debug.LogWarning("Slope Visual has not been assigned.");
            return;
        }

        float targetSlopeAngle = 0f;

        if (isGrounded && isOnWalkableSlope)
        {
            targetSlopeAngle = Mathf.Atan2(
                slopeDirection.y,
                slopeDirection.x
            ) * Mathf.Rad2Deg;
        }

        Debug.Log(
            "Grounded: " + isGrounded +
            " | On slope: " + isOnWalkableSlope +
            " | Detected angle: " + targetSlopeAngle +
            " | Visual: " + slopeVisual.name
        );

        // This direct assignment removes smoothing from the test so we can confirm
        // whether the selected visual can be rotated at all by this script.
        slopeVisual.localRotation =
            slopeVisualBaseRotation *
            Quaternion.Euler(0f, 0f, targetSlopeAngle);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        // The green circle visualises the area used to decide whether jumping is allowed.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );

        // The cyan line visualises the raycast used to determine the slope normal.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            groundCheck.position,
            groundCheck.position +
            Vector3.down * slopeCheckDistance
        );
    }

    public void Pause(InputAction.CallbackContext context)
    {
        if (!context.started)
        {
            return;
        }

        // Player Input receives the action, while PauseManager remains responsible
        // for the actual menu and time-scale behaviour.
        if (Pauser == null)
        {
            Debug.LogWarning("PauseManager could not be found.");
            return;
        }

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

    public void ResetMovementInput()
    {
        // Stored input is cleared during respawn so movement and jump requests from
        // before death cannot continue when control returns.
        moveInput = 0f;
        jumpQueued = false;

        if (rb != null)
        {
            // Respawn restores gravity in case death happened while the player was
            // standing on a slope with gravity temporarily disabled.
            rb.gravityScale = defaultGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (slopeVisual != null)
        {
            // Respawn restores the visual's original prefab orientation so a slope
            // angle cannot carry into the respawn location.
            slopeVisual.localRotation = slopeVisualBaseRotation;
        }
    }
}