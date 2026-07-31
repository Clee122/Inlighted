using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 1f;

    [Header("Light Penalty")]
    [SerializeField] private float respawnLightPenalty = 10f;

    private PlayerLifeSystem playerLifeSystem;
    private PlayerLightResource playerLightResource;
    private Rigidbody2D rb;
    private PlayerController2D playerController;
    private PlayerAnimationController playerAnimationController;

    [Header("Death UI")]
    public GameObject DeathMessage;

    private void Awake()
    {
        // These references are stored once because respawn needs to coordinate player health,
        // light, movement, animation, and physics without making those systems handle respawn.
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        playerLightResource = GetComponent<PlayerLightResource>();
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController2D>();
        playerAnimationController = GetComponent<PlayerAnimationController>();

        // A negative penalty would accidentally add light, so the value is kept at
        // zero or higher before it is used during the respawn process.
        respawnLightPenalty = Mathf.Max(0f, respawnLightPenalty);

        // DeathMessage is optional so missing UI references do not break the respawn system.
        // This is useful while the UI is still being changed by different team members.
        if (DeathMessage != null)
        {
            DeathMessage.SetActive(false);
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "PlayerRespawn could not find PlayerLightResource. " +
                "The respawn light penalty will not be applied."
            );
        }
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        Debug.Log("Player respawn routine started.");

        // Showing the death message here gives feedback before the player is moved back.
        // If no death UI is assigned, the gameplay respawn should still continue normally.
        if (DeathMessage != null)
        {
            DeathMessage.SetActive(true);
        }

        // Movement is disabled during death so held input cannot keep affecting the player
        // while the death animation and respawn delay are happening.
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // Clearing velocity immediately stops any falling, jumping, or running momentum
        // from continuing during the death state.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;

            Debug.Log(
                "Player moved to respawn point: " +
                respawnPoint.name
            );
        }
        else
        {
            Debug.LogWarning(
                "No respawn point is assigned. The player could not be moved."
            );
        }

        // Velocity is cleared again after teleporting because physics or input may
        // otherwise apply one additional frame of movement at the respawn position.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Health is fully restored after death, while light is handled separately
        // so it can retain its previous value with a configurable penalty.
        if (playerLifeSystem != null)
        {
            playerLifeSystem.DarknessIndicatorReset();
            playerLifeSystem.RestoreFullLives();

            Debug.Log("Player health restored during respawn.");
        }

        if (playerLightResource != null)
        {
            // Respawning preserves the resource the player had when they died,
            // but removes a small amount so death still has a light-energy consequence.
            playerLightResource.LoseLight(
                respawnLightPenalty,
                "Respawn penalty"
            );
        }

        if (playerAnimationController != null)
        {
            // Respawn always resets CatMoth to face the default/right direction,
            // so the player does not reappear facing the direction they died in.
            playerAnimationController.ResetFacingDirection();
        }

        // Waiting one frame before re-enabling movement helps prevent stored input
        // or physics from immediately pushing the player after respawning.
        yield return null;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (playerController != null)
        {
            // Reset input before re-enabling movement so the player does not continue
            // running or jumping from input held before death.
            playerController.ResetMovementInput();
            playerController.enabled = true;
        }

        if (DeathMessage != null)
        {
            DeathMessage.SetActive(false);
        }

        Debug.Log("Player respawn routine completed.");
    }

    public void SetCheckpoint(Transform newCheckpoint)
    {
        if (newCheckpoint == null)
        {
            Debug.LogWarning(
                "PlayerRespawn received an empty checkpoint reference."
            );

            return;
        }

        // This lets checkpoint objects update where the player respawns
        // without needing to rewrite the respawn routine.
        respawnPoint = newCheckpoint;

        Debug.Log(
            "Respawn point updated to: " +
            newCheckpoint.name
        );
    }
}