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

    [Header("Air Control")]

    // Air movement has its own speed multiplier so CatMoth can keep strong
    // horizontal steering without forcing grounded movement to use the same feel.
    [SerializeField] private float airMoveSpeedMultiplier = 1f;

    // Separate airborne acceleration allows CatMoth to respond more quickly to
    // directional input in the air while leaving grounded acceleration unchanged.
    [SerializeField] private float airAcceleration = 65f;

    // Air deceleration controls how strongly CatMoth can correct an overshoot
    // after the player releases horizontal input during a jump or fall.
    [SerializeField] private float airDeceleration = 55f;

    // Air turning is intentionally tuneable separately because reversing direction
    // is one of the most important ways the player can correct a landing in mid-air.
    [SerializeField] private float airTurnAcceleration = 110f;

    // Near the top of a jump the player has the most time to judge the landing.
    // A small control boost here makes fine corrections easier without increasing
    // CatMoth's maximum horizontal speed.
    [SerializeField] private float apexAirControlMultiplier = 1.2f;

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

    // Coyote time gives the player a short grace period after leaving a platform.
    // This makes jumps near platform edges more forgiving without changing the
    // normal jump height, movement speed, or physics of the jump itself.
    [SerializeField] private float coyoteTime = 0.12f;

    // Jump buffering remembers a jump input shortly before CatMoth lands.
    // This prevents slightly early button presses from being lost and makes
    // consecutive platform jumps feel more responsive and forgiving.
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Air Gravity")]

    // Rising gravity stays close to the Rigidbody's normal gravity so the jump
    // still has a clear upward arc instead of feeling like CatMoth is flying.
    [SerializeField] private float riseGravityMultiplier = 1f;

    // Gravity becomes lighter close to zero vertical velocity so CatMoth spends
    // slightly longer around the top of the jump. This creates controllable hang
    // time without artificially freezing the Rigidbody at the apex.
    [SerializeField] private float apexGravityMultiplier = 0.55f;

    // This threshold defines how close CatMoth must be to zero vertical velocity
    // before the softer apex gravity and extra apex steering are used.
    [SerializeField] private float apexVelocityThreshold = 1.5f;

    // Falling gravity is deliberately below the default value for the current
    // light-and-airy direction. This replaces the previous heavier fall multiplier
    // so CatMoth descends more slowly and gives the player longer to correct a landing.
    [SerializeField] private float fallGravityMultiplier = 0.7f;

    // Releasing jump early applies somewhat stronger upward gravity so the player
    // can still choose a shorter jump. It is intentionally gentle so variable jump
    // height does not undermine the floaty movement direction.
    [SerializeField] private float releasedJumpGravityMultiplier = 1.35f;

    // Maximum fall speed prevents gravity from accelerating CatMoth indefinitely
    // during long drops. Keeping the downward speed predictable also makes it
    // easier to tune Cinemachine so the camera can continue following the player.
    [SerializeField] private float maximumFallSpeed = 20f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Audio")]
    [SerializeField] private AudioClip walkingSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landingSound;

    // Landing audio requires a short period of genuine airtime before it can play.
    // This filters out brief ground-check interruptions caused by spawning,
    // slopes, collider seams, or other tiny contact changes.
    [SerializeField] private float minimumAirTimeForLandingSound = 0.1f;

    // Walking audio checks actual Rigidbody movement rather than input alone.
    // This prevents footsteps from continuing when the player holds against
    // a wall or another system has successfully stopped their movement.
    [SerializeField] private float walkingAudioSpeedThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showMovementDebugLogs = false;

    private bool isGrounded;
    private bool isOnWalkableSlope;
    private bool jumpQueued;

    // Tracking whether jump is still held lets the controller support variable
    // jump height through gravity rather than abruptly cutting vertical velocity.
    private bool jumpHeld;

    // A successful jump temporarily prevents slope movement from reclaiming the
    // Rigidbody while the GroundCheck is still overlapping the take-off surface.
    // This stops slope movement from overwriting the upward jump velocity during
    // the short period before CatMoth has physically separated from the ground.
    private bool isLeavingGroundFromJump;

    // The coyote timer remembers how recently CatMoth was grounded.
    // While this value remains above zero, a jump can still be accepted even
    // if the player has only just moved beyond the edge of a platform.
    private float coyoteTimeCounter;

    // The jump buffer timer keeps a recent jump press available while airborne.
    // If CatMoth lands before this timer expires, the stored input can immediately
    // become a normal grounded jump instead of requiring another button press.
    private float jumpBufferCounter;

    // This records whether the player entered the air through a successful jump input.
    // It allows jumping to generate light without treating falling, knockback, or other
    // uncontrolled airborne movement as valid light-generating movement.
    private bool isInPlayerControlledJump;

    // The previous grounded state is stored so landing can be detected reliably.
    // Landing ends the player-controlled jump state and prevents it carrying into later falls.
    private bool wasGroundedLastFrame;

    // Airtime is tracked separately from the grounded state so landing audio
    // only plays after CatMoth has actually been airborne for a meaningful duration.
    private float currentAirTime;

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

        // Movement and jump inputs can interrupt an active heal so player intent
        // immediately returns CatMoth to normal movement instead of being discarded.
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
        UpdateJumpBuffer();

        // Walking audio is checked after grounded state so footsteps stop as soon
        // as CatMoth leaves the floor and resume only after valid grounded movement.
        UpdateWalkingAudio();
    }

    private void FixedUpdate()
    {
        DetectSlope();
        ApplyMovement();

        // Air gravity is chosen from rising, apex and falling phases before the
        // final fall-speed clamp so CatMoth's jump arc remains predictable.
        ApplyAirGravity();

        // Fall speed is limited after gravity has updated the Rigidbody.
        // This keeps long falls controlled without changing upward jump velocity.
        ApplyMaximumFallSpeed();

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

        if (isGrounded)
        {
            // Refreshing the timer whenever CatMoth is grounded ensures the full
            // grace period is available immediately after stepping off an edge.
            coyoteTimeCounter =
                coyoteTime;
        }
        else
        {
            // Once CatMoth leaves the ground, the grace period counts down.
            // Clamping it to zero keeps the timer predictable and avoids
            // unnecessary negative values accumulating during long falls.
            coyoteTimeCounter =
                Mathf.Max(
                    0f,
                    coyoteTimeCounter -
                    Time.deltaTime
                );
        }

        if (!isGrounded)
        {
            // Once CatMoth has physically separated from the take-off surface,
            // normal airborne movement can take over and the temporary slope
            // protection used during jump take-off is no longer needed.
            isLeavingGroundFromJump = false;

            // Airtime accumulates only while CatMoth is genuinely detected as airborne.
            // Very short losses of contact on slopes or collider seams will normally
            // remain below the landing-audio threshold and therefore stay silent.
            currentAirTime +=
                Time.deltaTime;
        }

        if (
            isGrounded &&
            !wasGroundedLastFrame
        )
        {
            // Landing always ends any temporary jump-detachment state so the next
            // grounded movement frame can use normal slope handling again.
            isLeavingGroundFromJump = false;

            // Landing audio only plays after enough actual airtime has passed.
            // This prevents the initial spawn and tiny slope/contact interruptions
            // from being treated as meaningful landings.
            if (
                currentAirTime >=
                minimumAirTimeForLandingSound &&
                landingSound != null &&
                AudioManager.Instance != null
            )
            {
                AudioManager.Instance.PlaySFX(
                    landingSound
                );
            }

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

            // Resetting airtime after a grounded transition prepares the counter
            // for the next real jump or fall.
            currentAirTime = 0f;
        }
        else if (isGrounded)
        {
            // Remaining grounded clears any tiny accumulated airtime so brief
            // slope or collider contact losses cannot carry into a later landing.
            currentAirTime = 0f;
        }
    }

    private void UpdateJumpBuffer()
    {
        if (jumpBufferCounter <= 0f)
        {
            return;
        }

        // A buffered input only remains valid for a deliberately short period.
        // Once the timer expires, clearing the queued jump prevents an old input
        // from causing CatMoth to jump much later when the player eventually lands.
        jumpBufferCounter =
            Mathf.Max(
                0f,
                jumpBufferCounter -
                Time.deltaTime
            );

        if (jumpBufferCounter <= 0f)
        {
            jumpQueued = false;
        }
    }

    private void UpdateWalkingAudio()
    {
        if (
            walkingSound == null ||
            rb == null ||
            AudioManager.Instance == null
        )
        {
            return;
        }

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
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
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
            isOnWalkableSlope &&
            !isLeavingGroundFromJump
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

        if (!isGrounded)
        {
            targetSpeed *=
                Mathf.Max(
                    0f,
                    airMoveSpeedMultiplier
                );
        }

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
        if (!isGrounded)
        {
            float airborneMovementRate;

            if (!hasMovementInput)
            {
                airborneMovementRate =
                    airDeceleration;
            }
            else if (
                Mathf.Abs(currentSpeed) > 0.01f &&
                Mathf.Sign(moveInput) !=
                Mathf.Sign(currentSpeed)
            )
            {
                airborneMovementRate =
                    airTurnAcceleration;
            }
            else
            {
                airborneMovementRate =
                    airAcceleration;
            }

            if (
                rb != null &&
                Mathf.Abs(rb.linearVelocity.y) <=
                Mathf.Max(
                    0f,
                    apexVelocityThreshold
                )
            )
            {
                airborneMovementRate *=
                    Mathf.Max(
                        0f,
                        apexAirControlMultiplier
                    );
            }

            return airborneMovementRate;
        }

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

    private void ApplyAirGravity()
    {
        if (rb == null)
        {
            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            return;
        }

        if (
            isGrounded &&
            isOnWalkableSlope
        )
        {
            return;
        }

        if (isGrounded)
        {
            rb.gravityScale =
                defaultGravityScale;

            return;
        }

        float verticalSpeed =
            rb.linearVelocity.y;

        float safeApexThreshold =
            Mathf.Max(
                0f,
                apexVelocityThreshold
            );

        if (
            Mathf.Abs(verticalSpeed) <=
            safeApexThreshold
        )
        {
            rb.gravityScale =
                defaultGravityScale *
                Mathf.Max(
                    0f,
                    apexGravityMultiplier
                );

            return;
        }

        if (verticalSpeed > 0f)
        {
            float activeRiseMultiplier =
                jumpHeld
                    ? riseGravityMultiplier
                    : releasedJumpGravityMultiplier;

            rb.gravityScale =
                defaultGravityScale *
                Mathf.Max(
                    0f,
                    activeRiseMultiplier
                );

            return;
        }

        rb.gravityScale =
            defaultGravityScale *
            Mathf.Max(
                0f,
                fallGravityMultiplier
            );
    }

    private void ApplyMaximumFallSpeed()
    {
        if (rb == null)
        {
            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            return;
        }

        if (rb.linearVelocity.y >= 0f)
        {
            return;
        }

        float allowedFallSpeed =
            Mathf.Max(
                0f,
                maximumFallSpeed
            );

        if (
            rb.linearVelocity.y <
            -allowedFallSpeed
        )
        {
            rb.linearVelocity =
                new Vector2(
                    rb.linearVelocity.x,
                    -allowedFallSpeed
                );
        }
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
            jumpQueued = false;
            jumpBufferCounter = 0f;
            return;
        }

        if (
            (
                isGrounded ||
                coyoteTimeCounter > 0f
            ) &&
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

            isLeavingGroundFromJump = true;

            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;
            jumpQueued = false;

            isInPlayerControlledJump = true;

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

            return;
        }

        if (jumpBufferCounter <= 0f)
        {
            jumpQueued = false;
        }
    }

    public void OnMove(
        InputAction.CallbackContext context
    )
    {
        Vector2 input =
            context.ReadValue<Vector2>();

        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling() &&
            Mathf.Abs(input.x) > 0.01f
        )
        {
            // Horizontal movement is treated as an intentional interruption of
            // healing. Cancelling first releases the channel movement lock so the
            // same input can immediately continue into ordinary movement.
            playerLightChannel.CancelForPlayerAction(
                "Movement input"
            );
        }

        // The movement input is recorded even when it has just cancelled healing.
        // This prevents the player from needing to release and press the key again.
        moveInput =
            input.x;
    }

    public void OnJump(
        InputAction.CallbackContext context
    )
    {
        if (context.canceled)
        {
            // Releasing jump does not cut velocity instantly. The airborne gravity
            // code instead increases rising gravity so shorter jumps remain smooth.
            jumpHeld = false;
            return;
        }

        if (!context.performed)
        {
            return;
        }

        jumpHeld = true;

        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            // Jump input intentionally interrupts healing instead of being discarded.
            // Cancelling first releases the movement lock so this same input can
            // proceed into the existing jump buffer and coyote-time logic below.
            playerLightChannel.CancelForPlayerAction(
                "Jump input"
            );
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // Beam remains a hard movement lock, so jumping cannot interrupt it.
            jumpQueued = false;
            jumpBufferCounter = 0f;
            jumpHeld = false;

            if (showMovementDebugLogs)
            {
                Debug.Log(
                    "Jump input was blocked because the Light Beam is active."
                );
            }

            return;
        }

        // Every valid jump press is briefly stored instead of requiring CatMoth
        // to already be grounded on the exact input frame.
        jumpQueued = true;
        jumpBufferCounter = jumpBufferTime;

        if (showMovementDebugLogs)
        {
            Debug.Log(
                "Jump input buffered for " +
                jumpBufferTime +
                " seconds."
            );
        }
    }

    public bool HasHorizontalMovementInput()
    {
        return Mathf.Abs(moveInput) > 0.01f;
    }

    public float GetHorizontalMovementInput()
    {
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
        return isOnWalkableSlope;
    }

    public Vector2 GetSlopeDirection()
    {
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
        jumpBufferCounter = 0f;
        jumpHeld = false;
        isLeavingGroundFromJump = false;
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
        jumpHeld = false;
        isLeavingGroundFromJump = false;
        isChannelingLocked = false;
        isInPlayerControlledJump = false;

        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        currentAirTime = 0f;

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