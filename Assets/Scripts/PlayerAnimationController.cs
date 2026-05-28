using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator catMothAnimator;
    [SerializeField] private SpriteRenderer catMothSpriteRenderer;

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
        // This fallback keeps the script working if the reference is lost after replacing the character visual.
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        // The Animator is expected to be on the CatMoth Visual child object, not on the parent Player.
        // Finding it automatically helps after replacing the old cat placeholder with the new CatMoth asset.
        if (catMothAnimator == null)
        {
            catMothAnimator = GetComponentInChildren<Animator>();
        }

        // The SpriteRenderer is also expected to be on the CatMoth Visual child object.
        // This lets the script flip only the visual sprite without changing the parent Player transform or collider.
        if (catMothSpriteRenderer == null)
        {
            catMothSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        if (catMothAnimator == null || playerRigidbody == null)
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
        catMothAnimator.SetBool("isRunning", isRunning);
        catMothAnimator.SetBool("isGrounded", isGrounded);
        catMothAnimator.SetBool("isDead", isDead);

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
        FlipCatMothVisual();
    }

    private void FlipCatMothVisual()
    {
        if (catMothSpriteRenderer == null || playerRigidbody == null)
            return;

        float horizontalVelocity = playerRigidbody.linearVelocity.x;

        // The CatMoth asset faces right by default, so moving right should not flip the sprite.
        // This reverses the old placeholder cat logic because that asset originally faced the opposite way.
        if (horizontalVelocity > runningThreshold)
        {
            catMothSpriteRenderer.flipX = false;
        }
        else if (horizontalVelocity < -runningThreshold)
        {
            catMothSpriteRenderer.flipX = true;
        }
    }

    public void ResetFacingDirection()
    {
        if (catMothSpriteRenderer == null)
            return;

        // Respawn resets the CatMoth to face the default/right direction
        // so it does not keep facing the direction it died in.
        // For this CatMoth asset, Flip X = false is the right/front direction.
        catMothSpriteRenderer.flipX = false;
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
            // This is simple and reliable for the current greybox/platform setup.
            return true;
        }

        return false;
    }

    public void PlayHurtAnimation()
    {
        if (catMothAnimator != null)
        {
            // Resetting the trigger first makes repeated damage hits more reliable,
            // especially if the player takes damage again soon after the previous hurt animation.
            catMothAnimator.ResetTrigger("hurt");
            catMothAnimator.SetTrigger("hurt");

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