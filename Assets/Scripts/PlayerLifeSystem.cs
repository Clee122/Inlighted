using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PlayerLifeSystem : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;
    private int currentLives;

    [Header("Damage Settings")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    [Header("Death Animation")]
    [SerializeField] private float deathAnimationDelay = 0.6f;

    private bool isInvulnerable = false;
    private bool isDead = false;

    [Header("Darkness Indicator settings")]
    public Image DarknessIndicator;
    private Color tempColor;
    public float AMult; // set to 1/3
    private int DarknessDamage;
    private float ResetAlpha = 0.001f;

    private PlayerAnimationController playerAnimationController;

    private void Awake()
    {
        // The animation controller is cached once so the life system can tell the visual layer
        // when damage or death happens without directly controlling animation states itself.
        playerAnimationController = GetComponent<PlayerAnimationController>();
    }

    private void Start()
    {
        currentLives = maxLives;

        // This tracks how much darkness damage has been taken so the screen overlay can become darker.
        // It is reset on start so the player begins each run with a clean visual state.
        DarknessDamage = 0;

        // The null check is important because UI references can break during merges or scene changes.
        // If the darkness indicator is not assigned, the life system should still work instead of
        // causing death/respawn to break.
        if (DarknessIndicator != null)
        {
            tempColor = DarknessIndicator.color;
        }
    }

    public void TakeDamage(int amount)
    {
        // Damage is ignored while dead or briefly invulnerable so the player does not lose multiple lives
        // instantly from the same hazard.
        if (isDead || isInvulnerable)
            return;

        currentLives -= amount;
        DarknessDamage += amount;

        if (currentLives < 0)
            currentLives = 0;

        Debug.Log("Player took damage. Lives left: " + currentLives);

        if (currentLives <= 0)
        {
            Die();
        }
        else
        {
            // The hurt animation only plays when the player survives the hit.
            // Death has its own animation, so this avoids the hurt animation interrupting death.
            if (playerAnimationController != null)
            {
                playerAnimationController.PlayHurtAnimation();
            }

            StartCoroutine(InvulnerabilityCoroutine());
        }

        // The darkness indicator gives visual feedback for taking damage.
        // It is kept optional so the gameplay systems are not dependent on the UI being assigned correctly.
        if (DarknessIndicator != null)
        {
            tempColor.a = DarknessDamage * AMult;
            DarknessIndicator.color = tempColor;
        }
    }

    private void Die()
    {
        // This prevents death logic from running repeatedly if the player is already dead.
        // Without this, respawn could be triggered multiple times by the same hazard.
        if (isDead)
            return;

        isDead = true;
        isInvulnerable = true;

        Debug.Log("Player died");

        // Respawn is delayed so the death animation has time to play before the player
        // is restored at the checkpoint.
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathAnimationDelay);

        PlayerRespawn playerRespawn = GetComponent<PlayerRespawn>();

        // Respawn is kept in a separate script so the life system only decides when the player dies,
        // while the respawn script decides how the player returns to the level.
        if (playerRespawn != null)
        {
            playerRespawn.RespawnPlayer();
        }
    }

    private IEnumerator InvulnerabilityCoroutine()
    {
        // Temporary invulnerability keeps damage readable and fair, especially inside darkness zones
        // where the player could otherwise take damage too quickly.
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }

    public int GetCurrentLives()
    {
        return currentLives;
    }

    public int GetMaxLives()
    {
        return maxLives;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public void RestoreFullLives()
    {
        // Respawn needs to reset both health and death state so the player can continue playing normally.
        // This also lets the Animator leave the death state after the player has respawned.
        currentLives = maxLives;
        isDead = false;
        isInvulnerable = false;
    }

    public void DarknessIndicatorReset()
    {
        Debug.Log("reached code for alpha change");

        // This resets the visual damage buildup after respawn so the screen does not stay dark
        // after the player has been restored.
        DarknessDamage = 0;

        if (DarknessIndicator != null)
        {
            tempColor.a = ResetAlpha;
            DarknessIndicator.color = tempColor;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // The dead pit instantly triggers death because falling into it should be treated as a fail state,
        // not as normal tick damage from darkness.
        if (collision.gameObject.tag == "dead pit")
        {
            Die();
        }
    }
}