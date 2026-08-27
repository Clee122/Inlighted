using UnityEngine;
using UnityEngine.InputSystem;

public class MirrorRotate : MonoBehaviour
{
    private InputAction playerInteract;

    [Header("Puzzle")]
    // The shared puzzle controller tells this mirror when the puzzle has been
    // completed so its successful orientation remains locked permanently.
    [SerializeField] private LightPuzzleController puzzleController;

    [Header("Interaction")]
    // The Player reference provides access to the existing Interact1 action and
    // is also used to make sure only the player's collider can rotate the mirror.
    public GameObject Player;

    // The mirror only needs to know whether the player is close enough to use it.
    // A circular interaction area works from every direction and avoids the
    // inconsistent behaviour caused by the previous directional CircleCast.
    [SerializeField] private float interactionRadius = 1f;

    [Header("Rotation")]
    // Each successful interaction gives the mirror one temporary rotation.
    // It cannot be rotated again until the reset has completely finished.
    public float angleIncrement = 90f;

    // Existing mirrors can still begin at different orientations without
    // requiring separate scripts or prefabs.
    public float initialAngleChange = 0f;

    [Header("Timed Reset")]
    // An unsolved mirror keeps its changed orientation for this duration before
    // automatically returning to the position it had when the scene began.
    [SerializeField] private float resetDelay = 8f;

    // A smooth return communicates that the temporary puzzle state has expired
    // instead of making the mirror appear to suddenly snap or glitch.
    [SerializeField] private float resetRotationSpeed = 180f;

    private Quaternion originalRotation;

    private float resetTimer;
    private bool hasBeenRotated;
    private bool isResetting;

    private void Awake()
    {
        // Preserve Jayden's existing initial-angle behaviour before recording
        // the orientation this individual mirror should eventually reset to.
        transform.Rotate(
            0f,
            0f,
            initialAngleChange
        );

        originalRotation = transform.rotation;

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

        resetDelay =
            Mathf.Max(
                0f,
                resetDelay
            );

        resetRotationSpeed =
            Mathf.Max(
                0f,
                resetRotationSpeed
            );
    }

    private void Update()
    {
        // Once the overall puzzle is solved, the mirror keeps its successful
        // angle and no longer accepts interaction or performs its timed reset.
        if (
            puzzleController != null &&
            puzzleController.IsSolved()
        )
        {
            return;
        }

        HandleInteraction();
        HandleTimedReset();
    }

    private void HandleInteraction()
    {
        if (
            Player == null ||
            playerInteract == null ||
            hasBeenRotated ||
            isResetting
        )
        {
            // A mirror only receives one temporary rotation per reset cycle,
            // preventing repeated interaction from brute-forcing the solution.
            return;
        }

        // OverlapCircle checks the complete area surrounding the mirror instead
        // of searching in one direction. This makes interaction consistent
        // whether the player approaches from the left, right, above, or below.
        Collider2D[] nearbyColliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                interactionRadius
            );

        bool playerIsNearby = false;

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (
                nearbyCollider != null &&
                nearbyCollider.CompareTag("Player")
            )
            {
                playerIsNearby = true;
                break;
            }
        }

        if (!playerIsNearby)
        {
            return;
        }

        if (!playerInteract.WasPressedThisFrame())
        {
            return;
        }

        transform.Rotate(
            0f,
            0f,
            angleIncrement
        );

        hasBeenRotated = true;
        resetTimer = resetDelay;
    }

    private void HandleTimedReset()
    {
        if (!hasBeenRotated)
        {
            return;
        }

        if (!isResetting)
        {
            resetTimer -=
                Time.deltaTime;

            if (resetTimer > 0f)
            {
                return;
            }

            isResetting = true;
        }

        // RotateTowards gives the expired mirror state a readable animated return
        // instead of instantly replacing the player's temporary configuration.
        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                originalRotation,
                resetRotationSpeed * Time.deltaTime
            );

        if (
            Quaternion.Angle(
                transform.rotation,
                originalRotation
            ) <= 0.01f
        )
        {
            transform.rotation =
                originalRotation;

            // Interaction only becomes available again after the mirror has
            // completely returned to its original orientation.
            hasBeenRotated = false;
            isResetting = false;
            resetTimer = 0f;
        }
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
