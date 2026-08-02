using UnityEngine;

public class PlayerLightVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLightResource playerLightResource;
    [SerializeField] private SpriteRenderer catMothSpriteRenderer;

    [Header("Brightness")]
    [SerializeField, Range(0f, 1f)]
    private float minimumBrightness = 0.05f;

    [SerializeField, Range(0f, 1f)]
    private float maximumBrightness = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private float currentBrightness = 1f;

    private void Awake()
    {
        // The resource system is expected to remain on the parent Player object.
        // This fallback keeps the visual feedback working if the Inspector reference is lost.
        if (playerLightResource == null)
        {
            playerLightResource = GetComponent<PlayerLightResource>();
        }

        // CatMoth's visible SpriteRenderer is expected to be on the CatMoth Visual child.
        // The Inspector reference should still be assigned manually to avoid selecting another renderer.
        if (catMothSpriteRenderer == null)
        {
            catMothSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        minimumBrightness = Mathf.Clamp01(minimumBrightness);

        maximumBrightness = Mathf.Clamp(
            maximumBrightness,
            minimumBrightness,
            1f
        );

        if (playerLightResource == null)
        {
            Debug.LogError(
                "PlayerLightVisual could not find PlayerLightResource."
            );
        }

        if (catMothSpriteRenderer == null)
        {
            Debug.LogError(
                "PlayerLightVisual could not find CatMoth's SpriteRenderer."
            );
        }
    }

    private void Start()
    {
        // Start is used because PlayerLightResource initialises its current value in Awake.
        // This ensures CatMoth begins with the correct brightness for the actual starting light.
        RefreshBrightnessFromResource();
    }

    private void OnEnable()
    {
        if (playerLightResource != null)
        {
            // The event updates the target brightness whenever light is gained, spent, restored, or refunded.
            playerLightResource.OnLightChanged += HandleLightChanged;
        }
    }

    private void OnDisable()
    {
        if (playerLightResource != null)
        {
            // Removing the subscription prevents a disabled visual component from receiving resource updates.
            playerLightResource.OnLightChanged -= HandleLightChanged;
        }
    }

    private void LateUpdate()
    {
        // The colour is reapplied after animation updates so the current light value
        // remains the final visual state rendered for CatMoth each frame.
        ApplyBrightness();
    }

    private void HandleLightChanged(
        float currentLight,
        float maximumLight
    )
    {
        CalculateBrightness(
            currentLight,
            maximumLight
        );
    }

    private void RefreshBrightnessFromResource()
    {
        if (playerLightResource == null)
        {
            return;
        }

        CalculateBrightness(
            playerLightResource.GetCurrentLight(),
            playerLightResource.GetMaximumLight()
        );

        ApplyBrightness();
    }

    private void CalculateBrightness(
        float currentLight,
        float maximumLight
    )
    {
        if (maximumLight <= 0f)
        {
            currentBrightness = minimumBrightness;
            return;
        }

        float lightPercentage = Mathf.Clamp01(
            currentLight / maximumLight
        );

        // The full 0–100 light range is converted into the configured brightness range.
        // CatMoth remains barely visible at zero light and reaches normal white at full light.
        currentBrightness = Mathf.Lerp(
            minimumBrightness,
            maximumBrightness,
            lightPercentage
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "CatMoth brightness updated. Light: " +
                currentLight.ToString("0.0") +
                " / " +
                maximumLight.ToString("0.0") +
                " | Brightness: " +
                currentBrightness.ToString("0.00")
            );
        }
    }

    private void ApplyBrightness()
    {
        if (catMothSpriteRenderer == null)
        {
            return;
        }

        // Direct greyscale tinting uses the same SpriteRenderer colour behaviour
        // confirmed by the successful red runtime test.
        catMothSpriteRenderer.color = new Color(
            currentBrightness,
            currentBrightness,
            currentBrightness,
            1f
        );
    }

    [ContextMenu("Refresh Light Brightness")]
    private void RefreshLightBrightness()
    {
        // This command allows the current light value to be reapplied while tuning
        // the brightness range during Play Mode.
        RefreshBrightnessFromResource();
    }
}