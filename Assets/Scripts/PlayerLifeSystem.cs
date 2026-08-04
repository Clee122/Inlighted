using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLifeSystem : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;
    private int currentLives;

    // The event remains static so Eladio's HUD can subscribe without needing a
    // direct reference to the specific PlayerLifeSystem component.
    public static event Action<int, int> OnLivesChanged;

    [Header("Damage Settings")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    private bool isInvulnerable = false;
    private bool isDead = false;

    [Header("Darkness Indicator Settings")]
    public Image DarknessIndicator;
    private Color tempColor;

    // This should normally be set to one divided by the maximum number of lives
    // so each damage step increases the overlay by an equal amount.
    public float AMult;

    private int DarknessDamage;
    private float ResetAlpha = 0.001f;

    private PlayerAnimationController playerAnimationController;
    private PlayerLightChannel playerLightChannel;

    private void Awake()
    {
        // The animation controller is cached so the life system can request hurt
        // feedback without directly controlling Animator states itself.
        playerAnimationController = GetComponent<PlayerAnimationController>();

        // Damage and death must be able to interrupt channel healing immediately.
        playerLightChannel = GetComponent<PlayerLightChannel>();

        Debug.Log("LIFE CHECK 0: PlayerLifeSystem Awake completed.");
    }

    private void Start()
    {
        currentLives = maxLives;

        // Darkness damage begins at zero so each new run starts with a clean
        // screen overlay rather than inheriting damage feedback.
        DarknessDamage = 0;

        // The life system must continue functioning even if a UI reference is
        // temporarily missing because of a prefab or scene merge.
        if (DarknessIndicator != null)
        {
            tempColor = DarknessIndicator.color;
        }
        else
        {
            Debug.LogWarning("LIFE CHECK WARNING: DarknessIndicator is not assigned.");
        }

        NotifyLivesChanged();

        Debug.Log("LIFE CHECK 1: Start completed. Current lives = " + currentLives + " / " + maxLives);
    }

    public void TakeDamage(int amount)
    {
        Debug.Log(
            "LIFE CHECK 2: TakeDamage called. Amount = " + amount +
            " | isDead = " + isDead +
            " | isInvulnerable = " + isInvulnerable +
            " | currentLives before damage = " + currentLives
        );

        // Damage is ignored while dead or briefly invulnerable so one hazard
        // cannot remove several lives during the same contact.
        if (isDead || isInvulnerable)
        {
            Debug.Log("LIFE CHECK 2 STOPPED: Damage ignored because player is dead or invulnerable.");
            return;
        }

        // Only accepted damage interrupts channeling. Ignored damage attempts
        // should not repeatedly cancel the player's healing attempt.
        if (playerLightChannel != null)
        {
            Debug.Log("LIFE CHECK 3: Interrupting light channel by damage.");
            playerLightChannel.InterruptByDamage();
        }
        else
        {
            Debug.Log("LIFE CHECK 3: No PlayerLightChannel found. Skipping damage interrupt.");
        }

        currentLives -= amount;
        DarknessDamage += amount;

        currentLives = Mathf.Clamp(
            currentLives,
            0,
            maxLives
        );

        NotifyLivesChanged();

        Debug.Log(
            "LIFE CHECK 4: Damage accepted. Lives left = " +
            currentLives
        );

        if (currentLives <= 0)
        {
            Debug.Log("LIFE CHECK 5: Lives reached 0. Calling Die().");
            Die();
        }
        else
        {
            // The hurt animation plays only when the player survives the hit.
            // The final hit should transition into death behaviour instead.
            if (playerAnimationController == null)
            {
                playerAnimationController = GetComponent<PlayerAnimationController>();
                Debug.Log("LIFE CHECK 6: Tried to find PlayerAnimationController again for hurt animation.");
            }

            if (playerAnimationController != null)
            {
                Debug.Log("LIFE CHECK 7: Playing hurt animation.");
                playerAnimationController.PlayHurtAnimation();
            }
            else
            {
                Debug.LogWarning(
                    "LIFE CHECK 7 FAILED: PlayerAnimationController was not found, so the hurt animation could not play."
                );
            }

            StartCoroutine(
                InvulnerabilityCoroutine()
            );
        }

        UpdateDarknessIndicator();
    }

    public bool RestoreOneLife(string sourceName)
    {
        // Channel healing restores complete lives because the current health
        // system stores health as integers rather than partial values.
        if (isDead)
        {
            Debug.Log(
                sourceName +
                " could not restore health because the player is dead."
            );

            return false;
        }

        if (currentLives >= maxLives)
        {
            return false;
        }

        currentLives += 1;

        currentLives = Mathf.Clamp(
            currentLives,
            0,
            maxLives
        );

        // Restoring one life also removes one darkness-overlay step so the visual
        // damage feedback remains consistent with the player's actual health.
        DarknessDamage = Mathf.Max(
            0,
            DarknessDamage - 1
        );

        UpdateDarknessIndicator();
        NotifyLivesChanged();

        Debug.Log(
            sourceName +
            " restored one life. Current lives: " +
            currentLives +
            " / " +
            maxLives
        );

        return true;
    }

    private void Die()
    {
        // This prevents death and respawn logic from being started repeatedly
        // by additional hazards after the player has already died.
        if (isDead)
        {
            Debug.LogWarning("LIFE CHECK DIE STOPPED: Die() was called, but player is already dead.");
            return;
        }

        isDead = true;
        isInvulnerable = true;
        currentLives = 0;

        Debug.Log("LIFE CHECK DIE 1: Player entered Die(). isDead is now true.");

        // The death animation needs visual priority because darkness zones can cover
        // the player sprite and make the death animation difficult to see.
        if (playerAnimationController == null)
        {
            playerAnimationController = GetComponent<PlayerAnimationController>();
            Debug.Log("LIFE CHECK DIE 2: Tried to find PlayerAnimationController again.");
        }

        if (playerAnimationController != null)
        {
            Debug.Log("LIFE CHECK DIE 3: PlayerAnimationController found. Setting death visual priority.");
            playerAnimationController.SetDeathVisualPriority();
        }
        else
        {
            Debug.LogWarning("LIFE CHECK DIE 3 FAILED: PlayerAnimationController is missing.");
        }

        // Death permanently ends the active channel attempt. Respawning handles
        // the later health and light restoration separately.
        if (playerLightChannel != null)
        {
            Debug.Log("LIFE CHECK DIE 4: Interrupting channel by death.");
            playerLightChannel.InterruptByDeath();
        }
        else
        {
            Debug.Log("LIFE CHECK DIE 4: No PlayerLightChannel found. Skipping channel death interrupt.");
        }

        NotifyLivesChanged();

        Debug.Log("LIFE CHECK DIE 5: Player died. Lives notification sent.");

        PlayerRespawn playerRespawn = GetComponent<PlayerRespawn>();

        // PlayerRespawn already manages the death delay, UI and teleport, so the
        // life system only needs to request that routine once.
        if (playerRespawn != null)
        {
            Debug.Log("LIFE CHECK DIE 6: PlayerRespawn found. Calling RespawnPlayer().");
            playerRespawn.RespawnPlayer();
        }
        else
        {
            Debug.LogWarning(
                "LIFE CHECK DIE 6 FAILED: PlayerRespawn was not found, so the player could not respawn."
            );
        }
    }

    private IEnumerator InvulnerabilityCoroutine()
    {
        // Temporary invulnerability keeps repeated darkness damage readable and
        // prevents the player from losing several lives almost instantly.
        isInvulnerable = true;

        Debug.Log("LIFE CHECK INVULNERABILITY: Started for " + invulnerabilityDuration + " seconds.");

        yield return new WaitForSeconds(
            invulnerabilityDuration
        );

        isInvulnerable = false;

        Debug.Log("LIFE CHECK INVULNERABILITY: Ended.");
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

    public bool IsAtFullLives()
    {
        return currentLives >= maxLives;
    }

    public void RestoreFullLives()
    {
        // Respawning restores health and clears the death and invulnerability
        // states so normal damage and animation behaviour can resume.
        currentLives = maxLives;
        isDead = false;
        isInvulnerable = false;

        Debug.Log("LIFE CHECK RESTORE: RestoreFullLives called. isDead is now false.");

        if (playerAnimationController == null)
        {
            playerAnimationController = GetComponent<PlayerAnimationController>();
            Debug.Log("LIFE CHECK RESTORE: Tried to find PlayerAnimationController again.");
        }

        if (playerAnimationController != null)
        {
            // Death temporarily raises the CatMoth above darkness, so respawn must
            // restore the normal sorting values before gameplay continues.
            Debug.Log("LIFE CHECK RESTORE: Resetting CatMoth visual priority.");
            playerAnimationController.ResetVisualPriority();
        }
        else
        {
            Debug.LogWarning("LIFE CHECK RESTORE FAILED: PlayerAnimationController is missing.");
        }

        // The shared notification updates Eladio's HUD and any other health
        // feedback system through the same event used by damage and healing.
        NotifyLivesChanged();
    }

    public void DarknessIndicatorReset()
    {
        Debug.Log(
            "LIFE CHECK INDICATOR: Reached darkness-indicator alpha reset."
        );

        // Respawning and checkpoints clear accumulated darkness feedback so the
        // screen does not remain dark after health has been restored.
        DarknessDamage = 0;

        if (DarknessIndicator != null)
        {
            tempColor.a = ResetAlpha;
            DarknessIndicator.color = tempColor;
        }
        else
        {
            Debug.LogWarning("LIFE CHECK INDICATOR FAILED: DarknessIndicator is not assigned.");
        }
    }

    private void UpdateDarknessIndicator()
    {
        // Damage and channel healing both modify darkness buildup, so one helper
        // keeps the visual alpha calculation consistent in either direction.
        if (DarknessIndicator == null)
        {
            Debug.LogWarning("LIFE CHECK INDICATOR UPDATE FAILED: DarknessIndicator is not assigned.");
            return;
        }

        tempColor.a = Mathf.Clamp01(
            DarknessDamage * AMult
        );

        DarknessIndicator.color = tempColor;

        Debug.Log("LIFE CHECK INDICATOR UPDATE: Darkness alpha updated to " + tempColor.a);
    }

    private void NotifyLivesChanged()
    {
        // All health changes pass through this method so the HUD receives one
        // notification per change instead of duplicate event calls.
        OnLivesChanged?.Invoke(
            currentLives,
            maxLives
        );
    }

    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        // The dead pit is an immediate fail state rather than ordinary darkness
        // damage, so it enters death behaviour directly.
        if (collision.gameObject.CompareTag("dead pit"))
        {
            Debug.Log("LIFE CHECK PIT: Player collided with dead pit. Calling Die().");
            Die();
        }
    }

    private void OnTriggerEnter2D(
        Collider2D collision
    )
    {
        if (collision.gameObject.CompareTag("check point"))
        {
            // Checkpoints restore full health and clear the darkness overlay while
            // notifying the HUD through the same shared event.
            currentLives = maxLives;
            isDead = false;
            isInvulnerable = false;

            if (playerAnimationController == null)
            {
                playerAnimationController = GetComponent<PlayerAnimationController>();
            }

            if (playerAnimationController != null)
            {
                // Checkpoints can also clear death-related visual priority if the player
                // touches one after a respawn or while systems are being reset.
                playerAnimationController.ResetVisualPriority();
            }

            DarknessIndicatorReset();
            NotifyLivesChanged();

            Debug.Log(
                "LIFE CHECKPOINT: Checkpoint restored the player's lives."
            );
        }
    }
}