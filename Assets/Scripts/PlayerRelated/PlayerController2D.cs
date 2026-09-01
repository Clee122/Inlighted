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

    // Visual slope rotation checks the surface beneath both sides of the player.
    // A genuine slope should support both probes, while a platform edge often
    // supports only one side and should therefore leave CatMoth visually upright.
    [SerializeField] private float slopeVisualProbeHalfWidth = 0.15f;

    // Both visual probes should detect approximately the same surface angle.
    // This prevents corners or irregular collider edges from being interpreted
    // as a genuine slope and rotating CatMoth unexpectedly.
    [SerializeField] private float slopeVisualAngleTolerance = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Audio")]
    [SerializeField] private AudioClip walkingSound;
    [SerializeField] private AudioClip jumpSound;

    // Walking audio checks actual Rigidbody movement rather than input alone.
    // This prevents footsteps from continuing when the player holds against
    // a wall or another system has successfully stopped their movement.
    [SerializeField] private float walkingAudioSpeedThreshold = 0.1f;

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

    // The dash temporarily takes direct control of the Rigidbody, so normal
    // movement must stop applying velocity until dash movement has finished.
    private PlayerDash playerDash;

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

        // Dash is kept in its own component because its movement has different
        // timing and collision requirements from normal running and jumping.
        playerDash =
            GetComponent<PlayerDash>();

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

        // Walking audio is checked after grounded state so footsteps stop as soon
        // as CatMoth leaves the floor and resume only after valid grounded movement.
        UpdateWalkingAudio();
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

    private void UpdateWalkingAudio()
    {
        // Leaving this empty is valid while the final audio assets are pending.
        // Returning here also prevents an unassigned walking sound from interfering
        // with another looping sound such as Light Channel.
        if (
            walkingSound == null ||
            rb == null ||
            AudioManager.Instance == null
        )
        {
            return;
        }

        // Actual Rigidbody movement is checked alongside movement input so holding
        // a direction against a wall does not incorrectly produce walking audio.
        bool isActuallyMoving =
            rb.linearVelocity.magnitude >
            walkingAudioSpeedThreshold;

        bool shouldPlayWalkingAudio =
            isGrounded &&
            isActuallyMoving &&
            HasHorizontalMovementInput() &&
            !isChannelingLocked;

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // A fired Beam completely freezes the player, so footsteps must stop
            // even when the movement key remains held for movement afterwards.
            shouldPlayWalkingAudio = false;
        }

        if (
            playerDash != null &&
            playerDash.IsDashing()
        )
        {
            // Dash remains experimental and is not treated as normal walking.
            // Preventing footsteps here avoids giving dash unintended audio.
            shouldPlayWalkingAudio = false;
        }

        if (shouldPlayWalkingAudio)
        {
            AudioManager.Instance.StartLoopingSFX(
                walkingSound
            );
        }
        else
        {
            AudioManager.Instance.StopLoopingSFX(
                walkingSound
            );
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
            playerDash != null &&
            playerDash.IsDashing()
        )
        {
            // PlayerDash owns Rigidbody position, velocity and gravity for the
            // short dash period. Normal movement must not fight against it.
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
            playerDash != null &&
            playerDash.IsDashing()
        )
        {
            // Jump cannot execute while dash owns movement. Clearing the queued
            // input prevents a delayed jump from firing immediately after dash.
            jumpQueued = false;
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

            // Jump audio happens only once the gameplay jump has successfully
            // applied upward velocity. Invalid or blocked jump input therefore
            // cannot produce a sound when CatMoth did not actually jump.
            if (
                jumpSound != null &&
                AudioManager.Instance != null
            )
            {
                AudioManager.Instance.PlaySFX(
                    jumpSound
                );
            }

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

        // Movement input continues being recorded during Beam and dash locks.
        // This lets held input resume immediately once normal movement returns.
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
            playerDash != null &&
            playerDash.IsDashing()
        )
        {
            // Dash is a committed movement action, so jumping is ignored until
            // normal player movement has returned.
            jumpQueued = false;

            if (showMovementDebugLogs)
            {
                Debug.Log(
                    "Jump input was blocked because the player is dashing."
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

    public float GetHorizontalMovementInput()
    {
        // Dash needs the actual left/right input value so it can choose direction
        // without duplicating movement-input handling in another component.
        return moveInput;
    }

    public bool IsInPlayerControlledJump()
    {
        return isInPlayerControlledJump;
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public bool IsOnWalkableSlope()
    {
        // Dash uses the same slope result as normal movement so grounded dashes
        // can follow the level surface instead of moving through or away from it.
        return isOnWalkableSlope;
    }

    public Vector2 GetSlopeDirection()
    {
        // Exposing the already-calculated tangent keeps dash movement consistent
        // with the direction used by ordinary slope movement.
        return slopeDirection;
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

        // Normal movement can continue using the centre slope check, but the
        // CatMoth visual only rotates when both sides detect a stable slope.
        // This prevents platform lips from being mistaken for real inclines.
        if (
            isGrounded &&
            TryGetStableVisualSlopeAngle(
                out float stableSlopeAngle
            )
        )
        {
            targetSlopeAngle =
                stableSlopeAngle;
        }

        if (showMovementDebugLogs)
        {
            Debug.Log(
                "Grounded: " +
                isGrounded +
                " | Movement slope: " +
                isOnWalkableSlope +
                " | Visual slope angle: " +
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

    private bool TryGetStableVisualSlopeAngle(
        out float visualSlopeAngle
    )
    {
        visualSlopeAngle = 0f;

        if (groundCheck == null)
        {
            return false;
        }

        Vector2 leftProbeOrigin =
            (Vector2)groundCheck.position +
            Vector2.left *
            slopeVisualProbeHalfWidth;

        Vector2 rightProbeOrigin =
            (Vector2)groundCheck.position +
            Vector2.right *
            slopeVisualProbeHalfWidth;

        // Both sides of CatMoth must find ground before the visual can rotate.
        // At a platform edge one probe should normally lose contact, preventing
        // CatMoth from adopting the angle of the collider's corner.
        RaycastHit2D leftHit =
            Physics2D.Raycast(
                leftProbeOrigin,
                Vector2.down,
                slopeCheckDistance,
                groundLayer
            );

        RaycastHit2D rightHit =
            Physics2D.Raycast(
                rightProbeOrigin,
                Vector2.down,
                slopeCheckDistance,
                groundLayer
            );

        if (
            !leftHit ||
            !rightHit
        )
        {
            return false;
        }

        float leftSlopeAngle =
            Vector2.Angle(
                leftHit.normal,
                Vector2.up
            );

        float rightSlopeAngle =
            Vector2.Angle(
                rightHit.normal,
                Vector2.up
            );

        // A large difference between the two normals usually means the probes
        // are sitting across a corner or collider boundary rather than one
        // continuous slope.
        if (
            Mathf.Abs(
                leftSlopeAngle -
                rightSlopeAngle
            ) >
            slopeVisualAngleTolerance
        )
        {
            return false;
        }

        Vector2 averagedNormal =
            (
                leftHit.normal +
                rightHit.normal
            ).normalized;

        float averagedSlopeAngle =
            Vector2.Angle(
                averagedNormal,
                Vector2.up
            );

        // Flat surfaces keep the character upright, while surfaces steeper
        // than the movement limit should not visually behave like walkable slopes.
        if (
            averagedSlopeAngle <= 0.1f ||
            averagedSlopeAngle > maximumSlopeAngle
        )
        {
            return false;
        }

        Vector2 visualSlopeDirection =
            new Vector2(
                averagedNormal.y,
                -averagedNormal.x
            ).normalized;

        visualSlopeAngle =
            Mathf.Atan2(
                visualSlopeDirection.y,
                visualSlopeDirection.x
            ) *
            Mathf.Rad2Deg;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color =
            Color.green;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );

        Gizmos.color =
            Color.cyan;

        // The centre ray remains the slope probe used by normal movement.
        Gizmos.DrawLine(
            groundCheck.position,
            groundCheck.position +
            Vector3.down *
            slopeCheckDistance
        );

        Vector3 leftProbeOrigin =
            groundCheck.position +
            Vector3.left *
            slopeVisualProbeHalfWidth;

        Vector3 rightProbeOrigin =
            groundCheck.position +
            Vector3.right *
            slopeVisualProbeHalfWidth;

        // The two additional rays verify that the visual is standing over one
        // continuous slope rather than only touching a platform corner.
        Gizmos.DrawLine(
            leftProbeOrigin,
            leftProbeOrigin +
            Vector3.down *
            slopeCheckDistance
        );

        Gizmos.DrawLine(
            rightProbeOrigin,
            rightProbeOrigin +
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

        IsPaused =
            !IsPaused;
    }

    public void ResetMovementInput()
    {
        moveInput = 0f;
        jumpQueued = false;
        isChannelingLocked = false;
        isInPlayerControlledJump = false;

        if (playerDash != null)
        {
            // Respawning clears active dash and cooldown state so temporary
            // movement information from the previous life cannot carry over.
            playerDash.ResetDashState();
        }

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

        // Respawning clears movement, so any active footstep loop should also
        // stop rather than carrying audio from the previous life into respawn.
        if (
            walkingSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.StopLoopingSFX(
                walkingSound
            );
        }

        Debug.Log(
            "Player movement input and player-controlled jump state were reset."
        );
    }

    private void OnDisable()
    {
        // PlayerRespawn temporarily disables this controller during death.
        // Stopping footsteps here prevents the walking loop from continuing
        // while CatMoth is dead or movement control is otherwise disabled.
        if (
            walkingSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.StopLoopingSFX(
                walkingSound
            );
        }
    }
}