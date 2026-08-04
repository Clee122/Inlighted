using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator catMothAnimator;
    [SerializeField] private SpriteRenderer catMothSpriteRenderer;

    [Header("Death Visual Priority")]
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

        if (catMothSpriteRenderer != null)
        {
            // The original sorting values are saved so death can briefly appear above darkness,
            // then safely return to normal gameplay sorting after respawn.
            originalOrderInLayer = catMothSpriteRenderer.sortingOrder;
            originalSortingLayerID = catMothSpriteRenderer.sortingLayerID;
        }

        Debug.Log("ANIM CHECK 0: PlayerAnimationController Awake completed.");
    }

    private void Update()
    {
        if (catMothAnimator == null || playerRigidbody == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("ANIM CHECK FAILED: Missing Animator or Rigidbody2D reference.");
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
                "ANIM CHECK: Animator target = " + catMothAnimator.gameObject.name +
                " | X Velocity = " + horizontalVelocity +
                " | isRunning = " + isRunning +
                " | isGrounded = " + isGrounded +
                " | isDead sent = " + isDead +
                " | Animator isDead value = " + catMothAnimator.GetBool("isDead")
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
        {
            Debug.LogWarning("ANIM CHECK FAILED: Cannot reset facing because CatMoth SpriteRenderer is missing.");
            return;
        }

        // Respawn resets the CatMoth to face the default/right direction
        // so it does not keep facing the direction it died in.
        catMothSpriteRenderer.flipX = false;

        Debug.Log("ANIM CHECK: CatMoth facing direction reset to default/right.");
    }

    public void SetDeathVisualPriority()
    {
        if (catMothSpriteRenderer == null)
        {
            Debug.LogWarning("ANIM CHECK FAILED: Cannot set death visual priority because CatMoth SpriteRenderer is missing.");
            return;
        }

        // Death should briefly appear above darkness so the player can actually see
        // the death animation before the respawn UI and teleport happen.
        catMothSpriteRenderer.sortingOrder = deathOrderInLayer;

        Debug.Log("ANIM CHECK: Death visual priority set. Order in Layer = " + deathOrderInLayer);
    }

    public void ResetVisualPriority()
    {
        if (catMothSpriteRenderer == null)
        {
            Debug.LogWarning("ANIM CHECK FAILED: Cannot reset visual priority because CatMoth SpriteRenderer is missing.");
            return;
        }

        // Respawn restores the CatMoth's normal sorting so it does not permanently
        // stay above darkness, platforms, or other level visuals after death.
        catMothSpriteRenderer.sortingLayerID = originalSortingLayerID;
        catMothSpriteRenderer.sortingOrder = originalOrderInLayer;

        Debug.Log("ANIM CHECK: CatMoth visual priority reset to original order: " + originalOrderInLayer);
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

            Debug.Log("ANIM CHECK: Hurt animation trigger sent.");
        }
        else
        {
            Debug.LogWarning("ANIM CHECK FAILED: Cannot play hurt animation because CatMoth Animator is missing.");
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