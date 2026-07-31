using UnityEngine;
using System.Collections;

public class LightBurstController : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private float burstDuration = 2f;

    [Header("Light Resource Cost")]
    [SerializeField] private float lightCost = 25f;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    [Header("Darkness Dispel")]
    [SerializeField] private float burstDispelRadius = 3f;
    [SerializeField] private LayerMask darknessLayer;
    [SerializeField] private LayerMask GroundLayer;

    private bool isBurstActive = false;
    private bool isOnCooldown = false;
    private Coroutine burstCoroutine;
    private Coroutine cooldownCoroutine;

    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;

    private void Awake()
    {
        // The burst checks the player's unlock state so this ability can be introduced later in the level
        // rather than being available from the beginning.
        abilityUnlocks = GetComponent<PlayerAbilityUnlocks>();

        // Light Burst must request energy from the central resource component.
        // Keeping light spending outside this script prevents each ability from managing separate values.
        playerLightResource = GetComponent<PlayerLightResource>();

        if (playerLightResource == null)
        {
            Debug.LogError(
                "LightBurstController could not find PlayerLightResource on " +
                gameObject.name +
                ". Light Burst will not activate until the component is added."
            );
        }

        // The visual starts hidden because it should only appear when the ability is actually active.
        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        Debug.Log(
            "LightBurstController initialised. Burst light cost: " +
            lightCost.ToString("0.0")
        );
    }

    public bool IsBurstActive()
    {
        return isBurstActive;
    }

    public bool IsOnCooldown()
    {
        return isOnCooldown;
    }

    public void ActivateBurst()
    {
        // The player should not be able to use Light Burst until they have found the unlock object.
        // This supports the team's progression idea where abilities are earned during the level.
        if (abilityUnlocks != null && !abilityUnlocks.HasLightBurst())
        {
            Debug.Log("Light Burst is locked");
            return;
        }

        // This prevents the player from spamming burst or restarting it while it is already active.
        // Light is not spent when the input is rejected for either of these reasons.
        if (isBurstActive)
        {
            Debug.Log("Light Burst could not activate because it is already active.");
            return;
        }

        if (isOnCooldown)
        {
            Debug.Log("Light Burst could not activate because it is on cooldown.");
            return;
        }

        // The resource check happens before starting either coroutine.
        // This ensures a failed activation does not display the burst or begin its cooldown.
        if (playerLightResource == null)
        {
            Debug.LogError(
                "Light Burst could not activate because PlayerLightResource is missing."
            );
            return;
        }

        if (!playerLightResource.TrySpendLight(lightCost, "Light Burst"))
        {
            Debug.Log(
                "Light Burst activation was blocked because the player did not have enough light."
            );
            return;
        }

        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
        }

        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
        }

        burstCoroutine = StartCoroutine(BurstRoutine());
        cooldownCoroutine = StartCoroutine(CooldownRoutine());

        Debug.Log(
            "Light Burst successfully activated after spending " +
            lightCost.ToString("0.0") +
            " light."
        );
    }

    private IEnumerator BurstRoutine()
    {
        isBurstActive = true;

        if (burstVisual != null)
        {
            burstVisual.SetActive(true);
        }

        Debug.Log("Light burst active");

        float timer = 0f;
        float dispelCheckInterval = 0.1f;

        // The burst keeps checking for darkness while active because the player can move during the ability.
        // This makes the burst feel like a moving safe/light area instead of a one-frame effect.
        while (timer < burstDuration)
        {
            DispelDarknessInRadius();
            CheckLightplatformInBurst();

            timer += dispelCheckInterval;
            yield return new WaitForSeconds(dispelCheckInterval);
        }

        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        burstCoroutine = null;
        Debug.Log("Light burst ended");
    }

    private void DispelDarknessInRadius()
    {
        // A circular overlap fits the design of Light Burst as a close-range protective ability.
        // Unlike the beam, this is meant to affect the area around the player rather than a direction.
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            burstDispelRadius,
            darknessLayer
        );

        foreach (Collider2D hit in hits)
        {
            DarknessZone darknessZone = hit.GetComponentInParent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
            }
        }

        Debug.Log("Light burst dispelled darkness zones: " + hits.Length);
    }

    private IEnumerator CooldownRoutine()
    {
        // The cooldown is tied to the burst duration so the ability cannot be restarted while it is active,
        // but it still feels responsive once the burst has ended.
        isOnCooldown = true;
        Debug.Log("Light burst cooldown started");

        yield return new WaitForSeconds(burstDuration);

        isOnCooldown = false;
        cooldownCoroutine = null;
        Debug.Log("Light burst cooldown ended");
    }

    private void OnDrawGizmosSelected()
    {
        // This helps tune the burst radius in the editor without needing to guess the gameplay range.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, burstDispelRadius);
    }

    public float GetBurstDispelRadius()
    {
        // Darkness zones use this to check whether they are still being overlapped by an active burst.
        // This helps prevent darkness from reforming while the burst is still covering it.
        return burstDispelRadius;
    }

    private void CheckLightplatformInBurst()
    {
        Debug.Log("Checking light platforms");

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            burstDispelRadius
        );

        Debug.Log("Ground hit count: " + hits.Length);

        foreach (Collider2D hit in hits)
        {
            Debug.Log("Hit object: " + hit.gameObject.name);

            appear_and_disappeear_by_burst lightPlatform =
                hit.GetComponentInParent<appear_and_disappeear_by_burst>();

            if (lightPlatform != null)
            {
                Debug.Log(
                    "Found invisible platform: " +
                    lightPlatform.gameObject.name
                );

                lightPlatform.ShowPlatform();
            }
        }
    }
}