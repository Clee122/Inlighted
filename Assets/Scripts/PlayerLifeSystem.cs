using System;
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

    private bool isInvulnerable = false;
    private bool isDead = false;

    [Header("Darkness Indicator settings")]
    public Image DarknessIndicator;
    private Color tempColor;
    public float AMult; // set to 1/3
    private int DarknessDamage;
    private float ResetAlpha = 0.001f;

    private PlayerAnimationController playerAnimationController;
    private PlayerLightChannel playerLightChannel;

    // Health UI and other feedback systems can listen to one shared event for
    // damage, channel healing, checkpoints, and respawning.
    public event Action<int, int> OnLivesChanged;

    private void Awake()
    {
        // The animation controller is cached so the life system can request hurt feedback
        // without directly managing the Animator states itself.
        playerAnimationController = GetComponent<PlayerAnimationController>();

        // Damage and death must be able to interrupt channel healing immediately.
        playerLightChannel = GetComponent<PlayerLightChannel>();
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

        NotifyLivesChanged();
    }

    public void TakeDamage(int amount)
    {
        // Damage is ignored while dead or briefly invulnerable so the player does not lose multiple lives
        // instantly from the same hazard.
        if (isDead || isInvulnerable)
            return;

        // Only valid damage interrupts channeling. Damage attempts ignored by
        // invulnerability do not repeatedly cancel a channel attempt.
        if (playerLightChannel != null)
        {
            playerLightChannel.InterruptByDamage();
        }

        currentLives -= amount;
        DarknessDamage += amount;

        if (currentLives < 0)
            currentLives = 0;

        NotifyLivesChanged();

        Debug.Log("Player took damage. Lives left: " + currentLives);

        if (currentLives <= 0)
        {
            Die();
        }
        else
        {
            // The hurt animation only plays when the player survives the hit.
            // The final hit should go into the death animation instead.
            if (playerAnimationController == null)
            {
                playerAnimationController = GetComponent<PlayerAnimationController>();
            }

            if (playerAnimationController != null)
            {
                playerAnimationController.PlayHurtAnimation();
            }
            else
            {
                Debug.LogWarning("PlayerAnimationController was not found, so hurt animation could not play.");
            }

            StartCoroutine(InvulnerabilityCoroutine());
        }

        UpdateDarknessIndicator();
    }

    public bool RestoreOneLife(string sourceName)
    {
        // Channel healing restores health in whole-life steps because the current
        // health system uses integer lives rather than partial health values.
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

        // Healing one life also removes one level of darkness feedback so the
        // screen state continues to match the restored health value.
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
        // This prevents death logic from running repeatedly if the player is already dead.
        // Without this, respawn could be triggered multiple times by the same hazard.
        if (isDead)
            return;

        isDead = true;
        isInvulnerable = true;

        if (playerLightChannel != null)
        {
            playerLightChannel.InterruptByDeath();
        }

        Debug.Log("Player died");

        PlayerRespawn playerRespawn = GetComponent<PlayerRespawn>();

        // RespawnPlayer is called immediately because PlayerRespawn already shows the death UI
        // at the start of its routine, then waits before moving the player back.
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

    public bool IsAtFullLives()
    {
        return currentLives >= maxLives;
    }

    public void RestoreFullLives()
    {
        // Respawn needs to reset both health and death state so the player can continue playing normally.
        // This also lets the Animator leave the death state after the player has respawned.
        currentLives = maxLives;
        isDead = false;
        isInvulnerable = false;

        NotifyLivesChanged();
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

    private void UpdateDarknessIndicator()
    {
        // Damage and channel healing both change the darkness buildup, so this
        // helper keeps the visual update consistent in either direction.
        if (DarknessIndicator == null)
        {
            return;
        }

        tempColor.a = Mathf.Clamp01(
            DarknessDamage * AMult
        );

        DarknessIndicator.color = tempColor;
    }

    private void NotifyLivesChanged()
    {
        OnLivesChanged?.Invoke(
            currentLives,
            maxLives
        );
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "check point")
        {
            currentLives = maxLives;
            DarknessIndicatorReset();
            NotifyLivesChanged();
        }
    }
}