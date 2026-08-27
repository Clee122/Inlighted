using UnityEngine;
using UnityEngine.InputSystem;

public class ShootLaser : MonoBehaviour
{
    [Header("Puzzle")]
    // The LaserPointer stops accepting new activations after this specific
    // puzzle has been permanently solved.
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

    [Header("Timed Laser")]
    // The beam remains active long enough for the player to observe the route
    // without allowing the environmental laser to stay permanently active.
    [SerializeField] private float activeDuration = 4f;

    // A short cooldown prevents repeatedly restarting the laser immediately.
    [SerializeField] private float cooldownDuration = 1f;

    private InputAction playerInteract;

    private bool isLaserActive;
    private float activeTimer;
    private float cooldownTimer;

    private void Awake()
    {
        activeDuration =
            Mathf.Max(
                0.1f,
                activeDuration
            );

        cooldownDuration =
            Mathf.Max(
                0f,
                cooldownDuration
            );

        interactionDistance =
            Mathf.Max(
                0f,
                interactionDistance
            );

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
        // The puzzle begins with the laser switched off and its receivers
        // returned to their resting state.
        if (APReceiver != null)
        {
            APReceiver.DeActivate();
        }

        if (MPReceiver != null)
        {
            MPReceiver.DeActivate();
        }
    }

    private void Update()
    {
        UpdateCooldown();

        // Once solved, this LaserPointer no longer needs to accept interaction.
        if (
            puzzleController == null ||
            !puzzleController.IsSolved()
        )
        {
            HandleInteraction();
        }

        if (!isLaserActive)
        {
            return;
        }

        UpdateActiveLaser();
    }

    private void HandleInteraction()
    {
        if (
            Player == null ||
            playerInteract == null ||
            isLaserActive ||
            cooldownTimer > 0f
        )
        {
            return;
        }

        float distanceToPlayer =
            Vector2.Distance(
                transform.position,
                Player.transform.position
            );

        if (distanceToPlayer > interactionDistance)
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

        isLaserActive = true;
        activeTimer = activeDuration;

        beam.laser.enabled = true;
    }

    private void UpdateActiveLaser()
    {
        if (
            beam == null ||
            beam.laser == null
        )
        {
            DeactivateLaser();
            return;
        }

        // Recasting every frame allows reflected paths to update immediately
        // if a mirror changes while the temporary laser is active.
        beam.laser.positionCount = 0;
        beam.laserIndices.Clear();

        beam.CastRay(
            transform.position,
            transform.right,
            beam.laser
        );

        activeTimer -=
            Time.deltaTime;

        if (activeTimer <= 0f)
        {
            DeactivateLaser();
        }
    }

    private void DeactivateLaser()
    {
        isLaserActive = false;
        activeTimer = 0f;
        cooldownTimer = cooldownDuration;

        if (
            beam != null &&
            beam.laser != null
        )
        {
            beam.laser.positionCount = 0;
            beam.laser.enabled = false;
        }

        if (APReceiver != null)
        {
            APReceiver.DeActivate();
        }

        if (MPReceiver != null)
        {
            MPReceiver.DeActivate();
        }
    }

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
        {
            return;
        }

        cooldownTimer -=
            Time.deltaTime;

        if (cooldownTimer < 0f)
        {
            cooldownTimer = 0f;
        }
    }

    public bool IsLaserActive()
    {
        // Other puzzle systems can query the active state without duplicating
        // the timing logic used by this LaserPointer.
        return isLaserActive;
    }
}