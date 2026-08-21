using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 1f;

    [Header("Death Timing")]
    [SerializeField] private float deathAnimationViewTime = 1f;
    [SerializeField] private float deathMessageViewTime = 0.3f;

    [Header("Audio")]
    // Respawn audio plays when the player is actually returned to the checkpoint.
    // Keeping it separate from death audio makes the two gameplay moments easier
    // to distinguish and lets the final sound be assigned later in the Inspector.
    [SerializeField] private AudioClip respawnSound;

    private PlayerLifeSystem playerLifeSystem;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;
    private Rigidbody2D rb;
    private PlayerController2D playerController;
    private PlayerAnimationController playerAnimationController;

    [Header("Death UI")]
    public GameObject DeathMessage;

    private void Awake()
    {
        // These references are stored once because respawn needs to coordinate player health,
        // light, movement, channeling, animation, and physics without making those systems
        // handle the complete respawn process themselves.
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        playerLightResource = GetComponent<PlayerLightResource>();
        playerLightChannel = GetComponent<PlayerLightChannel>();
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController2D>();
        playerAnimationController = GetComponent<PlayerAnimationController>();

        // DeathMessage is optional so missing UI references do not break the respawn system.
        // This is useful while the UI is still being changed by different team members.
        if (DeathMessage != null)
        {
            DeathMessage.SetActive(false);
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "RESPAWN CHECK FAILED: PlayerRespawn could not find PlayerLightResource. " +
                "Light will not be restored after respawning."
            );
        }

        Debug.Log("RESPAWN CHECK 0: PlayerRespawn Awake completed.");
    }

    public void RespawnPlayer()
    {
        Debug.Log("RESPAWN CHECK 1: RespawnPlayer() was called.");
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        Debug.Log("RESPAWN CHECK 2: RespawnRoutine() started.");

        // Respawning clears channel progress and pending refunds before health and
        // light are restored to their full values.
        if (playerLightChannel != null)
        {
            Debug.Log("RESPAWN CHECK 3: Resetting light channel for respawn.");
            playerLightChannel.ResetForRespawn();
        }
        else
        {
            Debug.Log("RESPAWN CHECK 3: No PlayerLightChannel found. Skipping channel reset.");
        }

        // Movement is disabled immediately so the player cannot keep controlling
        // the character while the death animation is being shown.
        if (playerController != null)
        {
            Debug.Log("RESPAWN CHECK 4: Disabling PlayerController2D.");
            playerController.enabled = false;
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 4 FAILED: PlayerController2D is missing.");
        }

        // Clearing velocity immediately stops any falling, jumping, or running momentum
        // from continuing during the visible death animation.
        if (rb != null)
        {
            Debug.Log("RESPAWN CHECK 5: Clearing Rigidbody velocity before death animation wait.");
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 5 FAILED: Rigidbody2D is missing.");
        }

        Debug.Log("RESPAWN CHECK 6: Waiting for death animation view time: " + deathAnimationViewTime);
        yield return new WaitForSeconds(deathAnimationViewTime);

        // Showing the death message after the animation delay lets the player see the
        // character death first, then receive respawn feedback.
        if (DeathMessage != null)
        {
            Debug.Log("RESPAWN CHECK 7: DeathMessage found. Showing death message.");
            DeathMessage.SetActive(true);
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 7 FAILED: DeathMessage is not assigned in the Inspector.");
        }

        Debug.Log("RESPAWN CHECK 8: Waiting for death message view time: " + deathMessageViewTime);
        yield return new WaitForSeconds(deathMessageViewTime);

        Debug.Log("RESPAWN CHECK 9: Waiting for respawn delay: " + respawnDelay);
        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;

            // Respawn audio is tied to the successful teleport rather than the start
            // of the death routine, so the sound communicates that gameplay is returning.
            if (
                respawnSound != null &&
                AudioManager.Instance != null
            )
            {
                AudioManager.Instance.PlaySFX(
                    respawnSound
                );
            }

            Debug.Log(
                "RESPAWN CHECK 10: Player moved to respawn point: " +
                respawnPoint.name
            );
        }
        else
        {
            Debug.LogWarning(
                "RESPAWN CHECK 10 FAILED: No respawn point is assigned. The player could not be moved."
            );
        }

        // Velocity is cleared again after teleporting because physics or input may
        // otherwise apply one additional frame of movement at the respawn position.
        if (rb != null)
        {
            Debug.Log("RESPAWN CHECK 11: Clearing Rigidbody velocity after teleport.");
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Health is fully restored after death so the player can retry the section.
        if (playerLifeSystem != null)
        {
            Debug.Log("RESPAWN CHECK 12: Resetting darkness indicator and restoring lives.");
            playerLifeSystem.DarknessIndicatorReset();
            playerLifeSystem.RestoreFullLives();

            Debug.Log("RESPAWN CHECK 12 COMPLETE: Player health restored during respawn.");
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 12 FAILED: PlayerLifeSystem is missing.");
        }

        // Respawning restores the complete light resource so the player can retry
        // the section without being disadvantaged by the previous failed attempt.
        if (playerLightResource != null)
        {
            Debug.Log("RESPAWN CHECK 13: Restoring full light resource.");
            playerLightResource.RestoreFullLight("Respawn");
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 13 FAILED: PlayerLightResource is missing.");
        }

        if (playerAnimationController != null)
        {
            Debug.Log("RESPAWN CHECK 14: Resetting CatMoth facing direction.");
            playerAnimationController.ResetFacingDirection();
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 14 FAILED: PlayerAnimationController is missing.");
        }

        // Waiting one frame before re-enabling movement helps prevent stored input
        // or physics from immediately pushing the player after respawning.
        yield return null;

        if (rb != null)
        {
            Debug.Log("RESPAWN CHECK 15: Final velocity clear before movement is re-enabled.");
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (playerController != null)
        {
            Debug.Log("RESPAWN CHECK 16: Resetting movement input and re-enabling PlayerController2D.");
            playerController.ResetMovementInput();
            playerController.enabled = true;
        }
        else
        {
            Debug.LogWarning("RESPAWN CHECK 16 FAILED: PlayerController2D is missing.");
        }

        if (DeathMessage != null)
        {
            Debug.Log("RESPAWN CHECK 17: Hiding death message.");
            DeathMessage.SetActive(false);
        }

        Debug.Log("RESPAWN CHECK 18: RespawnRoutine() completed.");
    }

    public void SetCheckpoint(Transform newCheckpoint)
    {
        if (newCheckpoint == null)
        {
            Debug.LogWarning(
                "RESPAWN CHECKPOINT FAILED: PlayerRespawn received an empty checkpoint reference."
            );

            return;
        }

        // This lets checkpoint objects update where the player respawns
        // without needing to rewrite the respawn routine.
        respawnPoint = newCheckpoint;

        Debug.Log(
            "RESPAWN CHECKPOINT: Respawn point updated to: " +
            newCheckpoint.name
        );
    }
}