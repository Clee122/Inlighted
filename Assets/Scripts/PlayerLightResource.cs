using System;
using UnityEngine;

public class PlayerLightResource : MonoBehaviour
{
    [Header("Light Resource")]
    [SerializeField] private float maximumLight = 100f;

    // The player begins with no stored light so movement introduces how the
    // renewable portion of the resource works during the opening gameplay.
    [SerializeField] private float startingLight = 0f;

    [Header("Current Light - Runtime Display")]

    // Serialising the runtime value makes it visible in the Inspector during testing.
    // Awake resets this value to startingLight whenever gameplay begins.
    [SerializeField] private float currentLight = 0f;

    [Header("Movement Regeneration")]
    [SerializeField, Range(0f, 1f)]
    private float movementRegenerationLimitPercentage = 0.5f;

    [SerializeField] private float movementRegenerationRate = 10f;

    [Header("References")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private float debugLightInterval = 5f;

    private bool wasRegenerating;
    private bool regenerationLimitWasReached;
    private float nextDebugLightValue;

    // Other systems can subscribe to this event so character brightness and optional UI
    // update only when the light value changes instead of checking it every frame.
    public event Action<float, float> OnLightChanged;

    private void Reset()
    {
        // These systems are expected to be attached to the Player, so automatically
        // assigning them reduces setup mistakes when the component is first added.
        playerController = GetComponent<PlayerController2D>();
        lightBurstController = GetComponent<LightBurstController>();
        lightBeamController = GetComponent<LightBeamController>();
    }

    private void Awake()
    {
        // These fallbacks keep the resource system working if Inspector references
        // are lost while the Player prefab is edited or merged.
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (lightBurstController == null)
        {
            lightBurstController = GetComponent<LightBurstController>();
        }

        if (lightBeamController == null)
        {
            lightBeamController = GetComponent<LightBeamController>();
        }

        // These limits prevent invalid Inspector values from breaking resource
        // calculations or allowing the current value to exceed the maximum.
        maximumLight = Mathf.Max(1f, maximumLight);
        startingLight = Mathf.Clamp(startingLight, 0f, maximumLight);
        movementRegenerationRate = Mathf.Max(0f, movementRegenerationRate);

        currentLight = startingLight;
        nextDebugLightValue = GetNextDebugThreshold(currentLight);

        NotifyLightChanged();

        if (showDebugLogs)
        {
            Debug.Log(
                "Light resource initialised. Current light: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0")
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                "PlayerLightResource could not find PlayerController2D. " +
                "Movement regeneration will not work."
            );
        }
    }

    private void Update()
    {
        HandleMovementRegeneration();
    }

    private void HandleMovementRegeneration()
    {
        if (playerController == null)
        {
            if (wasRegenerating)
            {
                StopRegenerationDebug(
                    "Light regeneration stopped because PlayerController2D is missing."
                );
            }

            return;
        }

        float regenerationLimit =
            maximumLight * movementRegenerationLimitPercentage;

        bool isBelowRegenerationLimit =
            currentLight < regenerationLimit;

        bool isPlayerControlledMovement =
            playerController.IsActivelyGeneratingLight();

        // Regeneration pauses while an ability is actively producing light.
        // This prevents movement during Burst or Beam from immediately refunding
        // some of the energy spent to activate that ability.
        bool isLightAbilityActive =
            (lightBurstController != null &&
             lightBurstController.IsBurstActive()) ||
            (lightBeamController != null &&
             lightBeamController.IsBeamActive());

        bool shouldRegenerate =
            isBelowRegenerationLimit &&
            isPlayerControlledMovement &&
            playerController.enabled &&
            !isLightAbilityActive;

        if (!shouldRegenerate)
        {
            if (wasRegenerating)
            {
                string stopReason;

                if (isLightAbilityActive)
                {
                    stopReason =
                        "Movement regeneration paused because a light ability is active.";
                }
                else if (currentLight >= regenerationLimit)
                {
                    stopReason =
                        "Movement regeneration reached its limit.";
                }
                else
                {
                    stopReason =
                        "Player-controlled movement stopped.";
                }

                StopRegenerationDebug(stopReason);
            }

            return;
        }

        if (!wasRegenerating)
        {
            wasRegenerating = true;
            regenerationLimitWasReached = false;
            nextDebugLightValue = GetNextDebugThreshold(currentLight);

            if (showDebugLogs)
            {
                Debug.Log(
                    "Light movement regeneration started at: " +
                    currentLight.ToString("0.0")
                );
            }
        }

        float previousLight = currentLight;

        currentLight += movementRegenerationRate * Time.deltaTime;
        currentLight = Mathf.Min(currentLight, regenerationLimit);

        if (!Mathf.Approximately(previousLight, currentLight))
        {
            NotifyLightChanged();
            PrintRegenerationProgress();
        }

        if (
            currentLight >= regenerationLimit &&
            !regenerationLimitWasReached
        )
        {
            regenerationLimitWasReached = true;

            if (showDebugLogs)
            {
                Debug.Log(
                    "Movement regeneration reached the " +
                    (movementRegenerationLimitPercentage * 100f).ToString("0") +
                    "% limit: " +
                    currentLight.ToString("0.0") +
                    " / " +
                    maximumLight.ToString("0.0")
                );
            }
        }
    }

    public bool CanSpendLight(float amount)
    {
        // Zero or negative costs should not block an ability, while positive costs
        // require the player to have enough stored light.
        if (amount <= 0f)
        {
            return true;
        }

        return currentLight >= amount;
    }

    public bool TrySpendLight(float amount, string sourceName)
    {
        // All abilities spend light through this method so value checks, clamping
        // and debugging remain consistent across the project.
        if (amount <= 0f)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    sourceName +
                    " used no light because its configured cost is zero."
                );
            }

            return true;
        }

        if (!CanSpendLight(amount))
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "Not enough light to use " +
                    sourceName +
                    ". Required: " +
                    amount.ToString("0.0") +
                    " | Current: " +
                    currentLight.ToString("0.0")
                );
            }

            return false;
        }

        currentLight -= amount;
        currentLight = Mathf.Clamp(currentLight, 0f, maximumLight);

        regenerationLimitWasReached = false;
        nextDebugLightValue = GetNextDebugThreshold(currentLight);

        NotifyLightChanged();

        if (showDebugLogs)
        {
            Debug.Log(
                sourceName +
                " spent " +
                amount.ToString("0.0") +
                " light. Remaining light: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0")
            );
        }

        return true;
    }

    public void LoseLight(float amount, string sourceName)
    {
        // Penalties must be able to remove the remaining light even when the player
        // has less than the requested amount, so this uses clamping rather than the
        // normal ability-spending check.
        if (amount <= 0f)
        {
            return;
        }

        float previousLight = currentLight;

        currentLight -= amount;
        currentLight = Mathf.Clamp(currentLight, 0f, maximumLight);

        regenerationLimitWasReached = false;
        nextDebugLightValue = GetNextDebugThreshold(currentLight);

        if (!Mathf.Approximately(previousLight, currentLight))
        {
            NotifyLightChanged();
        }

        if (showDebugLogs)
        {
            Debug.Log(
                sourceName +
                " removed " +
                (previousLight - currentLight).ToString("0.0") +
                " light. Remaining light: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0")
            );
        }
    }

    public void RestoreLight(float amount, string sourceName)
    {
        // Restoration is kept in one place so checkpoints, pickups and future
        // environmental light sources cannot exceed the configured maximum.
        if (amount <= 0f)
        {
            return;
        }

        float previousLight = currentLight;

        currentLight += amount;
        currentLight = Mathf.Clamp(currentLight, 0f, maximumLight);

        if (Mathf.Approximately(previousLight, currentLight))
        {
            return;
        }

        regenerationLimitWasReached = false;
        nextDebugLightValue = GetNextDebugThreshold(currentLight);

        NotifyLightChanged();

        if (showDebugLogs)
        {
            Debug.Log(
                sourceName +
                " restored light. Current light: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0")
            );
        }
    }

    public void RestoreFullLight(string sourceName)
    {
        // Full restoration will later be called by checkpoints and respawning.
        currentLight = maximumLight;

        wasRegenerating = false;
        regenerationLimitWasReached = true;
        nextDebugLightValue = GetNextDebugThreshold(currentLight);

        NotifyLightChanged();

        if (showDebugLogs)
        {
            Debug.Log(
                sourceName +
                " restored light to full: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0")
            );
        }
    }

    public float GetCurrentLight()
    {
        return currentLight;
    }

    public float GetMaximumLight()
    {
        return maximumLight;
    }

    public float GetLightPercentage()
    {
        if (maximumLight <= 0f)
        {
            return 0f;
        }

        return currentLight / maximumLight;
    }

    public float GetMovementRegenerationLimit()
    {
        return maximumLight * movementRegenerationLimitPercentage;
    }

    [ContextMenu("Test Spend 60 Light")]
    private void TestSpendLight()
    {
        // This Inspector command lowers the resource so movement regeneration
        // can be tested without repeatedly using an ability.
        TrySpendLight(60f, "Inspector test");
    }

    [ContextMenu("Test Restore Full Light")]
    private void TestRestoreFullLight()
    {
        RestoreFullLight("Inspector test");
    }

    private void NotifyLightChanged()
    {
        OnLightChanged?.Invoke(currentLight, maximumLight);
    }

    private void PrintRegenerationProgress()
    {
        // Logging only at intervals keeps the Console readable while still showing
        // that continuous regeneration is updating correctly.
        if (!showDebugLogs || debugLightInterval <= 0f)
        {
            return;
        }

        if (currentLight < nextDebugLightValue)
        {
            return;
        }

        Debug.Log(
            "Light regenerated to: " +
            currentLight.ToString("0.0") +
            " / " +
            maximumLight.ToString("0.0")
        );

        nextDebugLightValue += debugLightInterval;
    }

    private void StopRegenerationDebug(string reason)
    {
        wasRegenerating = false;

        if (showDebugLogs)
        {
            Debug.Log(
                reason +
                " Current light: " +
                currentLight.ToString("0.0")
            );
        }
    }

    private float GetNextDebugThreshold(float value)
    {
        if (debugLightInterval <= 0f)
        {
            return value;
        }

        return
            Mathf.Floor(value / debugLightInterval) *
            debugLightInterval +
            debugLightInterval;
    }
}