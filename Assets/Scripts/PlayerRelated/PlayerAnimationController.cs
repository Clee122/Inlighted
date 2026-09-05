using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator catMothAnimator;
    [SerializeField] private SpriteRenderer catMothSpriteRenderer;

    [Header("Light Beam Origin")]
    [SerializeField] private Transform beamOrigin;

    [Header("Hurt Visual Priority")]
    [SerializeField] private string hurtSortingLayerName = "DeathPlayer";
    [SerializeField] private int hurtOrderInLayer = 50;

    [Header("Death Visual Priority")]
    [SerializeField] private string deathSortingLayerName = "DeathPlayer";
    [SerializeField] private int deathOrderInLayer = 100;

    [Header("Movement Check")]
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private float runningThreshold = 0.2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private PlayerLifeSystem playerLifeSystem;

    private int originalOrderInLayer;
    private int originalSortingLayerID;

    // The original Beam Origin position is kept so only the horizontal side
    // changes when CatMoth turns around.
    private Vector3 beamOriginDefaultLocalPosition;

    private void Awake()
    {
        // The life system stays on the parent Player because animation events
        // need to tell it when hurt protection can safely finish.
        playerLifeSystem =
            GetComponent<PlayerLifeSystem>();

        // The Rigidbody2D is expected to be on the parent Player object.
        // This fallback keeps the script working if its Inspector reference is lost.
        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponent<Rigidbody2D>();
        }

        // The Animator is expected to be on the CatMoth Visual child object.
        if (catMothAnimator == null)
        {
            catMothAnimator =
                GetComponentInChildren<Animator>();
        }

        // The SpriteRenderer is also expected to be on the CatMoth Visual child.
        if (catMothSpriteRenderer == null)
        {
            catMothSpriteRenderer =
                GetComponentInChildren<SpriteRenderer>();
        }

        if (catMothSpriteRenderer != null)
        {
            // Normal sorting values are stored once so temporary Hurt and Death
            // priority can always return CatMoth to the original gameplay layer.
            originalOrderInLayer =
                catMothSpriteRenderer.sortingOrder;

            originalSortingLayerID =
                catMothSpriteRenderer.sortingLayerID;
        }

        if (beamOrigin != null)
        {
            // The original position allows the Beam origin to swap sides without
            // losing its intended height or distance from CatMoth.
            beamOriginDefaultLocalPosition =
                beamOrigin.localPosition;
        }

        Debug.Log(
            "ANIM CHECK 0: PlayerAnimationController Awake completed."
        );
    }

    private void Update()
    {
        if (
            catMothAnimator == null ||
            playerRigidbody == null
        )
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    "ANIM CHECK FAILED: Missing Animator or Rigidbody2D reference."
                );
            }

            return;
        }

        float horizontalVelocity =
            playerRigidbody.linearVelocity.x;

        float verticalVelocity =
            playerRigidbody.linearVelocity.y;

        // A small threshold prevents tiny leftover Rigidbody movement from
        // being interpreted as active running.
        bool isRunning =
            Mathf.Abs(horizontalVelocity) >
            runningThreshold;

        // Grounded animation state comes from actual ground contact instead of
        // vertical velocity because slopes can legitimately create Y movement.
        bool isGrounded =
            CheckGrounded();

        // Falling begins only once CatMoth is both airborne and descending.
        // This allows Jump/RunJump to represent ascent while Fall represents descent.
        bool isFalling =
            !isGrounded &&
            verticalVelocity < 0f;

        bool isDead =
            playerLifeSystem != null &&
            playerLifeSystem.IsDead();

        // These names must exactly match the Animator parameters.
        catMothAnimator.SetBool(
            "isRunning",
            isRunning
        );

        catMothAnimator.SetBool(
            "isGrounded",
            isGrounded
        );

        catMothAnimator.SetBool(
            "isFalling",
            isFalling
        );

        catMothAnimator.SetBool(
            "isDead",
            isDead
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "ANIM CHECK: Animator target = " +
                catMothAnimator.gameObject.name +
                " | X Velocity = " +
                horizontalVelocity +
                " | Y Velocity = " +
                verticalVelocity +
                " | isRunning = " +
                isRunning +
                " | isGrounded = " +
                isGrounded +
                " | isFalling = " +
                isFalling +
                " | isDead = " +
                isDead
            );
        }
    }

    private void LateUpdate()
    {
        FlipCatMothVisual();
        UpdateBeamOriginFacing();
    }

    private void FlipCatMothVisual()
    {
        if (
            catMothSpriteRenderer == null ||
            playerRigidbody == null
        )
        {
            return;
        }

        float horizontalVelocity =
            playerRigidbody.linearVelocity.x;

        // CatMoth faces right by default, so only leftward movement requires
        // flipping the visual sprite.
        if (horizontalVelocity > runningThreshold)
        {
            catMothSpriteRenderer.flipX = false;
        }
        else if (
            horizontalVelocity <
            -runningThreshold
        )
        {
            catMothSpriteRenderer.flipX = true;
        }
    }

    private void UpdateBeamOriginFacing()
    {
        if (
            beamOrigin == null ||
            catMothSpriteRenderer == null
        )
        {
            return;
        }

        Vector3 targetPosition =
            beamOriginDefaultLocalPosition;

        // The Beam origin follows CatMoth's facing direction so the ability
        // continues spawning beside the character's face after turning.
        targetPosition.x =
            catMothSpriteRenderer.flipX
                ? -Mathf.Abs(
                    beamOriginDefaultLocalPosition.x
                )
                : Mathf.Abs(
                    beamOriginDefaultLocalPosition.x
                );

        beamOrigin.localPosition =
            targetPosition;
    }

    public int GetFacingDirection()
    {
        // Dash can use visual facing when there is no current movement input.
        if (catMothSpriteRenderer == null)
        {
            return 1;
        }

        return
            catMothSpriteRenderer.flipX
                ? -1
                : 1;
    }

    public void SetFacingDirection(
        float horizontalDirection
    )
    {
        if (
            catMothSpriteRenderer == null ||
            Mathf.Abs(horizontalDirection) <= 0.01f
        )
        {
            return;
        }

        // Dash can begin before Rigidbody velocity changes, so updating the
        // sprite immediately prevents a backwards-looking first dash frame.
        catMothSpriteRenderer.flipX =
            horizontalDirection < 0f;
    }

    public void ResetFacingDirection()
    {
        if (catMothSpriteRenderer == null)
        {
            Debug.LogWarning(
                "ANIM CHECK FAILED: Cannot reset facing because CatMoth SpriteRenderer is missing."
            );

            return;
        }

        // Respawn always restores CatMoth's default right-facing direction.
        catMothSpriteRenderer.flipX = false;

        if (beamOrigin != null)
        {
            beamOrigin.localPosition =
                new Vector3(
                    Mathf.Abs(
                        beamOriginDefaultLocalPosition.x
                    ),
                    beamOriginDefaultLocalPosition.y,
                    beamOriginDefaultLocalPosition.z
                );
        }

        Debug.Log(
            "ANIM CHECK: CatMoth facing direction reset to default/right."
        );
    }

    public void PlayHurtAnimation()
    {
        if (catMothAnimator == null)
        {
            Debug.LogWarning(
                "ANIM CHECK FAILED: Cannot play hurt animation because CatMoth Animator is missing."
            );

            return;
        }

        // Hurt raises CatMoth above darkness before the animation starts so
        // damage feedback remains readable even when the hit occurs inside darkness.
        SetHurtVisualPriority();

        // Resetting first makes repeated valid hits reliably restart the one-shot
        // Hurt animation rather than leaving a stale trigger behind.
        catMothAnimator.ResetTrigger("hurt");
        catMothAnimator.SetTrigger("hurt");

        Debug.Log(
            "ANIM CHECK: Hurt animation trigger sent."
        );
    }

    public void PlayLightBurstAnimation()
    {
        if (catMothAnimator == null)
        {
            Debug.LogWarning(
                "ANIM CHECK FAILED: Cannot play Light Burst animation because CatMoth Animator is missing."
            );

            return;
        }

        // Light Burst is a one-shot ability reaction, so a Trigger starts the
        // animation once without changing or locking the player's movement state.
        catMothAnimator.ResetTrigger("lightBurst");
        catMothAnimator.SetTrigger("lightBurst");

        Debug.Log(
            "ANIM CHECK: Light Burst animation trigger sent."
        );
    }

    public void SetHurtVisualPriority()
    {
        if (catMothSpriteRenderer == null)
        {
            Debug.LogWarning(
                "ANIM CHECK FAILED: Cannot set hurt visual priority because CatMoth SpriteRenderer is missing."
            );

            return;
        }

        // Hurt temporarily renders above darkness and nearby gameplay effects
        // so losing health always has clear visual feedback.
        catMothSpriteRenderer.sortingLayerName =
            hurtSortingLayerName;

        catMothSpriteRenderer.sortingOrder =
            hurtOrderInLayer;
    }

    public void FinishHurtAnimation()
    {
        // This method is intended for an Animation Event placed on the final
        // frame of both HurtIdle and HurtRun so visual priority and damage
        // protection end together with the visible hurt reaction.
        if (
            playerLifeSystem != null &&
            !playerLifeSystem.IsDead()
        )
        {
            ResetHurtVisualPriority();
            playerLifeSystem.EndHurtInvulnerability();
        }

        Debug.Log(
            "ANIM CHECK: Hurt animation finished."
        );
    }

    public void ResetHurtVisualPriority()
    {
        if (catMothSpriteRenderer == null)
        {
            return;
        }

        // Hurt priority is temporary; normal gameplay sorting returns as soon
        // as the reaction animation has completed.
        catMothSpriteRenderer.sortingLayerID =
            originalSortingLayerID;

        catMothSpriteRenderer.sortingOrder =
            originalOrderInLayer;
    }

    public void SetDeathVisualPriority()
    {
        if (catMothSpriteRenderer == null)
        {
            Debug.LogWarning(
                "ANIM CHECK FAILED: Cannot set death visual priority because CatMoth SpriteRenderer is missing."
            );

            return;
        }

        // Death takes greater priority than Hurt because it needs to remain
        // visible throughout the dedicated death presentation.
        catMothSpriteRenderer.sortingLayerName =
            deathSortingLayerName;

        catMothSpriteRenderer.sortingOrder =
            deathOrderInLayer;

        Debug.Log(
            "ANIM CHECK: Death visual priority set. Sorting Layer = " +
            deathSortingLayerName +
            " | Order in Layer = " +
            deathOrderInLayer
        );
    }

    public void ResetVisualPriority()
    {
        if (catMothSpriteRenderer == null)
        {
            Debug.LogWarning(
                "ANIM CHECK FAILED: Cannot reset visual priority because CatMoth SpriteRenderer is missing."
            );

            return;
        }

        // Respawning restores the exact sorting values CatMoth used before any
        // temporary Hurt or Death priority was applied.
        catMothSpriteRenderer.sortingLayerID =
            originalSortingLayerID;

        catMothSpriteRenderer.sortingOrder =
            originalOrderInLayer;

        Debug.Log(
            "ANIM CHECK: CatMoth visual priority reset."
        );
    }

    private bool CheckGrounded()
    {
        if (groundCheckPoint == null)
        {
            // Returning true prevents a missing setup reference from permanently
            // trapping CatMoth in an airborne animation.
            return true;
        }

        // Only actual Ground-layer colliders should affect animation grounding.
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                groundCheckPoint.position,
                groundCheckRadius,
                groundLayer
            );

        foreach (Collider2D hit in hits)
        {
            // The Player and its children should never count as their own ground.
            if (
                hit.transform == transform ||
                hit.transform.IsChildOf(transform)
            )
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null)
        {
            return;
        }

        // The gizmo shows exactly where animation grounding is being sampled,
        // which helps diagnose unusual Jump/Fall transitions around slopes.
        Gizmos.color =
            Color.green;

        Gizmos.DrawWireSphere(
            groundCheckPoint.position,
            groundCheckRadius
        );
    }
}