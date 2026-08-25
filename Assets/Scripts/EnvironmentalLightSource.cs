using System.Collections;
using UnityEngine;

public class EnvironmentalLightSource : MonoBehaviour
{
    [Header("Light Restoration")]

    // This controls how much Light the player gains when the source is collected.
    // Keeping it editable means different areas can later use stronger or weaker
    // Light sources without needing separate scripts.
    [SerializeField] private float lightRestoreAmount = 25f;

    [Header("Respawn")]

    // The source temporarily depletes after collection instead of disappearing
    // permanently, allowing the player to recover resources during repeated attempts.
    [SerializeField] private float respawnDelay = 5f;

    [Header("Visuals")]

    // The available visual contains the glowing orb that floats and pulses.
    // It is disabled while the source is depleted.
    [SerializeField] private GameObject availableVisual;

    // The depleted visual is optional. It can be used later for a faint shell,
    // dim remnant, or other indicator that the Light source will eventually return.
    [SerializeField] private GameObject depletedVisual;

    [Header("Floating")]

    // A small vertical movement helps the orb read as a collectible rather than
    // a grounded shrine or progression object.
    [SerializeField] private float floatDistance = 0.15f;

    [SerializeField] private float floatSpeed = 1.5f;

    [Header("Pulse")]

    // The pulse briefly changes the orb's scale to make it feel alive without
    // relying on the persistent shrine-like effects used by checkpoints.
    [SerializeField] private float pulseScaleMultiplier = 1.15f;

    [SerializeField] private float pulseDuration = 0.35f;

    // Randomising the delay keeps the orb from pulsing on a perfectly mechanical
    // rhythm, which helps it feel more like part of the environment.
    [SerializeField] private float minimumPulseInterval = 2.5f;

    [SerializeField] private float maximumPulseInterval = 4f;

    [Header("Debug")]

    [SerializeField] private bool showDebugLogs = true;

    private bool isAvailable = true;

    private Vector3 availableVisualStartLocalPosition;
    private Vector3 availableVisualStartLocalScale;

    private float floatTimer;

    private Coroutine respawnCoroutine;
    private Coroutine pulseCoroutine;

    private void Awake()
    {
        // Clamp Inspector values so accidental negative settings cannot create
        // invalid restoration, timing, floating, or scaling behaviour.
        lightRestoreAmount =
            Mathf.Max(
                0f,
                lightRestoreAmount
            );

        respawnDelay =
            Mathf.Max(
                0f,
                respawnDelay
            );

        floatDistance =
            Mathf.Max(
                0f,
                floatDistance
            );

        floatSpeed =
            Mathf.Max(
                0f,
                floatSpeed
            );

        pulseScaleMultiplier =
            Mathf.Max(
                1f,
                pulseScaleMultiplier
            );

        pulseDuration =
            Mathf.Max(
                0.01f,
                pulseDuration
            );

        minimumPulseInterval =
            Mathf.Max(
                0f,
                minimumPulseInterval
            );

        maximumPulseInterval =
            Mathf.Max(
                minimumPulseInterval,
                maximumPulseInterval
            );

        if (availableVisual != null)
        {
            // These starting values are stored so the bobbing and pulsing remain
            // relative to the artwork's original position and scale.
            availableVisualStartLocalPosition =
                availableVisual.transform.localPosition;

            availableVisualStartLocalScale =
                availableVisual.transform.localScale;
        }

        SetAvailableState(
            true
        );
    }

    private void Start()
    {
        // The pulse loop begins after Awake has recorded the original visual scale.
        // It continues running while the source exists, but only performs pulses
        // while the source is currently available.
        pulseCoroutine =
            StartCoroutine(
                PulseLoop()
            );
    }

    private void Update()
    {
        if (
            !isAvailable ||
            availableVisual == null
        )
        {
            return;
        }

        UpdateFloating();
    }

    private void OnTriggerEnter2D(
        Collider2D collision
    )
    {
        if (!isAvailable)
        {
            return;
        }

        // Only the Player can consume this source. Other trigger objects,
        // projectiles, or moving platforms should not affect its availability.
        if (!collision.CompareTag("Player"))
        {
            return;
        }

        PlayerLightResource playerLightResource =
            collision.GetComponent<PlayerLightResource>();

        if (playerLightResource == null)
        {
            // Player components may exist on a parent object when the trigger
            // collider belongs to a child, so this fallback supports that setup.
            playerLightResource =
                collision.GetComponentInParent<PlayerLightResource>();
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "EnvironmentalLightSource touched an object tagged Player, " +
                "but PlayerLightResource could not be found."
            );

            return;
        }

        float currentLight =
            playerLightResource.GetCurrentLight();

        float maximumLight =
            playerLightResource.GetMaximumLight();

        // The source remains available if the player is already full. This prevents
        // players from accidentally wasting a renewable pickup they did not need.
        if (currentLight >= maximumLight - 0.001f)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    gameObject.name +
                    " was not consumed because the player's Light is already full."
                );
            }

            return;
        }

        playerLightResource.RestoreLight(
            lightRestoreAmount,
            "Environmental Light Source " +
            gameObject.name
        );

        ConsumeSource();
    }

    private void ConsumeSource()
    {
        isAvailable =
            false;

        SetAvailableState(
            false
        );

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " was consumed and will return after " +
                respawnDelay.ToString("0.00") +
                " seconds."
            );
        }

        if (respawnCoroutine != null)
        {
            StopCoroutine(
                respawnCoroutine
            );
        }

        respawnCoroutine =
            StartCoroutine(
                RespawnRoutine()
            );
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(
            respawnDelay
        );

        SetAvailableState(
            true
        );

        respawnCoroutine =
            null;

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " has regenerated and can be collected again."
            );
        }
    }

    private void SetAvailableState(
        bool available
    )
    {
        isAvailable =
            available;

        if (availableVisual != null)
        {
            availableVisual.SetActive(
                available
            );

            if (available)
            {
                // Restoring the original transform prevents a previous pulse or
                // floating frame from affecting how the orb looks when it respawns.
                availableVisual.transform.localPosition =
                    availableVisualStartLocalPosition;

                availableVisual.transform.localScale =
                    availableVisualStartLocalScale;

                floatTimer =
                    0f;
            }
        }

        if (depletedVisual != null)
        {
            depletedVisual.SetActive(
                !available
            );
        }
    }

    private void UpdateFloating()
    {
        floatTimer +=
            Time.deltaTime *
            floatSpeed;

        // Sine movement gives the orb a smooth vertical loop without changing
        // the parent object's trigger position or gameplay location.
        float verticalOffset =
            Mathf.Sin(
                floatTimer
            ) *
            floatDistance;

        Vector3 floatingPosition =
            availableVisualStartLocalPosition;

        floatingPosition.y +=
            verticalOffset;

        availableVisual.transform.localPosition =
            floatingPosition;
    }

    private IEnumerator PulseLoop()
    {
        while (true)
        {
            float pulseWait =
                Random.Range(
                    minimumPulseInterval,
                    maximumPulseInterval
                );

            yield return new WaitForSeconds(
                pulseWait
            );

            if (
                !isAvailable ||
                availableVisual == null
            )
            {
                continue;
            }

            yield return StartCoroutine(
                PulseOnce()
            );
        }
    }

    private IEnumerator PulseOnce()
    {
        Vector3 normalScale =
            availableVisualStartLocalScale;

        Vector3 enlargedScale =
            normalScale *
            pulseScaleMultiplier;

        float halfDuration =
            pulseDuration *
            0.5f;

        float timer =
            0f;

        // The first half of the pulse grows the orb smoothly instead of snapping
        // to a larger size, keeping the effect subtle and readable.
        while (timer < halfDuration)
        {
            if (
                !isAvailable ||
                availableVisual == null
            )
            {
                yield break;
            }

            float progress =
                halfDuration <= 0f
                    ? 1f
                    : timer /
                      halfDuration;

            availableVisual.transform.localScale =
                Vector3.Lerp(
                    normalScale,
                    enlargedScale,
                    progress
                );

            timer +=
                Time.deltaTime;

            yield return null;
        }

        timer =
            0f;

        // The second half returns to the original scale so each pulse is temporary
        // and does not gradually alter the collectible's visual size.
        while (timer < halfDuration)
        {
            if (
                !isAvailable ||
                availableVisual == null
            )
            {
                yield break;
            }

            float progress =
                halfDuration <= 0f
                    ? 1f
                    : timer /
                      halfDuration;

            availableVisual.transform.localScale =
                Vector3.Lerp(
                    enlargedScale,
                    normalScale,
                    progress
                );

            timer +=
                Time.deltaTime;

            yield return null;
        }

        if (
            isAvailable &&
            availableVisual != null
        )
        {
            availableVisual.transform.localScale =
                normalScale;
        }
    }

    private void OnDisable()
    {
        // Stopping active routines prevents a disabled or destroyed pickup from
        // continuing delayed respawn or animation behaviour unexpectedly.
        if (respawnCoroutine != null)
        {
            StopCoroutine(
                respawnCoroutine
            );

            respawnCoroutine =
                null;
        }

        if (pulseCoroutine != null)
        {
            StopCoroutine(
                pulseCoroutine
            );

            pulseCoroutine =
                null;
        }
    }
}
