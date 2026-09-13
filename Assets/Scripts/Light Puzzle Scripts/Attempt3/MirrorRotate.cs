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

    // A short lockout after each completed rotation prevents accidental rapid
    // double inputs while still allowing the mirror to be rotated repeatedly.
    [SerializeField] private float interactionCooldown = 1.5f;

    [Header("Rotation")]

    // Every interaction moves the mirror towards one predictable angle step.
    // The angle remains persistent because the mirror no longer automatically
    // resets after a timer expires.
    public float angleIncrement = 90f;

    // Controls how quickly the mirror physically turns towards its next angle.
    // 180 degrees per second makes a 90-degree turn take roughly half a second,
    // which keeps the movement visible without slowing the puzzle down too much.
    [SerializeField] private float rotationSpeed = 180f;

    // Different mirrors can begin at different orientations while continuing
    // to use the same reusable interaction script.
    public float initialAngleChange = 0f;

    private float interactionCooldownTimer;

    // The mirror rotates towards this orientation instead of snapping immediately.
    // Storing a target also ensures repeated rotations remain exact 90-degree steps.
    private Quaternion targetRotation;

    // While a rotation is in progress, another interaction is blocked so the
    // player cannot queue several turns before seeing the result of the first one.
    private bool isRotating;

    private void Awake()
    {
        // Apply the level designer's starting orientation before the player
        // begins interacting with this particular mirror.
        transform.Rotate(
            0f,
            0f,
            initialAngleChange
        );

        // Begin with the target matching the mirror's actual starting rotation.
        // This prevents the mirror from trying to move when the scene first loads.
        targetRotation =
            transform.localRotation;

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

        // Preventing a zero or negative speed avoids creating a mirror that
        // receives an interaction but can never reach its requested rotation.
        rotationSpeed =
            Mathf.Max(
                0.01f,
                rotationSpeed
            );
    }

    private void Update()
    {
        // Continue an already-started rotation before processing new input.
        // This keeps the visual movement independent from interaction checks.
        UpdateSmoothRotation();

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

        // The cooldown starts after the mirror reaches its requested angle.
        // This keeps the short lockout meaningful even though rotation now takes
        // time instead of happening instantly.
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

    private void UpdateSmoothRotation()
    {
        if (!isRotating)
        {
            return;
        }

        // RotateTowards gives the mirror a consistent visible turning speed
        // without overshooting the exact target orientation.
        transform.localRotation =
            Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

        // A very small tolerance avoids relying on perfect floating-point equality
        // when deciding whether the requested movement has finished.
        if (
            Quaternion.Angle(
                transform.localRotation,
                targetRotation
            ) <= 0.01f
        )
        {
            // Snap the final tiny fraction to the intended orientation so repeated
            // rotations never accumulate small rotational inaccuracies.
            transform.localRotation =
                targetRotation;

            isRotating =
                false;

            // The cooldown begins once the player has actually seen the complete
            // rotation rather than being consumed while the mirror is still moving.
            interactionCooldownTimer =
                interactionCooldown;

            Debug.Log(
                gameObject.name +
                " finished rotating."
            );
        }
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
            interactionCooldownTimer > 0f ||
            isRotating
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

        // Each separate button press advances the target orientation by one
        // angle step. The mirror then visibly travels towards that orientation
        // rather than instantly snapping to it.
        targetRotation =
            targetRotation *
            Quaternion.Euler(
                0f,
                0f,
                angleIncrement
            );

        isRotating =
            true;

        Debug.Log(
            gameObject.name +
            " started rotating by " +
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