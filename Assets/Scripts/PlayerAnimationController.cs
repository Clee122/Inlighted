using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator catAnimator;
    [SerializeField] private SpriteRenderer catSpriteRenderer;

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

    private void Awake()
    {
        // The life system stays on the parent Player object because this script should only
        // read the player's state and convert it into animation parameters.
        playerLifeSystem = GetComponent<PlayerLifeSystem>();

        // The Rigidbody2D is expected to be on the parent Player object.
        // This fallback keeps the script working even if the reference was lost after undoing changes.
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        // The Animator is on the Cat Visual child object, not on the parent Player.
        // Finding it automatically avoids problems where the reference gets detached or cannot be dragged in.
        if (catAnimator == null)
        {
            catAnimator = GetComponentInChildren<Animator>();
        }

        // The SpriteRenderer is also on the Cat Visual child object.
        // This lets the script flip the cat sprite without changing the parent Player transform.
        if (catSpriteRenderer == null)
        {
            catSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        if (catAnimator == null || playerRigidbody == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("PlayerAnimationController is missing Animator or Rigidbody2D reference.");
            }

            return;
        }

        float horizontalVelocity = playerRigidbody.linearVelocity.x;

        // A small threshold prevents tiny leftover Rigidbody movement from being treated as running.
        bool isRunning = Mathf.Abs(horizontalVelocity) > runningThreshold;
        bool isGrounded = CheckGrounded();
        bool isDead = playerLifeSystem != null && playerLifeSystem.IsDead();

        // These names must match the Animator parameters exactly.
        catAnimator.SetBool("isRunning", isRunning);
        catAnimator.SetBool("isGrounded", isGrounded);
        catAnimator.SetBool("isDead", isDead);

        if (showDebugLogs)
        {
            Debug.Log(
                "Anim State | X Velocity: " + horizontalVelocity +
                " | isRunning: " + isRunning +
                " | isGrounded: " + isGrounded +
                " | isDead: " + isDead
            );
        }
    }

    private void LateUpdate()
    {
        FlipCatVisual();
    }

    private void FlipCatVisual()
    {
        if (catSpriteRenderer == null || playerRigidbody == null)
            return;

        float horizontalVelocity = playerRigidbody.linearVelocity.x;

        // This is reversed because the downloaded cat sprite faces the opposite direction
        // from the direction expected by the player movement.
        if (horizontalVelocity > runningThreshold)
        {
            catSpriteRenderer.flipX = true;
        }
        else if (horizontalVelocity < -runningThreshold)
        {
            catSpriteRenderer.flipX = false;
        }
    }

    public void ResetFacingDirection()
    {
        if (catSpriteRenderer == null)
            return;

        // Respawn resets the placeholder cat to face the default/front direction
        // so it does not keep facing the direction it died in.
        // For this specific cat asset, Flip X = true is the direction you wanted as "right/front".
        catSpriteRenderer.flipX = true;
    }

    private bool CheckGrounded()
    {
        if (groundCheckPoint == null)
        {
            // Returning true prevents the player from being stuck in jump animation
            // if the GroundCheck reference is missing during setup.
            return true;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            groundCheckPoint.position,
            groundCheckRadius
        );

        foreach (Collider2D hit in hits)
        {
            // The ground check may overlap the player's own collider, so we ignore the Player
            // and any child objects under the Player.
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            // Any non-player 2D collider touching the GroundCheck counts as ground for now.
            // This is simple and reliable for your current greybox/platform setup.
            return true;
        }

        return false;
    }

    public void PlayHurtAnimation()
    {
        if (catAnimator != null)
        {
            // Resetting the trigger first makes repeated damage hits more reliable,
            // especially if the player takes damage again soon after the previous hurt animation.
            catAnimator.ResetTrigger("hurt");
            catAnimator.SetTrigger("hurt");

            if (showDebugLogs)
            {
                Debug.Log("Hurt animation triggered");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null)
            return;

        // This shows the exact area used for ground detection in the Scene view.
        // It makes it easier to tune the GroundCheck position and radius.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}