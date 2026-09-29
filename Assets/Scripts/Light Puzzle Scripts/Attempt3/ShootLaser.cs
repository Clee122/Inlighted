using UnityEngine;
using UnityEngine.InputSystem;

public class ShootLaser : MonoBehaviour
{
    [Header("Puzzle")]

    // The LaserPointer remains active after activation, including after this
    // specific puzzle is solved, so the completed light path stays visible.
    [SerializeField] private LightPuzzleController puzzleController;

    [Header("Laser")]
    public Material material;

    // The laser LineRenderer is created at runtime, so its sorting order cannot
    // be edited directly in the Inspector. Exposing the value here makes it
    // easy to keep the beam visible over environment artwork.
    [SerializeField] private int laserSortingOrder = 5;

    // The beam is created at runtime, so exposing its width here allows the
    // laser thickness to be adjusted directly for each LaserPointer in the Inspector.
    [SerializeField] private float laserWidth = 0.2f;

    // The laser uses a dedicated emitter Transform instead of this object's centre
    // because the LaserPointer visual can move during its activation animation.
    // Keeping the origin attached to the animated visual ensures the beam continues
    // to emerge from the correct physical point on the LaserPointer.
    [SerializeField] private Transform laserOrigin;

    private LaserBeam beam;

    public AppearingPlatformReceiver APReceiver;
    public MovingPlatformReceiver MPReceiver;

    [Header("Interaction")]

    // The LaserPointer uses the same Player and Interact1 input as the mirrors
    // so the puzzle keeps one consistent interaction button.
    [SerializeField] private GameObject Player;

    // The player only needs to be near the LaserPointer to activate it.
    [SerializeField] private float interactionDistance = 1.5f;

    [Header("Animation")]

    // The Laser Pointer owns its own activation animation, so the environmental
    // puzzle piece controls its Animator directly instead of involving CatMoth's Animator.
    [SerializeField] private Animator laserPointerAnimator;

    // Keeping the trigger name exposed makes the script easier to reuse if the
    // Animator parameter is renamed later without requiring another code change.
    [SerializeField] private string activationTriggerName = "activate";

    [Header("Interaction Visual Feedback")]

    // The same outline component used by mirrors makes all environmental
    // puzzle interactions use one consistent visual language.
    [SerializeField] private PuzzleInteractableOutline interactionOutline;

    [Header("SFX")]
    public AudioSource PlantBeamInteractSFX;

    private InputAction playerInteract;

    private bool isLaserActive;

    private void Awake()
    {
        interactionDistance =
            Mathf.Max(
                0f,
                interactionDistance
            );

        laserWidth =
            Mathf.Max(
                0.01f,
                laserWidth
            );

        if (interactionOutline == null)
        {
            // Automatically finding the outline keeps prefab setup consistent
            // with MirrorRotate while still allowing a manual reference.
            interactionOutline =
                GetComponent<PuzzleInteractableOutline>();
        }

        if (laserPointerAnimator == null)
        {
            // The Animator normally belongs to the Laser Pointer itself or one
            // of its visual children, so this fallback reduces prefab setup mistakes.
            laserPointerAnimator =
                GetComponentInChildren<Animator>();
        }

        if (laserOrigin == null)
        {
            // Falling back to this Transform preserves the previous laser behaviour
            // if the dedicated LaserOrigin has not been assigned yet.
            Debug.LogWarning(
                "ShootLaser does not have a Laser Origin assigned. Falling back to the LaserPointer transform.",
                this
            );
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
        // Once the puzzle is solved, the successful laser path remains active
        // permanently so the completed light connection stays visually clear.
        // Interaction remains disabled because the LaserPointer has already
        // performed its one available activation.
        if (
            puzzleController != null &&
            puzzleController.IsSolved()
        )
        {
            SetInteractionOutline(
                false
            );

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

        // Audio feedback is optional, so a missing AudioSource should never prevent
        // the LaserPointer's gameplay interaction, beam, or animation from activating.
        if (PlantBeamInteractSFX != null)
        {
            PlantBeamInteractSFX.Play();
        }

        ActivateLaser();
    }

    private void ActivateLaser()
    {
        Transform origin =
            laserOrigin != null
                ? laserOrigin
                : transform;

        if (beam == null)
        {
            // The sorting order, width, and origin are supplied because the beam
            // creates its own LineRenderer at runtime rather than using a component
            // that can be configured directly on the LaserPointer.
            beam =
                new LaserBeam(
                    origin.position,
                    origin.right,
                    material,
                    APReceiver,
                    MPReceiver,
                    laserSortingOrder,
                    laserWidth
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

        // The animation is triggered only after activation has successfully reached
        // this point, so a failed laser setup cannot play a misleading visual response.
        PlayActivationAnimation();

        // Activating the source starts a persistent laser so the player can
        // experiment with mirror angles. Once the puzzle is solved, the laser
        // remains active permanently to preserve the completed light path.
        isLaserActive = true;

        // Once the source has been activated there is no further interaction
        // available here, so its proximity outline should disappear immediately.
        SetInteractionOutline(
            false
        );

        beam.laser.enabled = true;

        Debug.Log(
            gameObject.name +
            " activated. The laser will remain on permanently."
        );
    }

    private void PlayActivationAnimation()
    {
        if (laserPointerAnimator == null)
        {
            // The puzzle itself can still function without animation, so a missing
            // Animator should warn rather than prevent the laser from activating.
            Debug.LogWarning(
                "ShootLaser could not play the Laser Pointer activation animation because no Animator was found.",
                this
            );

            return;
        }

        if (string.IsNullOrEmpty(activationTriggerName))
        {
            Debug.LogWarning(
                "ShootLaser activation trigger name is empty.",
                this
            );

            return;
        }

        // The Laser Pointer activation is a one-shot reaction to the player's C
        // interaction, so a Trigger is appropriate instead of a persistent Bool.
        laserPointerAnimator.ResetTrigger(
            activationTriggerName
        );

        laserPointerAnimator.SetTrigger(
            activationTriggerName
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

        // Recasting every frame gives immediate feedback when mirrors rotate and
        // keeps the successful beam path visible permanently after puzzle completion.
        beam.laser.positionCount = 0;
        beam.laserIndices.Clear();

        // The origin is read every frame because the activation animation moves
        // the LaserOrigin as the pointer opens. Reading its current transform keeps
        // the beam attached to the emitter throughout the entire animation.
        Transform origin =
            laserOrigin != null
                ? laserOrigin
                : transform;

        beam.CastRay(
            origin.position,
            origin.right,
            beam.laser
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