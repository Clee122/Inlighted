using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShootLaser : MonoBehaviour
{
    [Header("Puzzle")]

    // The LaserPointer remains active while the player solves this specific
    // puzzle, then shuts down after a configurable delay once success is registered.
    [SerializeField] private LightPuzzleController puzzleController;

    [Header("Laser")]
    public Material material;

    // The laser LineRenderer is created at runtime, so its sorting order cannot
    // be edited directly in the Inspector. Exposing the value here makes it
    // easy to keep the beam visible over environment artwork.
    [SerializeField] private int laserSortingOrder = 5;

    private LaserBeam beam;

    public AppearingPlatformReceiver APReceiver;
    public MovingPlatformReceiver MPReceiver;

    [Header("Interaction")]

    // The LaserPointer uses the same Player and Interact1 input as the mirrors
    // so the puzzle keeps one consistent interaction button.
    [SerializeField] private GameObject Player;

    // The player only needs to be near the LaserPointer to activate it.
    [SerializeField] private float interactionDistance = 1.5f;

    [Header("Interaction Visual Feedback")]

    // The same outline component used by mirrors makes all environmental
    // puzzle interactions use one consistent visual language.
    [SerializeField] private PuzzleInteractableOutline interactionOutline;

    [Header("Solved Laser Shutoff")]

    // The laser remains visible briefly after the puzzle is solved so the player
    // can clearly see the successful beam path and the resulting platform movement.
    // Different puzzle layouts can use different delays in the Inspector.
    [SerializeField] private float solvedShutoffDelay = 3f;

    private InputAction playerInteract;

    private bool isLaserActive;
    private bool isWaitingToShutOff;

    private Coroutine solvedShutoffCoroutine;

    private void Awake()
    {
        interactionDistance =
            Mathf.Max(
                0f,
                interactionDistance
            );

        solvedShutoffDelay =
            Mathf.Max(
                0f,
                solvedShutoffDelay
            );

        if (interactionOutline == null)
        {
            // Automatically finding the outline keeps prefab setup consistent
            // with MirrorRotate while still allowing a manual reference.
            interactionOutline =
                GetComponent<PuzzleInteractableOutline>();
        }

        if (Player == null)
        {
            Debug.LogError(
                "ShootLaser requires the Player reference to be assigned.",
                this
            );

            return;
        }

        PlayerInput playerInput =
            Player.GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError(
                "ShootLaser could not find PlayerInput on the assigned Player.",
                this
            );

            return;
        }

        playerInteract =
            playerInput.actions["Interact1"];

        if (playerInteract == null)
        {
            Debug.LogError(
                "ShootLaser could not find the Interact1 input action.",
                this
            );
        }
    }

    private void Start()
    {
        // The puzzle begins with the environmental laser switched off and its
        // controlled receiver objects in their unsolved resting states.
        if (APReceiver != null)
        {
            APReceiver.DeActivate();
        }

        if (MPReceiver != null)
        {
            MPReceiver.DeActivate();
        }

        SetInteractionOutline(
            false
        );
    }

    private void Update()
    {
        // Once solved, the laser keeps recasting during its shutdown delay so
        // the successful light path remains visible while the platform moves.
        if (
            puzzleController != null &&
            puzzleController.IsSolved()
        )
        {
            SetInteractionOutline(
                false
            );

            if (
                isLaserActive &&
                !isWaitingToShutOff
            )
            {
                StartSolvedShutoff();
            }

            if (isLaserActive)
            {
                UpdateActiveLaser();
            }

            return;
        }

        /*
         * The LaserPointer is only interactable before it has been activated.
         * Once its persistent laser is running, removing the outline prevents
         * the player from expecting that pressing C will perform another action.
         */
        if (!isLaserActive)
        {
            bool playerIsNearby =
                IsPlayerWithinInteractionRange();

            SetInteractionOutline(
                playerIsNearby
            );

            HandleInteraction(
                playerIsNearby
            );

            return;
        }

        SetInteractionOutline(
            false
        );

        UpdateActiveLaser();
    }

    private bool IsPlayerWithinInteractionRange()
    {
        if (Player == null)
        {
            return false;
        }

        float distanceToPlayer =
            Vector2.Distance(
                transform.position,
                Player.transform.position
            );

        return
            distanceToPlayer <=
            interactionDistance;
    }

    private void HandleInteraction(
        bool playerIsNearby
    )
    {
        if (
            Player == null ||
            playerInteract == null ||
            isLaserActive
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

        ActivateLaser();
    }

    private void ActivateLaser()
    {
        if (beam == null)
        {
            // The sorting order is passed into LaserBeam because the beam creates
            // its own LineRenderer at runtime rather than using this GameObject.
            beam =
                new LaserBeam(
                    transform.position,
                    transform.right,
                    material,
                    APReceiver,
                    MPReceiver,
                    laserSortingOrder
                );
        }

        if (
            beam == null ||
            beam.laser == null
        )
        {
            Debug.LogError(
                "ShootLaser could not create its LaserBeam.",
                this
            );

            return;
        }

        // Activating the source starts a persistent laser so the player can
        // experiment with mirror angles without needing to race against a timer.
        isLaserActive = true;
        isWaitingToShutOff = false;

        // Once the source has been activated there is no further interaction
        // available here, so its proximity outline should disappear immediately.
        SetInteractionOutline(
            false
        );

        beam.laser.enabled = true;

        Debug.Log(
            gameObject.name +
            " activated. The laser will remain on until the puzzle is solved."
        );
    }

    private void UpdateActiveLaser()
    {
        if (
            beam == null ||
            beam.laser == null
        )
        {
            isLaserActive = false;
            return;
        }

        // Recasting every frame gives immediate feedback when mirrors rotate,
        // and also keeps the completed beam path visible during the solved delay.
        beam.laser.positionCount = 0;
        beam.laserIndices.Clear();

        beam.CastRay(
            transform.position,
            transform.right,
            beam.laser
        );
    }

    private void StartSolvedShutoff()
    {
        isWaitingToShutOff = true;

        if (solvedShutoffCoroutine != null)
        {
            StopCoroutine(
                solvedShutoffCoroutine
            );
        }

        solvedShutoffCoroutine =
            StartCoroutine(
                SolvedShutoffRoutine()
            );

        Debug.Log(
            gameObject.name +
            " puzzle solved. Laser will switch off after " +
            solvedShutoffDelay.ToString("0.00") +
            " seconds."
        );
    }

    private IEnumerator SolvedShutoffRoutine()
    {
        // Keeping the laser active during this delay lets the player visually
        // connect the successful receiver hit with the moving platform response.
        if (solvedShutoffDelay > 0f)
        {
            yield return new WaitForSeconds(
                solvedShutoffDelay
            );
        }

        DeactivateLaserAfterPuzzleSolved();

        solvedShutoffCoroutine = null;
    }

    private void DeactivateLaserAfterPuzzleSolved()
    {
        isLaserActive = false;
        isWaitingToShutOff = false;

        if (
            beam != null &&
            beam.laser != null
        )
        {
            beam.laser.positionCount = 0;
            beam.laser.enabled = false;
        }

        // Do not deactivate the receiver here because the completed puzzle
        // must keep its door/platform in the solved state after the beam disappears.

        Debug.Log(
            gameObject.name +
            " switched off after the solved puzzle delay."
        );
    }

    public bool IsLaserActive()
    {
        // Other puzzle systems can query whether the environmental laser is
        // currently active without needing access to its runtime LineRenderer.
        return isLaserActive;
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
        // Stop any delayed shutdown if this puzzle object itself is disabled,
        // preventing a coroutine from continuing against an inactive object.
        if (solvedShutoffCoroutine != null)
        {
            StopCoroutine(
                solvedShutoffCoroutine
            );

            solvedShutoffCoroutine = null;
        }

        // Removing the proximity feedback ensures an inactive puzzle object
        // cannot leave its generated outline visible in the level.
        SetInteractionOutline(
            false
        );
    }

    private void OnDrawGizmosSelected()
    {
        // Showing the LaserPointer's interaction range in the Scene view makes
        // its proximity behaviour as easy to tune as the mirrors.
        Gizmos.DrawWireSphere(
            transform.position,
            interactionDistance
        );
    }
}