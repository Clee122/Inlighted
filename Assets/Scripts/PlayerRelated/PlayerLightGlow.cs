using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlayerLightGlow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLightResource playerLightResource;
    [SerializeField] private Light2D playerLight;

    [Header("Light Intensity")]
    [SerializeField] private float zeroLightIntensity = 0f;
    [SerializeField] private float halfLightIntensity = 0.6f;
    [SerializeField] private float fullLightIntensity = 1.2f;

    [Header("Light Radius")]
    [SerializeField] private float zeroLightRadius = 0.5f;
    [SerializeField] private float halfLightRadius = 2f;
    [SerializeField] private float fullLightRadius = 3.5f;

    [Header("Smoothing")]
    [SerializeField] private float visualChangeSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private float targetIntensity;
    private float targetRadius;

    private void Reset()
    {
        // These components normally exist on the Player and its light child, so
        // automatically finding them reduces setup mistakes when the script is added.
        playerLightResource = GetComponent<PlayerLightResource>();
        playerLight = GetComponentInChildren<Light2D>();
    }

    private void Awake()
    {
        // These fallbacks protect the visual effect if prefab references are lost
        // while the Player or scene is being edited.
        if (playerLightResource == null)
        {
            playerLightResource = GetComponent<PlayerLightResource>();
        }

        if (playerLight == null)
        {
            playerLight = GetComponentInChildren<Light2D>();
        }

        zeroLightIntensity = Mathf.Max(0f, zeroLightIntensity);
        halfLightIntensity = Mathf.Max(
            zeroLightIntensity,
            halfLightIntensity
        );
        fullLightIntensity = Mathf.Max(
            halfLightIntensity,
            fullLightIntensity
        );

        zeroLightRadius = Mathf.Max(0f, zeroLightRadius);
        halfLightRadius = Mathf.Max(
            zeroLightRadius,
            halfLightRadius
        );
        fullLightRadius = Mathf.Max(
            halfLightRadius,
            fullLightRadius
        );

        visualChangeSpeed = Mathf.Max(
            0f,
            visualChangeSpeed
        );

        if (playerLightResource == null)
        {
            Debug.LogError(
                "PlayerLightGlow could not find PlayerLightResource."
            );
        }

        if (playerLight == null)
        {
            Debug.LogError(
                "PlayerLightGlow could not find a child Light2D."
            );
        }
    }

    private void Start()
    {
        // PlayerLightResource establishes its starting value during Awake, so
        // applying the glow in Start uses the correct gameplay resource value.
        RefreshGlowFromResource(true);
    }

    private void OnEnable()
    {
        if (playerLightResource != null)
        {
            // The glow target updates whenever light is gained, spent, restored,
            // channelled, or refunded.
            playerLightResource.OnLightChanged += HandleLightChanged;
        }
    }

    private void OnDisable()
    {
        if (playerLightResource != null)
        {
            // Removing the subscription prevents a disabled visual component from
            // continuing to react to resource changes.
            playerLightResource.OnLightChanged -= HandleLightChanged;
        }
    }

    private void Update()
    {
        if (playerLight == null)
        {
            return;
        }

        // Smoothing prevents light gain and spending from making the environment
        // flash abruptly between values.
        playerLight.intensity = Mathf.MoveTowards(
            playerLight.intensity,
            targetIntensity,
            visualChangeSpeed * Time.deltaTime
        );

        playerLight.pointLightOuterRadius = Mathf.MoveTowards(
            playerLight.pointLightOuterRadius,
            targetRadius,
            visualChangeSpeed * Time.deltaTime
        );
    }

    private void HandleLightChanged(
        float currentLight,
        float maximumLight
    )
    {
        CalculateGlowTargets(
            currentLight,
            maximumLight
        );
    }

    private void RefreshGlowFromResource(
        bool applyImmediately
    )
    {
        if (playerLightResource == null)
        {
            return;
        }

        CalculateGlowTargets(
            playerLightResource.GetCurrentLight(),
            playerLightResource.GetMaximumLight()
        );

        if (
            applyImmediately &&
            playerLight != null
        )
        {
            // Starting values are applied immediately so the light does not visibly
            // fade down from its Inspector value when the scene begins.
            playerLight.intensity = targetIntensity;
            playerLight.pointLightOuterRadius = targetRadius;
        }
    }

    private void CalculateGlowTargets(
        float currentLight,
        float maximumLight
    )
    {
        if (maximumLight <= 0f)
        {
            targetIntensity = zeroLightIntensity;
            targetRadius = zeroLightRadius;
            return;
        }

        float lightPercentage = Mathf.Clamp01(
            currentLight / maximumLight
        );

        if (lightPercentage <= 0.5f)
        {
            // The renewable half of the resource builds a moderate environmental
            // light around CatMoth.
            float firstHalfProgress =
                lightPercentage / 0.5f;

            targetIntensity = Mathf.Lerp(
                zeroLightIntensity,
                halfLightIntensity,
                firstHalfProgress
            );

            targetRadius = Mathf.Lerp(
                zeroLightRadius,
                halfLightRadius,
                firstHalfProgress
            );
        }
        else
        {
            // Light above the normal movement-regeneration limit strengthens and
            // widens the glow, making excess stored energy visually distinctive.
            float secondHalfProgress =
                (lightPercentage - 0.5f) / 0.5f;

            targetIntensity = Mathf.Lerp(
                halfLightIntensity,
                fullLightIntensity,
                secondHalfProgress
            );

            targetRadius = Mathf.Lerp(
                halfLightRadius,
                fullLightRadius,
                secondHalfProgress
            );
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Player glow updated. Light: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0") +
                " | Target intensity: " +
                targetIntensity.ToString("0.00") +
                " | Target radius: " +
                targetRadius.ToString("0.00")
            );
        }
    }

    [ContextMenu("Refresh Player Glow")]
    private void RefreshPlayerGlow()
    {
        // This Inspector command reapplies the current resource value while tuning
        // glow intensity and radius during Play Mode.
        RefreshGlowFromResource(true);
    }
}