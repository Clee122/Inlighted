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

    // The fallback coroutine prevents the player from becoming permanently
    // invulnerable if a Hurt animation ever loses its ending Animation Event.
    private Coroutine hurtInvulnerabilityCoroutine;

    [Header("Audio")]
    // Hurt audio plays only when valid damage is accepted and the player survives.
    // Keeping it separate from death prevents the final hit from playing both sounds.
    [SerializeField] private AudioClip hurtSound;

    // Death audio plays once when the life system genuinely enters the dead state.
    // Additional hazards cannot replay it because Die() already guards against duplicates.
    [SerializeField] private AudioClip deathSound;

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
        playerAnimationController =
            GetComponent<PlayerAnimationController>();

        // Damage and death must be able to interrupt channel healing immediately.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

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

        Debug.Log(
            "LIFE CHECK 1: Start completed. Current lives = " +
            currentLives +
            " / " +
            maxLives
        );
    }

    public void TakeDamage(int amount)
    {
        Debug.Log(
            "LIFE CHECK 2: TakeDamage called. Amount = " + amount +
            " | isDead = " + isDead +
            " | isInvulnerable = " + isInvulnerable +
            " | currentLives before damage = " + currentLives
        );

        // Damage is ignored while dead or during the active Hurt reaction so one
        // hazard cannot remove several lives before the player can read the feedback.
        if (isDead || isInvulnerable)
        {
            Debug.Log(
                "LIFE CHECK 2 STOPPED: Damage ignored because player is dead or invulnerable."
            );

            return;
        }

        // Only accepted damage interrupts channeling. Ignored damage attempts
        // should not repeatedly cancel the player's healing attempt.
        if (playerLightChannel != null)
        {
            Debug.Log(
                "LIFE CHECK 3: Interrupting light channel by damage."
            );

            playerLightChannel.InterruptByDamage();
        }
        else
        {
            Debug.Log(
                "LIFE CHECK 3: No PlayerLightChannel found. Skipping damage interrupt."
            );
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
            Debug.Log(
                "LIFE CHECK 5: Lives reached 0. Calling Die()."
            );

            Die();
        }
        else
        {
            // Hurt audio belongs only to non-lethal damage. Playing it here
            // prevents the final hit from producing both hurt and death sounds.
            if (
                hurtSound != null &&
                AudioManager.Instance != null
            )
            {
                AudioManager.Instance.PlaySFX(
                    hurtSound
                );
            }

            // Hurt protection starts before the animation trigger is sent so
            // overlapping hazards cannot remove another life on the same frame.
            BeginHurtInvulnerability();

            // The hurt animation plays only when the player survives the hit.
            // The final hit should transition into death behaviour instead.
            if (playerAnimationController == null)
            {
                playerAnimationController =
                    GetComponent<PlayerAnimationController>();

                Debug.Log(
                    "LIFE CHECK 6: Tried to find PlayerAnimationController again for hurt animation."
                );
            }

            if (playerAnimationController != null)
            {
                Debug.Log(
                    "LIFE CHECK 7: Playing hurt animation."
                );

                playerAnimationController.PlayHurtAnimation();
            }
            else
            {
                Debug.LogWarning(
                    "LIFE CHECK 7 FAILED: PlayerAnimationController was not found, so the hurt animation could not play."
                );
            }
        }

        UpdateDarknessIndicator();
    }

    private void BeginHurtInvulnerability()
    {
        // Invulnerability remains active for the Hurt reaction so repeated
        // darkness contacts cannot remove another life during the animation.
        isInvulnerable = true;

        if (hurtInvulnerabilityCoroutine != null)
        {
            StopCoroutine(
                hurtInvulnerabilityCoroutine
            );
        }

        // This is a safety fallback only. Normally the final Hurt animation
        // frame calls EndHurtInvulnerability through an Animation Event.
        hurtInvulnerabilityCoroutine =
            StartCoroutine(
                HurtInvulnerabilityFallback()
            );

        Debug.Log(
            "LIFE CHECK INVULNERABILITY: Hurt protection started."
        );
    }

    private IEnumerator HurtInvulnerabilityFallback()
    {
        yield return new WaitForSeconds(
            invulnerabilityDuration
        );

        hurtInvulnerabilityCoroutine = null;

        // Death deliberately keeps the player invulnerable until respawn, so
        // the fallback must never cancel protection after the player has died.
        if (!isDead)
        {
            isInvulnerable = false;

            if (playerAnimationController != null)
            {
                // If an Animation Event is missing, the fallback also restores
                // CatMoth's normal sorting so Hurt priority cannot become permanent.
                playerAnimationController.ResetHurtVisualPriority();
            }
        }

        Debug.Log(
            "LIFE CHECK INVULNERABILITY: Fallback protection ended."
        );
    }

    public void EndHurtInvulnerability()
    {
        // The Hurt animation normally calls this on its final frame so damage
        // protection ends at the same moment as the visible reaction.
        if (hurtInvulnerabilityCoroutine != null)
        {
            StopCoroutine(
                hurtInvulnerabilityCoroutine
            );

            hurtInvulnerabilityCoroutine = null;
        }

        // Death has its own protection and a late Hurt Animation Event must not
        // accidentally make the player vulnerable after they have died.
        if (isDead)
        {
            return;
        }

        isInvulnerable = false;

        Debug.Log(
            "LIFE CHECK INVULNERABILITY: Hurt animation ended protection."
        );
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
            Debug.LogWarning(
                "LIFE CHECK DIE STOPPED: Die() was called, but player is already dead."
            );

            return;
        }

        isDead = true;
        isInvulnerable = true;
        currentLives = 0;

        // Any Hurt fallback still running is no longer needed once Death takes
        // ownership of invulnerability and visual priority.
        if (hurtInvulnerabilityCoroutine != null)
        {
            StopCoroutine(
                hurtInvulnerabilityCoroutine
            );

            hurtInvulnerabilityCoroutine = null;
        }

        /*
         * Light is cleared immediately when death begins so its HUD no longer
         * remains visibly filled during the dedicated death presentation.
         * PlayerRespawn restores the full resource later when gameplay resumes.
         */
        PlayerLightResource playerLightResource =
            GetComponent<PlayerLightResource>();

        if (playerLightResource != null)
        {
            playerLightResource.ResetLightForDeath();

            Debug.Log(
                "LIFE CHECK DIE: Light resource reset for death."
            );
        }
        else
        {
            Debug.LogWarning(
                "LIFE CHECK DIE: PlayerLightResource was not found, so Light could not be reset."
            );
        }

        Debug.Log(
            "LIFE CHECK DIE 1: Player entered Die(). isDead is now true."
        );

        // Death audio is triggered only after the dead state is successfully set.
        // The isDead guard above ensures repeated hazard contact cannot replay it.
        if (
            deathSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.PlaySFX(
                deathSound
            );
        }

        // The death animation needs visual priority because darkness zones can cover
        // the player sprite and make the death animation difficult to see.
        if (playerAnimationController == null)
        {
            playerAnimationController =
                GetComponent<PlayerAnimationController>();

            Debug.Log(
                "LIFE CHECK DIE 2: Tried to find PlayerAnimationController again."
            );
        }

        if (playerAnimationController != null)
        {
            Debug.Log(
                "LIFE CHECK DIE 3: PlayerAnimationController found. Setting death visual priority."
            );

            playerAnimationController.SetDeathVisualPriority();
        }
        else
        {
            Debug.LogWarning(
                "LIFE CHECK DIE 3 FAILED: PlayerAnimationController is missing."
            );
        }

        // Death permanently ends the active channel attempt. Respawning handles
        // the later health and light restoration separately.
        if (playerLightChannel != null)
        {
            Debug.Log(
                "LIFE CHECK DIE 4: Interrupting channel by death."
            );

            playerLightChannel.InterruptByDeath();
        }
        else
        {
            Debug.Log(
                "LIFE CHECK DIE 4: No PlayerLightChannel found. Skipping channel death interrupt."
            );
        }

        NotifyLivesChanged();

        Debug.Log(
            "LIFE CHECK DIE 5: Player died. Lives notification sent."
        );

        PlayerRespawn playerRespawn =
            GetComponent<PlayerRespawn>();

        // PlayerRespawn already manages the death delay, UI and teleport, so the
        // life system only needs to request that routine once.
        if (playerRespawn != null)
        {
            Debug.Log(
                "LIFE CHECK DIE 6: PlayerRespawn found. Calling RespawnPlayer()."
            );

            playerRespawn.RespawnPlayer();
        }
        else
        {
            Debug.LogWarning(
                "LIFE CHECK DIE 6 FAILED: PlayerRespawn was not found, so the player could not respawn."
            );
        }
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

        // Any old Hurt fallback must be cleared during respawn so it cannot
        // unexpectedly change sorting or invulnerability in the new life.
        if (hurtInvulnerabilityCoroutine != null)
        {
            StopCoroutine(
                hurtInvulnerabilityCoroutine
            );

            hurtInvulnerabilityCoroutine = null;
        }

        Debug.Log(
            "LIFE CHECK RESTORE: RestoreFullLives called. isDead is now false."
        );

        if (playerAnimationController == null)
        {
            playerAnimationController =
                GetComponent<PlayerAnimationController>();

            Debug.Log(
                "LIFE CHECK RESTORE: Tried to find PlayerAnimationController again."
            );
        }

        if (playerAnimationController != null)
        {
            // Hurt and Death can both temporarily raise CatMoth above gameplay,
            // so respawn restores the original visual priority unconditionally.
            Debug.Log(
                "LIFE CHECK RESTORE: Resetting CatMoth visual priority."
            );

            playerAnimationController.ResetVisualPriority();
        }
        else
        {
            Debug.LogWarning(
                "LIFE CHECK RESTORE FAILED: PlayerAnimationController is missing."
            );
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
            Debug.LogWarning(
                "LIFE CHECK INDICATOR FAILED: DarknessIndicator is not assigned."
            );
        }
    }

    private void UpdateDarknessIndicator()
    {
        // Damage and channel healing both modify darkness buildup, so one helper
        // keeps the visual alpha calculation consistent in either direction.
        if (DarknessIndicator == null)
        {
            Debug.LogWarning(
                "LIFE CHECK INDICATOR UPDATE FAILED: DarknessIndicator is not assigned."
            );

            return;
        }

        tempColor.a = Mathf.Clamp01(
            DarknessDamage * AMult
        );

        DarknessIndicator.color = tempColor;

        Debug.Log(
            "LIFE CHECK INDICATOR UPDATE: Darkness alpha updated to " +
            tempColor.a
        );
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
            Debug.Log(
                "LIFE CHECK PIT: Player collided with dead pit. Calling Die()."
            );

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

            // Checkpoints should also cancel any Hurt fallback that was still
            // active so restored gameplay begins from a completely clean state.
            if (hurtInvulnerabilityCoroutine != null)
            {
                StopCoroutine(
                    hurtInvulnerabilityCoroutine
                );

                hurtInvulnerabilityCoroutine = null;
            }

            if (playerAnimationController == null)
            {
                playerAnimationController =
                    GetComponent<PlayerAnimationController>();
            }

            if (playerAnimationController != null)
            {
                // Checkpoints can clear any temporary Hurt or Death sorting
                // priority before normal gameplay continues.
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