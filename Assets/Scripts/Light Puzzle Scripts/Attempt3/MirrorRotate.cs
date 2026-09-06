using UnityEngine;
using UnityEngine.InputSystem;

public class MirrorRotate : MonoBehaviour
{
    private InputAction playerInteract;

    [Header("Puzzle")]

    // Once the shared puzzle is solved, this mirror keeps its successful
    // orientation and stops accepting further interactions.
    [SerializeField] private LightPuzzleController puzzleController;

    [Header("Interaction")]

    // The Player reference provides access to the existing Interact1 action and
    // is also used to ensure the player is close enough to rotate the mirror.
    public GameObject Player;

    // A circular interaction area allows the player to use the mirror
    // consistently regardless of which direction they approach it from.
    [SerializeField] private float interactionRadius = 1f;

    [Header("Interaction Visual Feedback")]

    // The shared outline component communicates that the mirror can currently
    // be interacted with without changing the mirror's actual sprite colour.
    [SerializeField] private PuzzleInteractableOutline interactionOutline;

    [Header("Interaction Timing")]

    // A short lockout after each successful rotation prevents accidental rapid
    // double inputs without limiting how many times the player can use the mirror.
    [SerializeField] private float interactionCooldown = 1.5f;

    [Header("Rotation")]

    // Every interaction rotates the mirror by one predictable step. There is no
    // timer or automatic reset, allowing players to experiment with the laser
    // path for as long as necessary.
    public float angleIncrement = 90f;

    // Different mirrors can begin at different orientations while continuing
    // to use the same reusable interaction script.
    public float initialAngleChange = 0f;

    private float interactionCooldownTimer;

    private void Awake()
    {
        // Apply the level designer's starting orientation before the player
        // begins interacting with this particular mirror.
        transform.Rotate(
            0f,
            0f,
            initialAngleChange
        );

        if (interactionOutline == null)
        {
            // Automatically finding the outline on this puzzle piece reduces
            // Inspector setup while still allowing a manual reference.
            interactionOutline =
                GetComponent<PuzzleInteractableOutline>();
        }

        if (Player == null)
        {
            Debug.LogError(
                "MirrorRotate requires the Player reference to be assigned.",
                this
            );

            return;
        }

        PlayerInput playerInput =
            Player.GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError(
                "MirrorRotate could not find PlayerInput on the assigned Player.",
                this
            );

            return;
        }

        // Mirrors continue using the same Interact1 action as the LaserPointer
        // so all environmental puzzle objects share one interaction button.
        playerInteract =
            playerInput.actions["Interact1"];

        if (playerInteract == null)
        {
            Debug.LogError(
                "MirrorRotate could not find the Interact1 input action.",
                this
            );
        }

        interactionRadius =
            Mathf.Max(
                0f,
                interactionRadius
            );

        interactionCooldown =
            Mathf.Max(
                0f,
                interactionCooldown
            );
    }

    private void Update()
    {
        // Solving the overall puzzle freezes the successful mirror configuration.
        // It also removes interaction feedback because this mirror can no longer
        // be manipulated after the puzzle has been completed.
        if (
            puzzleController != null &&
            puzzleController.IsSolved()
        )
        {
            SetInteractionOutline(
                false
            );

            return;
        }

        bool playerIsNearby =
            IsPlayerWithinInteractionRange();

        /*
         * The outline represents interaction range rather than cooldown state.
         * Keeping it visible during the short input lockout prevents the mirror
         * from visually flickering after every successful rotation.
         */
        SetInteractionOutline(
            playerIsNearby
        );

        // The cooldown only blocks another rotation briefly after a successful
        // interaction instead of restricting how many times the mirror can rotate.
        if (interactionCooldownTimer > 0f)
        {
            interactionCooldownTimer -=
                Time.deltaTime;

            if (interactionCooldownTimer < 0f)
            {
                interactionCooldownTimer = 0f;
            }
        }

        HandleInteraction(
            playerIsNearby
        );
    }

    private bool IsPlayerWithinInteractionRange()
    {
        if (Player == null)
        {
            return false;
        }

        // OverlapCircle checks the complete area surrounding the mirror instead
        // of using a directional cast, keeping interaction consistent from all sides.
        Collider2D[] nearbyColliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                interactionRadius
            );

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (
                nearbyCollider != null &&
                nearbyCollider.CompareTag("Player")
            )
            {
                return true;
            }
        }

        return false;
    }

    private void HandleInteraction(
        bool playerIsNearby
    )
    {
        if (
            Player == null ||
            playerInteract == null ||
            interactionCooldownTimer > 0f
        )
        {
            return;
        }

        if (!playerIsNearby)
        {
            return;
        }

        if (!playerInteract.WasPressedThisFrame())
        {
            return;
        }

        // Each separate button press advances the mirror by one angle step.
        // Because there is no reset timer, the chosen orientation persists
        // while the player moves between other mirrors in the puzzle.
        transform.Rotate(
            0f,
            0f,
            angleIncrement
        );

        // Starting the lockout only after a valid rotation prevents accidental
        // rapid activations while keeping repeated deliberate interaction responsive.
        interactionCooldownTimer =
            interactionCooldown;

        Debug.Log(
            gameObject.name +
            " rotated by " +
            angleIncrement.ToString("0.0") +
            " degrees."
        );
    }

    private void SetInteractionOutline(
        bool shouldShow
    )
    {
        if (interactionOutline == null)
        {
            return;
        }

        interactionOutline.SetVisible(
            shouldShow
        );
    }

    private void OnDisable()
    {
        // Explicitly removing the feedback prevents an outline remaining visible
        // if this puzzle object is disabled while the player is standing nearby.
        SetInteractionOutline(
            false
        );
    }

    private void OnDrawGizmosSelected()
    {
        // Drawing the interaction radius in the Scene view makes it easy to
        // position mirrors without guessing whether the player can reach them.
        Gizmos.DrawWireSphere(
            transform.position,
            interactionRadius
        );
    }
}