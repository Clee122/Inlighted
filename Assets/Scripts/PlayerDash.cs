using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerDash : MonoBehaviour
{
    [Header("Dash Settings")]

    // Dash distance is intentionally designer-controlled because darkness-patch
    // sizes may change during level development. The Inspector range keeps
    // playtesting flexible without requiring code changes.
    [Range(0f, 10f)]
    [SerializeField] private float dashDistance = 2f;

    // The dash happens over a very short period so it feels close to a blink
    // while still remaining readable and compatible with Rigidbody movement.
    [Range(0.01f, 0.5f)]
    [SerializeField] private float dashDuration = 0.12f;

    // A short recovery prevents ground dash from becoming continuous movement
    // while keeping the ability responsive enough for small darkness obstacles.
    [Range(0f, 2f)]
    [SerializeField] private float dashCooldown = 0.45f;

    [Header("Dash Collision")]

    // This should contain solid level geometry such as ground and walls.
    // Darkness must not be included because dash is intended to cross it.
    [SerializeField] private LayerMask dashCollisionLayer;

    // A small gap prevents the Rigidbody from finishing a blocked dash embedded
    // directly against the collider that stopped it.
    [SerializeField] private float wallSkinWidth = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool showDashDebugLogs = false;

    private Rigidbody2D rb;
    private PlayerController2D playerController;
    private PlayerAnimationController playerAnimationController;
    private LightBeamController lightBeamController;
    private PlayerLightChannel playerLightChannel;

    private bool isDashing;
    private bool isOnCooldown;

    // Air dash is limited to one use until landing so the short cooldown cannot
    // be exploited to repeatedly dash across the level without touching ground.
    private bool airDashUsed;

    private Coroutine dashCoroutine;

    private ContactFilter2D dashContactFilter;

    // Multiple cast results are stored because complex geometry may return more
    // than one surface and the nearest valid collision needs to stop the dash.
    private readonly RaycastHit2D[] dashHits =
        new RaycastHit2D[8];

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody2D>();

        playerController =
            GetComponent<PlayerController2D>();

        playerAnimationController =
            GetComponent<PlayerAnimationController>();

        lightBeamController =
            GetComponent<LightBeamController>();

        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        // Rigidbody casting checks only solid geometry. This lets the player
        // pass through darkness while still preventing wall tunnelling.
        dashContactFilter =
            new ContactFilter2D();

        dashContactFilter.useLayerMask = true;

        dashContactFilter.SetLayerMask(
            dashCollisionLayer
        );

        dashContactFilter.useTriggers = false;

        if (rb == null)
        {
            Debug.LogError(
                "PlayerDash could not find Rigidbody2D on " +
                gameObject.name +
                ". Dash movement cannot work without it."
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                "PlayerDash could not find PlayerController2D on " +
                gameObject.name +
                ". Ground and slope checks will not work."
            );
        }
    }

    private void Update()
    {
        if (playerController == null)
        {
            return;
        }

        // Landing restores the single air dash. The reset waits until the active
        // dash has ended so touching ground during dash cannot grant another use.
        if (
            playerController.IsGrounded() &&
            !isDashing
        )
        {
            airDashUsed = false;
        }
    }

    public void OnDash(
        InputAction.CallbackContext context
    )
    {
        if (!context.performed)
        {
            return;
        }

        TryDash();
    }

    private void TryDash()
    {
        if (
            rb == null ||
            playerController == null
        )
        {
            return;
        }

        if (
            isDashing ||
            isOnCooldown
        )
        {
            if (showDashDebugLogs)
            {
                Debug.Log(
                    "Dash input was blocked because the dash is active or cooling down."
                );
            }

            return;
        }

        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            // Channeling commits the player to remaining still, so dash cannot
            // be used to escape the existing channel movement restriction.
            if (showDashDebugLogs)
            {
                Debug.Log(
                    "Dash input was blocked because the player is channeling."
                );
            }

            return;
        }

        if (
            lightBeamController != null &&
            lightBeamController.IsBeamActive()
        )
        {
            // Beam aiming is compatible with dash, but once the Beam has fired
            // its existing movement lock takes priority.
            if (showDashDebugLogs)
            {
                Debug.Log(
                    "Dash input was blocked because the Light Beam is active."
                );
            }

            return;
        }

        bool isGrounded =
            playerController.IsGrounded();

        if (
            !isGrounded &&
            airDashUsed
        )
        {
            if (showDashDebugLogs)
            {
                Debug.Log(
                    "Dash input was blocked because the air dash has already been used."
                );
            }

            return;
        }

        float horizontalDirection =
            GetDashHorizontalDirection();

        // The player's input remains left/right only. Ground slope information
        // adjusts the movement vector when necessary so the dash follows the
        // surface instead of travelling through or away from it.
        Vector2 dashDirection =
            GetDashMovementDirection(
                horizontalDirection
            );

        float availableDistance =
            GetAvailableDashDistance(
                dashDirection
            );

        // A solid surface directly beside the player should block the dash rather
        // than consuming cooldown for movement that could not actually occur.
        if (availableDistance <= 0.001f)
        {
            if (showDashDebugLogs)
            {
                Debug.Log(
                    "Dash was blocked because there is no safe space in the chosen direction."
                );
            }

            return;
        }

        if (!isGrounded)
        {
            airDashUsed = true;
        }

        // Facing is still based only on horizontal direction. The vertical part
        // of a slope-following dash should never make CatMoth face differently.
        if (playerAnimationController != null)
        {
            playerAnimationController.SetFacingDirection(
                horizontalDirection
            );
        }

        if (dashCoroutine != null)
        {
            StopCoroutine(
                dashCoroutine
            );
        }

        dashCoroutine =
            StartCoroutine(
                DashRoutine(
                    dashDirection,
                    availableDistance
                )
            );
    }

    private float GetDashHorizontalDirection()
    {
        float movementInput =
            playerController.GetHorizontalMovementInput();

        // Held movement input takes priority so the player can instantly dash
        // opposite their current facing direction.
        if (Mathf.Abs(movementInput) > 0.01f)
        {
            return Mathf.Sign(
                movementInput
            );
        }

        // Without movement input, dash continues in CatMoth's current facing
        // direction so standing dashes remain predictable.
        if (playerAnimationController != null)
        {
            return playerAnimationController.GetFacingDirection();
        }

        return 1f;
    }

    private Vector2 GetDashMovementDirection(
        float horizontalDirection
    )
    {
        // Air dashes stay completely horizontal as originally designed.
        if (!playerController.IsGrounded())
        {
            return
                Vector2.right *
                horizontalDirection;
        }

        if (!playerController.IsOnWalkableSlope())
        {
            // Flat-ground dash remains a normal horizontal dash.
            return
                Vector2.right *
                horizontalDirection;
        }

        Vector2 slopeDirection =
            playerController.GetSlopeDirection();

        // PlayerController2D stores one tangent direction for the slope.
        // Flip that tangent when necessary so pressing left always travels left
        // and pressing right always travels right regardless of slope orientation.
        if (
            Mathf.Sign(slopeDirection.x) !=
            Mathf.Sign(horizontalDirection)
        )
        {
            slopeDirection =
                -slopeDirection;
        }

        // Normalising keeps Dash Distance measured consistently along the slope
        // rather than allowing steeper slopes to alter the configured distance.
        return slopeDirection.normalized;
    }

    private float GetAvailableDashDistance(
    Vector2 dashDirection
)
    {
        float requestedDistance =
            Mathf.Clamp(
                dashDistance,
                0f,
                10f
            );

        if (requestedDistance <= 0f)
        {
            return 0f;
        }

        int hitCount =
            rb.Cast(
                dashDirection,
                dashContactFilter,
                dashHits,
                requestedDistance
            );

        float safeDistance =
            requestedDistance;

        for (
            int i = 0;
            i < hitCount;
            i++
        )
        {
            RaycastHit2D hit =
                dashHits[i];

            if (hit.collider == null)
            {
                continue;
            }

            // The player's Rigidbody is normally already touching the ground when a
            // grounded dash starts. Rigidbody.Cast can report that supporting surface
            // as a hit at almost zero distance, which previously caused valid dashes
            // to be cancelled or appear stuck in the same position.
            //
            // Only a surface whose normal substantially opposes the dash direction
            // should shorten the dash. Ground beneath the player and the walkable
            // slope currently being followed therefore do not count as blockers.
            float blockingAmount =
                Vector2.Dot(
                    hit.normal,
                    dashDirection
                );

            if (blockingAmount > -0.2f)
            {
                continue;
            }

            // Genuine walls or other surfaces facing against the dash still stop the
            // player before contact so the dash cannot pass through level geometry.
            float distanceBeforeBlocker =
                Mathf.Max(
                    0f,
                    hit.distance -
                    wallSkinWidth
                );

            safeDistance =
                Mathf.Min(
                    safeDistance,
                    distanceBeforeBlocker
                );

            if (showDashDebugLogs)
            {
                Debug.Log(
                    "Dash blocker detected: " +
                    hit.collider.gameObject.name +
                    " | Distance: " +
                    hit.distance.ToString("0.00") +
                    " | Surface normal: " +
                    hit.normal +
                    " | Blocking amount: " +
                    blockingAmount.ToString("0.00")
                );
            }
        }

        return safeDistance;
    }

    private IEnumerator DashRoutine(
        Vector2 dashDirection,
        float distance
    )
    {
        isDashing = true;
        isOnCooldown = true;

        float gravityBeforeDash =
            rb.gravityScale;

        // Gravity and previous velocity are temporarily removed so the dash
        // follows its chosen path consistently while grounded or airborne.
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        Vector2 startPosition =
            rb.position;

        Vector2 targetPosition =
            startPosition +
            dashDirection *
            distance;

        float elapsedTime = 0f;

        if (showDashDebugLogs)
        {
            Debug.Log(
                "Dash started. Distance: " +
                distance.ToString("0.00") +
                " | Direction: " +
                dashDirection
            );
        }

        while (elapsedTime < dashDuration)
        {
            float progress =
                dashDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsedTime /
                        dashDuration
                    );

            // Rigidbody movement keeps the dash integrated with the player's
            // existing 2D physics rather than teleporting the Transform directly.
            rb.MovePosition(
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    progress
                )
            );

            elapsedTime +=
                Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(
            targetPosition
        );

        rb.linearVelocity =
            Vector2.zero;

        rb.gravityScale =
            gravityBeforeDash;

        isDashing = false;

        if (showDashDebugLogs)
        {
            Debug.Log(
                "Dash movement ended."
            );
        }

        // Cooldown begins after movement finishes so the Inspector value
        // represents the actual recovery time between completed dashes.
        yield return new WaitForSeconds(
            dashCooldown
        );

        isOnCooldown = false;
        dashCoroutine = null;

        if (showDashDebugLogs)
        {
            Debug.Log(
                "Dash cooldown ended."
            );
        }
    }

    public bool IsDashing()
    {
        return isDashing;
    }

    public bool IsOnCooldown()
    {
        return isOnCooldown;
    }

    public void ResetDashState()
    {
        // Respawn clears active dash and cooldown state so movement information
        // from the previous life cannot carry over to the new spawn.
        if (dashCoroutine != null)
        {
            StopCoroutine(
                dashCoroutine
            );

            dashCoroutine = null;
        }

        isDashing = false;
        isOnCooldown = false;
        airDashUsed = false;

        if (rb != null)
        {
            rb.linearVelocity =
                Vector2.zero;
        }

        if (showDashDebugLogs)
        {
            Debug.Log(
                "Dash state reset."
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        // These lines show the configured distance on flat ground. Actual ground
        // dashes follow a detected slope while preserving the same total distance.
        Gizmos.color =
            Color.magenta;

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            Vector3.right *
            dashDistance
        );

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            Vector3.left *
            dashDistance
        );
    }
}