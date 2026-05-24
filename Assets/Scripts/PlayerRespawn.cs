using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 1f;

    private PlayerLifeSystem playerLifeSystem;
    private Rigidbody2D rb;
    private PlayerController2D playerController;
    private PlayerAnimationController playerAnimationController;

    [Header("Death UI")]
    public GameObject DeathMessage;

    private void Awake()
    {
        // These references are stored once because respawn needs to coordinate player health,
        // movement, animation, and physics without making those systems handle respawn by themselves.
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController2D>();
        playerAnimationController = GetComponent<PlayerAnimationController>();

        // DeathMessage is optional so missing UI references do not break the respawn system.
        // This is useful while the UI is still being changed by different team members.
        if (DeathMessage != null)
        {
            DeathMessage.SetActive(false);
        }
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
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
        }

        // Clear velocity again after teleporting because physics/input can sometimes apply
        // one more frame of movement before the respawn position fully settles.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // The life system handles restoring health and resetting the damage overlay,
        // while this script only decides when that reset should happen.
        if (playerLifeSystem != null)
        {
            playerLifeSystem.DarknessIndicatorReset();
            playerLifeSystem.RestoreFullLives();
        }

        if (playerAnimationController != null)
        {
            // Respawn always resets the placeholder character to face the default/front direction,
            // so the player does not reappear facing the direction they died in.
            playerAnimationController.ResetFacingDirection();
        }

        // Waiting one frame before re-enabling movement helps avoid stored input or physics
        // immediately pushing the player on the same frame they respawn.
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
    }

    public void SetCheckpoint(Transform newCheckpoint)
    {
        // This lets future checkpoint objects update where the player respawns
        // without needing to rewrite the respawn routine.
        respawnPoint = newCheckpoint;
    }
}