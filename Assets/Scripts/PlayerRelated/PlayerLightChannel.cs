using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLightChannel : MonoBehaviour
{
    [Header("Channel Healing")]

    // Each completed Heal animation consumes this amount of light before the
    // configured healing frame is reached.
    [SerializeField] private float lightCostPerLife = 25f;

    // A short pause separates consecutive Heal cycles while Q remains held.
    [SerializeField] private float delayBetweenLives = 0.5f;

    [SerializeField] private bool requireGrounded = true;

    [Header("Voluntary Cancellation")]
    [SerializeField] private float refundDelay = 0.5f;

    [Header("Audio")]

    // Channel audio loops for the duration of a valid healing attempt.
    [SerializeField] private AudioClip channelSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private PlayerLifeSystem playerLifeSystem;
    private PlayerLightResource playerLightResource;
    private PlayerController2D playerController;
    private LightBurstController lightBurstController;
    private LightBeamController lightBeamController;
    private PlayerAnimationController playerAnimationController;

    private bool isChanneling;
    private bool isWaitingBetweenLives;
    private bool channelInputHeld;

    // Once one cycle reaches its healing point, this prevents it from restoring
    // more than one life before a new animation cycle begins.
    private bool healAppliedThisCycle;

    private float delayBetweenLivesTimer;

    // Only light spent during the current unfinished animation can be refunded.
    private float lightSpentThisAttempt;

    private float pendingRefund;
    private Coroutine refundCoroutine;

    private void Awake()
    {
        // Channeling coordinates existing systems instead of duplicating health,
        // movement, light and ability state inside this script.
        playerLifeSystem =
            GetComponent<PlayerLifeSystem>();

        playerLightResource =
            GetComponent<PlayerLightResource>();

        playerController =
            GetComponent<PlayerController2D>();

        lightBurstController =
            GetComponent<LightBurstController>();

        lightBeamController =
            GetComponent<LightBeamController>();

        playerAnimationController =
            GetComponent<PlayerAnimationController>();

        lightCostPerLife =
            Mathf.Max(
                0.1f,
                lightCostPerLife
            );

        delayBetweenLives =
            Mathf.Max(
                0f,
                delayBetweenLives
            );

        refundDelay =
            Mathf.Max(
                0f,
                refundDelay
            );
    }

    private void Update()
    {
        if (!isChanneling)
        {
            return;
        }

        ContinueChanneling();
    }

    public void OnChannel(
        InputAction.CallbackContext context
    )
    {
        if (context.started)
        {
            channelInputHeld = true;
            TryStartChanneling();
        }
        else if (context.canceled)
        {
            channelInputHeld = false;

            // Once a successful life restoration has already happened, the
            // current animation is allowed to finish before the channel stops.
            if (healAppliedThisCycle)
            {
                return;
            }

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

        CompletePendingRefundImmediately();

        if (
            playerLifeSystem == null ||
            playerLightResource == null ||
            playerController == null
        )
        {
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

        if (
            playerLightResource.GetCurrentLight() <=
            0f
        )
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
        healAppliedThisCycle = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        // Healing is a committed stationary action, so movement remains locked
        // while the channel is active.
        playerController.SetChannelingLocked(
            true
        );

        if (playerAnimationController != null)
        {
            // The Bool marks the gameplay channel as active, while the explicit
            // restart guarantees the first Heal begins from frame 0.
            playerAnimationController.SetChannelingAnimation(
                true
            );

            playerAnimationController.RestartHealAnimation();
        }

        StartChannelAudio();

        if (showDebugLogs)
        {
            Debug.Log(
                "Light channeling started. Heal animation restarted from frame 0."
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
            CancelVoluntarily(
                "Player left the ground"
            );

            return;
        }

        if (isWaitingBetweenLives)
        {
            HandleDelayBetweenLives();
        }
    }

    public void UpdateChannelFromHealAnimation(
        float animationProgress,
        float healApplyNormalisedTime
    )
    {
        if (
            !isChanneling ||
            isWaitingBetweenLives ||
            healAppliedThisCycle
        )
        {
            return;
        }

        float safeHealPoint =
            Mathf.Clamp(
                healApplyNormalisedTime,
                0.01f,
                1f
            );

        float channelProgress =
            Mathf.Clamp01(
                animationProgress /
                safeHealPoint
            );

        float targetLightSpent =
            lightCostPerLife *
            channelProgress;

        float lightStillToSpend =
            targetLightSpent -
            lightSpentThisAttempt;

        if (lightStillToSpend <= 0.001f)
        {
            return;
        }

        float lightRemoved =
            playerLightResource.RemoveLightUpTo(
                lightStillToSpend,
                "Health channeling",
                false
            );

        lightSpentThisAttempt +=
            lightRemoved;

        // Running out of light before reaching the healing point cancels the
        // unfinished attempt rather than granting a partially paid heal.
        if (
            lightRemoved <
            lightStillToSpend - 0.001f &&
            playerLightResource.GetCurrentLight() <=
            0.001f
        )
        {
            CancelVoluntarily(
                "Channeling stopped because there was not enough light to complete the heal"
            );
        }
    }

    public void ApplyPendingHealFromAnimation()
    {
        if (
            !isChanneling ||
            isWaitingBetweenLives ||
            healAppliedThisCycle
        )
        {
            return;
        }

        float remainingCost =
            lightCostPerLife -
            lightSpentThisAttempt;

        if (remainingCost > 0.001f)
        {
            float finalLightRemoved =
                playerLightResource.RemoveLightUpTo(
                    remainingCost,
                    "Health channeling",
                    false
                );

            lightSpentThisAttempt +=
                finalLightRemoved;
        }

        if (
            lightSpentThisAttempt <
            lightCostPerLife - 0.001f
        )
        {
            CancelVoluntarily(
                "Channeling stopped because there was not enough light to complete the heal"
            );

            return;
        }

        bool restoredLife =
            playerLifeSystem.RestoreOneLife(
                "Light channeling"
            );

        if (!restoredLife)
        {
            CancelVoluntarily(
                "Health could not be restored"
            );

            return;
        }

        healAppliedThisCycle = true;

        // A completed light cost has become health, so it cannot be refunded by
        // later cancellation of the remaining animation frames.
        lightSpentThisAttempt = 0f;

        if (showDebugLogs)
        {
            Debug.Log(
                "Combined Heal animation restored one life."
            );
        }
    }

    public void FinishHealAnimation()
    {
        if (
            !isChanneling ||
            isWaitingBetweenLives
        )
        {
            return;
        }

        // The Animator Bool is cleared between healing cycles so the current
        // completed Heal can return to normal locomotion before the next restart.
        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }

        if (!healAppliedThisCycle)
        {
            ApplyPendingHealFromAnimation();
        }

        if (!isChanneling)
        {
            return;
        }

        if (!healAppliedThisCycle)
        {
            return;
        }

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

        if (!channelInputHeld)
        {
            StopWithoutRefund(
                "Channeling ended after the Heal animation because the channel button was released."
            );

            return;
        }

        BeginDelayBetweenLives();
    }

    private void BeginDelayBetweenLives()
    {
        healAppliedThisCycle = false;
        lightSpentThisAttempt = 0f;

        if (delayBetweenLives <= 0f)
        {
            isWaitingBetweenLives = false;
            delayBetweenLivesTimer = 0f;

            BeginNextHealCycle();
            return;
        }

        isWaitingBetweenLives = true;

        delayBetweenLivesTimer =
            delayBetweenLives;
    }

    private void HandleDelayBetweenLives()
    {
        delayBetweenLivesTimer -=
            Time.deltaTime;

        if (delayBetweenLivesTimer > 0f)
        {
            return;
        }

        isWaitingBetweenLives = false;
        delayBetweenLivesTimer = 0f;

        if (!channelInputHeld)
        {
            StopWithoutRefund(
                "Channeling ended because the channel button was released."
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

        BeginNextHealCycle();
    }

    private void BeginNextHealCycle()
    {
        healAppliedThisCycle = false;
        lightSpentThisAttempt = 0f;

        if (playerAnimationController != null)
        {
            // Each consecutive heal explicitly restarts the completed non-looping
            // state so the next cycle begins from animation frame 0.
            playerAnimationController.SetChannelingAnimation(
                true
            );

            playerAnimationController.RestartHealAnimation();
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Next combined Heal cycle restarted from frame 0."
            );
        }
    }

    public void CancelForPlayerAction(
        string actionName
    )
    {
        if (!isChanneling)
        {
            return;
        }

        CancelVoluntarily(
            actionName
        );
    }

    private void CancelVoluntarily(
        string reason
    )
    {
        if (!isChanneling)
        {
            return;
        }

        isChanneling = false;
        isWaitingBetweenLives = false;
        healAppliedThisCycle = false;
        delayBetweenLivesTimer = 0f;

        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }

        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(
                false
            );
        }

        float amountToRefund =
            lightSpentThisAttempt;

        lightSpentThisAttempt = 0f;

        if (amountToRefund > 0.001f)
        {
            pendingRefund +=
                amountToRefund;

            if (refundCoroutine != null)
            {
                StopCoroutine(
                    refundCoroutine
                );
            }

            refundCoroutine =
                StartCoroutine(
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
        CancelPendingRefund();

        if (!isChanneling)
        {
            return;
        }

        isChanneling = false;
        isWaitingBetweenLives = false;
        healAppliedThisCycle = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }

        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(
                false
            );
        }
    }

    public void InterruptByDeath()
    {
        CancelPendingRefund();

        isChanneling = false;
        isWaitingBetweenLives = false;
        healAppliedThisCycle = false;
        channelInputHeld = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }

        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(
                false
            );
        }
    }

    public void ResetForRespawn()
    {
        CancelPendingRefund();

        isChanneling = false;
        isWaitingBetweenLives = false;
        healAppliedThisCycle = false;
        channelInputHeld = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }

        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(
                false
            );
        }
    }

    private void StopWithoutRefund(
        string reason
    )
    {
        isChanneling = false;
        isWaitingBetweenLives = false;
        healAppliedThisCycle = false;
        delayBetweenLivesTimer = 0f;
        lightSpentThisAttempt = 0f;

        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }

        StopChannelAudio();

        if (playerController != null)
        {
            playerController.SetChannelingLocked(
                false
            );
        }

        if (showDebugLogs)
        {
            Debug.Log(
                reason
            );
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

        AudioManager.Instance.StopLoopingSFX(
            channelSound
        );
    }

    private IEnumerator RefundAfterDelay()
    {
        yield return new WaitForSeconds(
            refundDelay
        );

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
            StopCoroutine(
                refundCoroutine
            );

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
            StopCoroutine(
                refundCoroutine
            );

            refundCoroutine = null;
        }

        pendingRefund = 0f;
    }

    private void PrintBlockedReason(
        string reason
    )
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
        return isWaitingBetweenLives;
    }

    public bool IsRefundPending()
    {
        return
            pendingRefund > 0.001f ||
            refundCoroutine != null;
    }

    private void OnDisable()
    {
        StopChannelAudio();

        if (playerAnimationController != null)
        {
            playerAnimationController.SetChannelingAnimation(
                false
            );
        }
    }
}