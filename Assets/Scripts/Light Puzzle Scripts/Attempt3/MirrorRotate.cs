using UnityEngine;
using UnityEngine.InputSystem;

public class MirrorRotate : MonoBehaviour
{
    private InputAction playerInteract;

    [Header("Puzzle")]
    // The shared controller tells this mirror when the overall puzzle has been
    // solved so its successful orientation can become permanent.
    [SerializeField] private LightPuzzleController puzzleController;

    [Header("Interaction")]
    // The Player reference is used to check whether the player is close enough
    // to interact and to access the existing Interact1 input action.
    public GameObject Player;

    // These values keep Jayden's original proximity interaction setup so the
    // mirror can be tested without changing how the player approaches it.
    public float radius = 0.8f;
    public Vector2 direction = Vector2.right;
    public float distance = 0.85f;

    [Header("Rotation")]
    // Each successful interaction rotates the mirror once by this amount.
    // The mirror then locks until it has fully reset to prevent brute-forcing.
    public float angleIncrement = 90f;

    // Different mirrors can still begin at different orientations using the
    // existing Inspector value from Jayden's original puzzle setup.
    public float initialAngleChange = 0f;

    [Header("Timed Reset")]
    // After being rotated, the mirror holds its temporary orientation for this
    // amount of time before returning to its original puzzle position.
    [SerializeField] private float resetDelay = 8f;

    // Returning smoothly makes it clear that the temporary state has expired
    // rather than making the mirror appear to snap or glitch back into place.
    [SerializeField] private float resetRotationSpeed = 180f;

    private Quaternion originalRotation;

    private float resetTimer;
    private bool hasBeenRotated;
    private bool isResetting;

    private void Awake()
    {
        // Apply Jayden's existing starting-angle adjustment before recording
        // the orientation that this mirror should eventually return to.
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

        // Reuse the project's existing interaction input so mirrors and the
        // LaserPointer both follow the same player control language.
        playerInteract =
            playerInput.actions["Interact1"];

        if (playerInteract == null)
        {
            Debug.LogError(
                "MirrorRotate could not find the Interact1 input action.",
                this
            );
        }

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
        // Once the puzzle succeeds, the mirror deliberately stops all reset
        // and interaction behaviour so the successful orientation is preserved.
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
            // Once the mirror has been rotated, it stays locked until the timed
            // reset completely finishes. This prevents players from repeatedly
            // rotating the same mirror and brute-forcing the puzzle solution.
            return;
        }

        RaycastHit2D hit =
            Physics2D.CircleCast(
                transform.position,
                radius,
                direction,
                distance
            );

        if (
            hit.collider == null ||
            !hit.collider.CompareTag("Player")
        )
        {
            return;
        }

        if (!playerInteract.WasPressedThisFrame())
        {
            return;
        }

        // The mirror only receives one temporary rotation per reset cycle.
        // Additional interaction attempts are ignored until it returns home.
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

        // RotateTowards lets the mirror visibly return to its original angle
        // instead of instantly snapping back when the timer expires.
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

            // The mirror only becomes interactable again after it has completely
            // returned to its original orientation.
            hasBeenRotated = false;
            isResetting = false;
            resetTimer = 0f;
        }
    }
}
