using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerIntroDrop : MonoBehaviour
{
    [Header("Intro Positions")]

    // CatMoth begins at this position rather than at the normal ground-level
    // starting location so the first visible action is the character dropping in.
    [SerializeField] private Transform dropStartPoint;

    // The camera remains focused on this point throughout the drop so CatMoth
    // visibly enters the intended starting composition instead of being followed down.
    [SerializeField] private Transform introCameraPoint;

    [Header("Intro Camera")]

    // Using the existing camera manager lets the intro temporarily take control
    // of the same Cinemachine follow point without creating a second virtual camera.
    [SerializeField] private cameramanage cameraManager;

    [Header("Landing")]

    // Only collisions with these layers are considered a valid intro landing.
    // This prevents triggers and unrelated gameplay objects from ending the intro.
    [SerializeField] private LayerMask landingLayers;

    [Header("Drop Behaviour")]

    // The intro uses a fraction of CatMoth's normal Rigidbody gravity so the
    // entrance is slightly slower and easier to read than a normal gameplay fall.
    [SerializeField]
    [Range(0.1f, 1f)]
    private float introGravityMultiplier = 0.65f;

    // Limiting the intro's downward velocity prevents CatMoth from accelerating
    // to the normal gameplay fall speed during the short opening entrance.
    [SerializeField] private float introMaximumFallSpeed = 6f;

    // A tiny pause after landing makes CatMoth's arrival readable before the
    // camera begins following and control is returned to the player.
    [SerializeField] private float postLandingDelay = 0.05f;

    private Rigidbody2D rb;
    private PlayerController2D playerController;
    private PlayerInput playerInput;

    private float normalGravityScale;

    private bool introIsActive;
    private bool hasLanded;

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody2D>();

        playerController =
            GetComponent<PlayerController2D>();

        playerInput =
            GetComponent<PlayerInput>();

        if (rb == null)
        {
            Debug.LogError(
                "PlayerIntroDrop requires a Rigidbody2D on the Player.",
                this
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                "PlayerIntroDrop requires PlayerController2D on the Player.",
                this
            );
        }

        if (playerInput == null)
        {
            Debug.LogError(
                "PlayerIntroDrop requires PlayerInput on the Player.",
                this
            );
        }

        if (cameraManager == null)
        {
            cameraManager =
                FindFirstObjectByType<cameramanage>();
        }

        if (rb != null)
        {
            // Remember the player's normal gravity so the intro can temporarily
            // modify the fall without permanently changing gameplay physics.
            normalGravityScale =
                rb.gravityScale;
        }

        introGravityMultiplier =
            Mathf.Clamp(
                introGravityMultiplier,
                0.1f,
                1f
            );

        introMaximumFallSpeed =
            Mathf.Max(
                0.1f,
                introMaximumFallSpeed
            );

        postLandingDelay =
            Mathf.Max(
                0f,
                postLandingDelay
            );
    }

    private void Start()
    {
        if (
            rb == null ||
            playerController == null ||
            playerInput == null ||
            dropStartPoint == null
        )
        {
            Debug.LogError(
                "PlayerIntroDrop is missing a required reference and cannot play.",
                this
            );

            return;
        }

        StartCoroutine(
            PlayIntroDrop()
        );
    }

    private IEnumerator PlayIntroDrop()
    {
        introIsActive =
            true;

        hasLanded =
            false;

        // Normal movement is disabled during the opening because PlayerController2D
        // has its own gravity and fall-speed behaviour that would otherwise fight
        // against the deliberately slower introductory drop.
        playerController.enabled =
            false;

        // PlayerInput is disabled so movement, jumping and abilities cannot be used
        // until CatMoth has genuinely reached the starting floor.
        playerInput.enabled =
            false;

        rb.linearVelocity =
            Vector2.zero;

        rb.angularVelocity =
            0f;

        transform.position =
            dropStartPoint.position;

        rb.gravityScale =
            normalGravityScale *
            introGravityMultiplier;

        if (
            cameraManager != null &&
            introCameraPoint != null
        )
        {
            // The camera is fixed before CatMoth begins falling so the character
            // visibly drops into an already-established opening composition.
            cameraManager.HoldIntroCamera(
                introCameraPoint
            );
        }

        // Landing is now the only condition that can end the introductory fall.
        // The camera therefore cannot resume following CatMoth while the character
        // is still airborne, regardless of how long the fall happens to take.
        while (!hasLanded)
        {
            yield return null;
        }

        // A tiny pause after contact gives the landing a moment to register before
        // normal camera tracking and player control begin at the same time.
        if (postLandingDelay > 0f)
        {
            yield return new WaitForSeconds(
                postLandingDelay
            );
        }

        FinishIntro();
    }

    private void FixedUpdate()
    {
        if (
            !introIsActive ||
            rb == null
        )
        {
            return;
        }

        // Horizontal movement is removed during the entrance so CatMoth falls
        // directly towards the intended starting location rather than drifting.
        float verticalVelocity =
            rb.linearVelocity.y;

        if (
            verticalVelocity <
            -introMaximumFallSpeed
        )
        {
            verticalVelocity =
                -introMaximumFallSpeed;
        }

        rb.linearVelocity =
            new Vector2(
                0f,
                verticalVelocity
            );
    }

    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        if (
            !introIsActive ||
            collision == null ||
            collision.gameObject == null
        )
        {
            return;
        }

        int collisionLayerMask =
            1 << collision.gameObject.layer;

        bool landedOnValidLayer =
            (
                landingLayers.value &
                collisionLayerMask
            ) != 0;

        if (!landedOnValidLayer)
        {
            return;
        }

        // A valid ground collision while CatMoth is descending confirms that the
        // opening fall has genuinely finished and normal gameplay may now begin.
        if (
            rb != null &&
            rb.linearVelocity.y <= 0.1f
        )
        {
            hasLanded =
                true;

            Debug.Log(
                "CatMoth landed during the opening intro."
            );
        }
    }

    private void FinishIntro()
    {
        introIsActive =
            false;

        if (rb != null)
        {
            // Restore normal gameplay physics only after landing so the slower
            // introductory gravity cannot carry into ordinary movement.
            rb.gravityScale =
                normalGravityScale;

            rb.linearVelocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;
        }

        if (playerController != null)
        {
            playerController.enabled =
                true;

            // Clearing movement state ensures no input stored before or during
            // the entrance unexpectedly executes when control is returned.
            playerController.ResetMovementInput();
        }

        if (playerInput != null)
        {
            playerInput.enabled =
                true;
        }

        if (cameraManager != null)
        {
            // Camera follow is deliberately restored here rather than on a timer.
            // Reaching this point means CatMoth has already touched the ground.
            cameraManager.ResumePlayerFollowAfterIntro();
        }

        Debug.Log(
            "Player intro drop completed. Camera follow and player control restored."
        );
    }
}