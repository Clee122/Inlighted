using UnityEngine;
using UnityEngine.InputSystem;

public class ShootLaser : MonoBehaviour
{
    [Header("Puzzle")]
    // The LaserPointer stops accepting new activations after the introductory
    // puzzle has permanently succeeded.
    [SerializeField] private LightPuzzleController puzzleController;

    [Header("Laser")]
    public Material material;

    private LaserBeam beam;

    public AppearingPlatformReceiver APReceiver;
    public MovingPlatformReceiver MPReceiver;

    [Header("Interaction")]
    // The LaserPointer uses the same Player and Interact1 input as the mirrors
    // so the puzzle keeps one consistent interaction button.
    [SerializeField] private GameObject Player;

    // The player only needs to be near the LaserPointer to activate it.
    // This avoids requiring another trigger collider just for this prototype.
    [SerializeField] private float interactionDistance = 1.5f;

    [Header("Timed Laser")]
    // The laser remains active long enough for the player to observe the
    // reflected route, but it no longer solves the puzzle continuously.
    [SerializeField] private float activeDuration = 4f;

    // A short cooldown prevents repeatedly pressing the interaction button
    // from constantly restarting the laser timer.
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
        // The puzzle begins with the laser switched off. Its receivers are
        // explicitly returned to their resting state.
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

        // A solved puzzle no longer needs another activation attempt. An
        // already-running successful laser is still allowed to finish naturally.
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
        // LaserBeam is created only when the player first activates the
        // LaserPointer, preventing it from appearing when the scene loads.
        if (beam == null)
        {
            beam =
                new LaserBeam(
                    transform.position,
                    transform.right,
                    material,
                    APReceiver,
                    MPReceiver
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

        // Recasting every frame allows mirrors to change the reflected path
        // immediately while the temporary laser remains active.
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
            // Removing the LineRenderer points and disabling it prevents a
            // stale laser path from remaining visible after the timer ends.
            beam.laser.positionCount = 0;
            beam.laser.enabled = false;
        }

        if (APReceiver != null)
        {
            APReceiver.DeActivate();
        }

        if (MPReceiver != null)
        {
            // MovingPlatformReceiver now knows whether its puzzle has been
            // solved, so this call can safely preserve a completed platform.
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
        // Other puzzle visuals or prompts can later query whether the
        // environmental laser is currently producing a beam.
        return isLaserActive;
    }
}