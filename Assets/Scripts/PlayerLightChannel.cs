using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLightChannel : MonoBehaviour
{
    [Header("Channel Healing")]
    [SerializeField] private float secondsPerLife = 1.5f;

    // Each completed heal consumes this exact amount. The drain rate is derived
    // from this cost and the configured healing time.
    [SerializeField] private float lightCostPerLife = 25f;

    // A brief pause after each restored life makes each healing step readable
    // and prevents multiple lives from blending into one continuous drain.
    [SerializeField] private float delayBetweenLives = 0.5f;

    [SerializeField] private bool requireGrounded = true;

    [Header("Voluntary Cancellation")]
    [SerializeField] private float refundDelay = 0.5f;

    [Header("Audio")]
    // Channel audio loops for the full duration of a valid channel attempt.
    // Keeping the clip assignable means the final sound can be added later
    // without changing any healing or input behaviour.
    [SerializeField] private AudioClip channelSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private PlayerLifeSystem playerLifeSystem;
    private PlayerLightResource playerLightResource;
    private PlayerController2D playerController;
    private LightBurstController lightBurstController;
    private LightBeamController lightBeamController;

    private bool isChanneling;
    private bool isWaitingBetweenLives;
    private float delayBetweenLivesTimer;

    // This stores only the light spent towards the current unfinished heal.
    // Completed healing costs are cleared and can no longer be refunded.
    private float lightSpentThisAttempt;

    private float pendingRefund;
    private Coroutine refundCoroutine;

    private void Awake()
    {
        // Channeling coordinates the existing health, light, movement and ability
        // systems instead of duplicating their stored values.
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        playerLightResource = GetComponent<PlayerLightResource>();
        playerController = GetComponent<PlayerController2D>();
        lightBurstController = GetComponent<LightBurstController>();
        lightBeamController = GetComponent<LightBeamController>();

        // Safe minimum values prevent division by zero, instant accidental healing,
        // or invalid delays from producing unpredictable channel behaviour.
        secondsPerLife = Mathf.Max(0.1f, secondsPerLife);
        lightCostPerLife = Mathf.Max(0.1f, lightCostPerLife);
        delayBetweenLives = Mathf.Max(0f, delayBetweenLives);
        refundDelay = Mathf.Max(0f, refundDelay);

        if (playerLifeSystem == null)
        {
            Debug.LogError(
                "PlayerLightChannel could not find PlayerLifeSystem."
            );
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "PlayerLightChannel could not find PlayerLightResource."
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                "PlayerLightChannel could not find PlayerController2D."
            );
        }
    }

    private void Update()
    {
        if (!isChanneling)
        {
            return;
        }

        ContinueChanneling();
    }

    public void OnChannel(InputAction.CallbackContext context)
    {
        // Pressing begins the channel immediately, while releasing voluntarily
        // cancels the current unfinished healing attempt.
        if (context.started)
        {
            TryStartChanneling();
        }
        else if (context.canceled)
        {
            CancelVoluntarily(
                "Channel button released"
            );
        }
    }

    private void TryStartChanneling()
    {
        if (isChanneling)
        {
            return;
        }

        // A previous delayed refund is completed before another attempt begins.
        // This prevents refunds from overlapping or being counted twice.
        CompletePendingRefundImmediately();

        if (
            playerLifeSystem == null ||
            playerLightResource == null ||
            playerController == null
        )
        {
            Debug.LogError(
                "Channeling could not begin because a required player component is missing."
            );

            return;
        }

        if (playerLifeSystem.IsDead())
        {
            PrintBlockedReason(
                "the player is dead"
            );

            return;
        }

        if (playerLifeSystem.IsAtFullLives())
        {
            PrintBlockedReason(
                "health is already full"
            );

            return;
        }

        if (
            requireGrounded &&
            !playerController.IsGrounded()
        )
        {
            PrintBlockedReason(
                "the player is not grounded"
            );

            return;
        }

        if (playerLightResource.GetCurrentLight() <= 0f)
        {
            PrintBlockedReason(
                "the player has no light"
            );

            return;
        }

        if (
            lightBurstController != null &&
            lightBurstController.IsBurstActive()
        )
        {
            PrintBlockedReason(
                "Light Burst is active"
            );

            return;
        }

        if (
            lightBeamController != null &&
            (
                lightBeamController.IsBeamActive() ||
                lightBeamController.IsAiming()
            )
        )
        {
            PrintBlockedReason(
                "Light Beam is active or being aimed"
            );

            return;
        }

        isChanneling = true;
        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        // Movement is locked while channeling so restoring health requires the
        // player to remain stationary and commit to the action.
        playerController.SetChannelingLocked(true);

        // Channel audio begins only after every gameplay requirement has passed.
        // Rejected channel attempts therefore cannot start the healing loop.
        StartChannelAudio();

        if (showDebugLogs)
        {
            Debug.Log(
                "Light channeling started. Cost per restored life: " +
                lightCostPerLife.ToString("0.0")
            );
        }
    }

    private void ContinueChanneling()
    {
        if (playerLifeSystem.IsDead())
        {
            InterruptByDeath();
            return;
        }

        if (
            requireGrounded &&
            !playerController.IsGrounded()
        )
        {
            // Accidentally losing the ground counts as voluntary cancellation
            // rather than permanently destroying an unfinished healing cost.
            CancelVoluntarily(
                "Player left the ground"
            );

            return;
        }

        if (playerLifeSystem.IsAtFullLives())
        {
            StopWithoutRefund(
                "Channeling stopped because health is full."
            );

            return;
        }

        if (isWaitingBetweenLives)
        {
            HandleDelayBetweenLives();
            return;
        }

        DrainLightTowardsNextLife();
    }

    private void HandleDelayBetweenLives()
    {
        // No light is removed during this pause. The player remains movement-locked,
        // and continuing to hold the input begins the next heal after the timer.
        delayBetweenLivesTimer -= Time.deltaTime;

        if (delayBetweenLivesTimer > 0f)
        {
            return;
        }

        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;

        if (playerLightResource.GetCurrentLight() <= 0.001f)
        {
            StopWithoutRefund(
                "Channeling stopped after the healing delay because no light remains."
            );

            return;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Healing delay ended. Light spending has resumed."
            );
        }
    }

    private void DrainLightTowardsNextLife()
    {
        // The drain rate is calculated from the exact cost and required time.
        // At 25 light over 1.5 seconds, each successful life always costs 25.
        float lightDrainPerSecond =
            lightCostPerLife / secondsPerLife;

        float remainingCost =
            lightCostPerLife - lightSpentThisAttempt;

        // The frame drain is capped at the remaining cost so frame-rate differences
        // cannot cause a completed heal to spend more than the intended value.
        float requestedDrain = Mathf.Min(
            lightDrainPerSecond * Time.deltaTime,
            remainingCost
        );

        float lightRemoved =
            playerLightResource.RemoveLightUpTo(
                requestedDrain,
                "Health channeling",
                false
            );

        lightSpentThisAttempt += lightRemoved;

        if (
            lightSpentThisAttempt >=
            lightCostPerLife - 0.001f
        )
        {
            lightSpentThisAttempt =
                lightCostPerLife;

            CompleteHeal();
            return;
        }

        if (
            playerLightResource.GetCurrentLight() <=
            0.001f
        )
        {
            // No life was restored, so an attempt that runs out of light is treated
            // as incomplete and its spent light is returned after the refund delay.
            CancelVoluntarily(
                "Channeling stopped because there was not enough light to complete the heal"
            );
        }
    }

    private void CompleteHeal()
    {
        bool restoredLife =
            playerLifeSystem.RestoreOneLife(
                "Light channeling"
            );

        if (!restoredLife)
        {
            // If health cannot be restored, the current unfinished transaction is
            // returned instead of permanently charging the player.
            CancelVoluntarily(
                "Health could not be restored"
            );

            return;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Channeling restored one life after spending exactly " +
                lightCostPerLife.ToString("0.0") +
                " light."
            );
        }

        // The completed cost has successfully been converted into health and must
        // no longer be available to voluntary cancellation refunds.
        lightSpentThisAttempt = 0f;

        if (playerLifeSystem.IsAtFullLives())
        {
            StopWithoutRefund(
                "Channeling completed because health is now full."
            );

            return;
        }

        if (
            playerLightResource.GetCurrentLight() <=
            0.001f
        )
        {
            StopWithoutRefund(
                "Channeling stopped because no light remains."
            );

            return;
        }

        BeginDelayBetweenLives();
    }

    private void BeginDelayBetweenLives()
    {
        if (delayBetweenLives <= 0f)
        {
            isWaitingBetweenLives = false;
            delayBetweenLivesTimer = 0f;
            return;
        }

        isWaitingBetweenLives = true;
        delayBetweenLivesTimer = delayBetweenLives;

        if (showDebugLogs)
        {
            Debug.Log(
                "One life was restored. Light spending paused for " +
                delayBetweenLives.ToString("0.00") +
                " seconds."
            );
        }
    }

    public void CancelForPlayerAction(string actionName)
    {
        // This remains available for future interactions that should deliberately
        // cancel channeling, although movement and abilities currently ignore input.
        if (!isChanneling)
        {
            return;
        }

        CancelVoluntarily(actionName);
    }

    private void CancelVoluntarily(string reason)
    {
        if (!isChanneling)
        {
            return;
        }

        isChanneling = false;
        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;

        // The loop must stop at the same moment the channel state ends so audio
        // cannot continue during the delayed refund period.
        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(false);
        }

        float amountToRefund =
            lightSpentThisAttempt;

        lightSpentThisAttempt = 0f;

        if (amountToRefund > 0.001f)
        {
            pendingRefund += amountToRefund;

            if (refundCoroutine != null)
            {
                StopCoroutine(refundCoroutine);
            }

            refundCoroutine = StartCoroutine(
                RefundAfterDelay()
            );
        }

        if (showDebugLogs)
        {
            Debug.Log(
                reason +
                " cancelled channeling. Pending light refund: " +
                amountToRefund.ToString("0.000")
            );
        }
    }

    public void InterruptByDamage()
    {
        // Valid damage permanently removes the light spent towards the current heal
        // and also destroys any refund that was still waiting to be returned.
        CancelPendingRefund();

        if (!isChanneling)
        {
            return;
        }

        float lostLight =
            lightSpentThisAttempt;

        isChanneling = false;
        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        // Damage ends the channel immediately, so its audio must also stop
        // before normal movement or hurt behaviour resumes.
        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(false);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Channeling was interrupted by damage. Lost unfinished light: " +
                lostLight.ToString("0.000")
            );
        }
    }

    public void InterruptByDeath()
    {
        // Death clears channel progress and pending refunds because respawning
        // restores the player's health and light separately.
        CancelPendingRefund();

        isChanneling = false;
        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        // Death must always clear the channel loop even if the channel state
        // changes before the respawn sequence begins.
        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(false);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Channeling was cleared because the player died."
            );
        }
    }

    public void ResetForRespawn()
    {
        // Respawning must not preserve an old healing attempt, healing delay, or
        // refund because the respawn system restores the player completely.
        CancelPendingRefund();

        isChanneling = false;
        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        // Respawn is another safety boundary for the loop. Stopping it here
        // guarantees an interrupted death sequence cannot leave channel audio active.
        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(false);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Channeling state was reset for respawn."
            );
        }
    }

    private void StopWithoutRefund(string reason)
    {
        isChanneling = false;
        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        // Natural completion, full health and exhausted light all end the active
        // channel, so the loop should stop without waiting for input release.
        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(false);
        }

        if (showDebugLogs)
        {
            Debug.Log(reason);
        }
    }

    private void StartChannelAudio()
    {
        if (
            channelSound == null ||
            AudioManager.Instance == null
        )
        {
            return;
        }

        // The AudioManager protects against restarting an identical active loop,
        // allowing this method to stay safe if channel-start logic changes later.
        AudioManager.Instance.StartLoopingSFX(
            channelSound
        );
    }

    private void StopChannelAudio()
    {
        if (
            channelSound == null ||
            AudioManager.Instance == null
        )
        {
            return;
        }

        // Passing the channel clip means this script only stops its own loop.
        // It cannot accidentally stop another looping sound owned by a different system.
        AudioManager.Instance.StopLoopingSFX(
            channelSound
        );
    }

    private IEnumerator RefundAfterDelay()
    {
        yield return new WaitForSeconds(refundDelay);

        refundCoroutine = null;

        if (
            pendingRefund <= 0.001f ||
            playerLightResource == null
        )
        {
            pendingRefund = 0f;
            yield break;
        }

        float refundAmount =
            pendingRefund;

        pendingRefund = 0f;

        playerLightResource.RestoreLight(
            refundAmount,
            "Cancelled channel refund"
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "Cancelled channel refunded " +
                refundAmount.ToString("0.000") +
                " light."
            );
        }
    }

    private void CompletePendingRefundImmediately()
    {
        if (pendingRefund <= 0.001f)
        {
            pendingRefund = 0f;
            return;
        }

        if (refundCoroutine != null)
        {
            StopCoroutine(refundCoroutine);
            refundCoroutine = null;
        }

        float refundAmount =
            pendingRefund;

        pendingRefund = 0f;

        if (playerLightResource != null)
        {
            playerLightResource.RestoreLight(
                refundAmount,
                "Previous channel refund"
            );
        }
    }

    private void CancelPendingRefund()
    {
        if (refundCoroutine != null)
        {
            StopCoroutine(refundCoroutine);
            refundCoroutine = null;
        }

        if (
            pendingRefund > 0.001f &&
            showDebugLogs
        )
        {
            Debug.Log(
                "Pending channel refund was cancelled. Lost light: " +
                pendingRefund.ToString("0.000")
            );
        }

        pendingRefund = 0f;
    }

    private void PrintBlockedReason(string reason)
    {
        if (showDebugLogs)
        {
            Debug.Log(
                "Channeling could not begin because " +
                reason +
                "."
            );
        }
    }

    public bool IsChanneling()
    {
        return isChanneling;
    }

    public bool IsWaitingBetweenLives()
    {
        // Animation or visual feedback can later use this to distinguish active
        // light draining from the short pause after a successful heal.
        return isWaitingBetweenLives;
    }

    public bool IsRefundPending()
    {
        // Movement regeneration must remain paused while unfinished channel light
        // is waiting to be returned, otherwise regeneration and refund can stack.
        return pendingRefund > 0.001f ||
               refundCoroutine != null;
    }

    private void OnDisable()
    {
        // Disabling the component or Player should never leave its looping
        // channel sound active after the gameplay system has stopped running.
        StopChannelAudio();
    }
}