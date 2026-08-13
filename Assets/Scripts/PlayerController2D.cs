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

    [Header("Debug")]
    [SerializeField] private bool showMovementDebugLogs = false;

    private bool isGrounded;
    private bool isOnWalkableSlope;
    private bool jumpQueued;

    // This records whether the player entered the air through a successful jump input.
    // It allows jumping to generate light without treating falling, knockback, or other
    // uncontrolled airborne movement as valid light-generating movement.
    private bool isInPlayerControlledJump;

    // The previous grounded state is stored so landing can be detected reliably.
    // Landing ends the player-controlled jump state and prevents it carrying into later falls.
    private bool wasGroundedLastFrame;

    private float moveInput;
    private float defaultGravityScale;
    private float slopeAngle;

    private Vector2 slopeNormal = Vector2.up;
    private Vector2 slopeDirection = Vector2.right;

    private Quaternion slopeVisualBaseRotation;

    private PlayerLightChannel playerLightChannel;

    // The movement controller checks the Beam state so movement and jumping are
    // disabled while the fired Beam is active.
    private LightBeamController lightBeamController;

    private bool isChannelingLocked;

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
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            defaultGravityScale = rb.gravityScale;
        }
        else
        {
            Debug.LogError(
                "PlayerController2D could not find a Rigidbody2D. " +
                "Player movement and light-generation checks will not work."
            );
        }

        if (slopeVisual != null)
        {
            slopeVisualBaseRotation =
                slopeVisual.localRotation;
        }

        Pauser =
            FindFirstObjectByType<PauseManager>();

        // Movement and jump inputs check the channel state so they can be ignored
        // instead of ending the channel and moving the player on the same frame.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        // The Beam controller is checked separately from channeling because the
        // player should remain still for the full duration of a fired Beam.
        lightBeamController =
            GetComponent<LightBeamController>();

        if (showMovementDebugLogs)
        {
            Debug.Log(
                "PlayerController2D initialised."
            );
        }
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
        UpdateVisualSlopeRotation();
    }

    private void UpdateGroundedState()
    {
        wasGroundedLastFrame =
            isGrounded;

        if (groundCheck == null)
        {
            isGrounded = false;

            if (showMovementDebugLogs)
            {
                Debug.LogWarning(
                    "Ground Check has not been assigned, so the player is being treated as airborne."
                );
            }

            return;
        }

        isGrounded =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );

        if (
            isGrounded &&
            !wasGroundedLastFrame
        )
        {
            if (
                isInPlayerControlledJump &&
                showMovementDebugLogs
            )
            {
                Debug.Log(
                    "Player-controlled jump ended after landing. " +
                    "Airborne light generation has stopped."
                );
            }

            isInPlayerControlledJump = false;
        }
    }

    private void DetectSlope()
    {
        isOnWalkableSlope = false;
        slopeNormal = Vector2.up;
        slopeDirection = Vector2.right;
        slopeAngle = 0f;

        if (
            !isGrounded ||
            groundCheck == null
        )
        {
            return;
        }

        RaycastHit2D hit =
            Physics2D.Raycast(
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

        slopeAngle =
            Vector2.Angle(
                slopeNormal,
                Vector2.up
            );

        slopeDirection =
            new Vector2(
                slopeNormal.y,
                -slopeNormal.x
            ).normalized;

        isOnWalkableSlope =
            slopeAngle > 0.1f &&
            slopeAngle <= maximumSlopeAngle;
    }

    private void ApplyMovement()
    {
        if (rb == null)
        {
            return;
        }

        if (isChannelingLocked)
        {
            // Channeling removes horizontal movement while preserving vertical
            // gravity in case the platform underneath the player disappears.
            rb.gravityScale =
                defaultGravityScale;

            rb.linearVelocity =
                new Vector2(
                    0f,
                    rb.linearVelocity.y
                );

            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // The player stays completely still while the Beam is active so the
            // fired Beam remains lined up with the position where it was fired.
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            return;
        }

        float targetSpeed =
            moveInput * moveSpeed;

        bool hasMovementInput =
            Mathf.Abs(moveInput) > 0.01f;

        if (
            isGrounded &&
            isOnWalkableSlope
        )
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
        rb.gravityScale =
            defaultGravityScale;

        float currentHorizontalSpeed =
            rb.linearVelocity.x;

        float movementRate =
            GetMovementRate(
                currentHorizontalSpeed,
                hasMovementInput
            );

        float newHorizontalSpeed =
            Mathf.MoveTowards(
                currentHorizontalSpeed,
                targetSpeed,
                movementRate *
                Time.fixedDeltaTime
            );

        rb.linearVelocity =
            new Vector2(
                newHorizontalSpeed,
                rb.linearVelocity.y
            );
    }

    private void ApplySlopeMovement(
        float targetSpeed,
        bool hasMovementInput
    )
    {
        rb.gravityScale = 0f;

        if (!hasMovementInput)
        {
            rb.linearVelocity =
                Vector2.zero;

            return;
        }

        float currentSlopeSpeed =
            Vector2.Dot(
                rb.linearVelocity,
                slopeDirection
            );

        float movementRate =
            GetMovementRate(
                currentSlopeSpeed,
                hasMovementInput
            );

        float newSlopeSpeed =
            Mathf.MoveTowards(
                currentSlopeSpeed,
                targetSpeed,
                movementRate *
                Time.fixedDeltaTime
            );

        rb.linearVelocity =
            slopeDirection *
            newSlopeSpeed;
    }

    private float GetMovementRate(
        float currentSpeed,
        bool hasMovementInput
    )
    {
        if (!hasMovementInput)
        {
            return deceleration;
        }

        if (
            Mathf.Abs(currentSpeed) > 0.01f &&
            Mathf.Sign(moveInput) !=
            Mathf.Sign(currentSpeed)
        )
        {
            return turnAcceleration;
        }

        return acceleration;
    }

    private void ProcessQueuedJump()
    {
        if (!jumpQueued)
        {
            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // Clear any jump that was queued before the Beam fired so it cannot
            // happen during the shot or immediately after it ends.
            jumpQueued = false;
            return;
        }

        if (
            isGrounded &&
            rb != null
        )
        {
            rb.gravityScale =
                defaultGravityScale;

            rb.linearVelocity =
                new Vector2(
                    rb.linearVelocity.x,
                    jumpForce
                );

            isInPlayerControlledJump = true;

            Debug.Log(
                "Player-controlled jump started. " +
                "This jump can generate light while the player remains airborne."
            );
        }

        jumpQueued = false;
    }

    public void OnMove(
        InputAction.CallbackContext context
    )
    {
        Vector2 input =
            context.ReadValue<Vector2>();

        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            // Movement input is discarded while channeling rather than cancelling
            // the channel and allowing movement on the same frame.
            moveInput = 0f;
            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // Movement input is cleared while the Beam is active so held input
            // cannot continue moving the player during the shot.
            moveInput = 0f;
            return;
        }

        moveInput = input.x;
    }

    public void OnJump(
        InputAction.CallbackContext context
    )
    {
        if (!context.performed)
        {
            return;
        }

        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            // Jump input is ignored while channeling because healing requires the
            // player to remain grounded and committed to the channel action.
            jumpQueued = false;

            if (showMovementDebugLogs)
            {
                Debug.Log(
                    "Jump input was blocked because the player is channeling."
                );
            }

            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // Jump input is ignored for the duration of the fired Beam.
            jumpQueued = false;

            if (showMovementDebugLogs)
            {
                Debug.Log(
                    "Jump input was blocked because the Light Beam is active."
                );
            }

            return;
        }

        if (isGrounded)
        {
            jumpQueued = true;
        }
        else if (showMovementDebugLogs)
        {
            Debug.Log(
                "Jump input was ignored because the player was not grounded."
            );
        }
    }

    public bool HasHorizontalMovementInput()
    {
        return Mathf.Abs(moveInput) > 0.01f;
    }

    public bool IsInPlayerControlledJump()
    {
        return isInPlayerControlledJump;
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public bool IsActivelyGeneratingLight()
    {
        if (isChannelingLocked)
        {
            return false;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // Ability use does not count as movement for light regeneration.
            return false;
        }

        bool isRunningWithInput =
            isGrounded &&
            HasHorizontalMovementInput();

        bool isActivelyJumping =
            !isGrounded &&
            isInPlayerControlledJump;

        return
            isRunningWithInput ||
            isActivelyJumping;
    }

    public void SetChannelingLocked(
        bool shouldLock
    )
    {
        // The channel script controls only whether movement can be accepted.
        // Ground checks, gravity and the rest of the controller remain active.
        isChannelingLocked =
            shouldLock;

        if (!shouldLock)
        {
            return;
        }

        moveInput = 0f;
        jumpQueued = false;
        isInPlayerControlledJump = false;

        if (rb != null)
        {
            rb.gravityScale =
                defaultGravityScale;

            rb.linearVelocity =
                new Vector2(
                    0f,
                    rb.linearVelocity.y
                );
        }

        if (showMovementDebugLogs)
        {
            Debug.Log(
                "Player movement and jumping were locked for channeling."
            );
        }
    }

    private void UpdateVisualSlopeRotation()
    {
        if (slopeVisual == null)
        {
            if (showMovementDebugLogs)
            {
                Debug.LogWarning(
                    "Slope Visual has not been assigned."
                );
            }

            return;
        }

        float targetSlopeAngle = 0f;

        if (
            isGrounded &&
            isOnWalkableSlope
        )
        {
            targetSlopeAngle =
                Mathf.Atan2(
                    slopeDirection.y,
                    slopeDirection.x
                ) *
                Mathf.Rad2Deg;
        }

        if (showMovementDebugLogs)
        {
            Debug.Log(
                "Grounded: " + isGrounded +
                " | On slope: " +
                isOnWalkableSlope +
                " | Detected angle: " +
                targetSlopeAngle +
                " | Visual: " +
                slopeVisual.name
            );
        }

        slopeVisual.localRotation =
            slopeVisualBaseRotation *
            Quaternion.Euler(
                0f,
                0f,
                targetSlopeAngle
            );
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(
            groundCheck.position,
            groundCheck.position +
            Vector3.down *
            slopeCheckDistance
        );
    }

    public void Pause(
        InputAction.CallbackContext context
    )
    {
        if (!context.started)
        {
            return;
        }

        if (Pauser == null)
        {
            Debug.LogWarning(
                "PauseManager could not be found."
            );

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
        moveInput = 0f;
        jumpQueued = false;
        isChannelingLocked = false;
        isInPlayerControlledJump = false;

        if (rb != null)
        {
            rb.gravityScale =
                defaultGravityScale;

            rb.linearVelocity =
                Vector2.zero;

            rb.angularVelocity = 0f;
        }

        if (slopeVisual != null)
        {
            slopeVisual.localRotation =
                slopeVisualBaseRotation;
        }

        Debug.Log(
            "Player movement input and player-controlled jump state were reset."
        );
    }
}