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
    private PlayerLightChannel playerLightChannel;

    private void Awake()
    {
        abilityUnlocks =
            GetComponent<PlayerAbilityUnlocks>();

        playerLightResource =
            GetComponent<PlayerLightResource>();

        // Burst must check channeling before spending light or displaying visuals.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        if (playerLightResource == null)
        {
            Debug.LogError(
                "LightBurstController could not find PlayerLightResource on " +
                gameObject.name +
                ". Light Burst will not activate until the component is added."
            );
        }

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
        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            // Burst input is ignored rather than cancelling channeling. This keeps
            // channeling as a committed state where abilities cannot be used.
            Debug.Log(
                "Light Burst was blocked because the player is channeling."
            );

            return;
        }

        if (
            abilityUnlocks != null &&
            !abilityUnlocks.HasLightBurst()
        )
        {
            Debug.Log(
                "Light Burst is locked"
            );

            return;
        }

        if (isBurstActive)
        {
            Debug.Log(
                "Light Burst could not activate because it is already active."
            );

            return;
        }

        if (isOnCooldown)
        {
            Debug.Log(
                "Light Burst could not activate because it is on cooldown."
            );

            return;
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "Light Burst could not activate because PlayerLightResource is missing."
            );

            return;
        }

        if (
            !playerLightResource.TrySpendLight(
                lightCost,
                "Light Burst"
            )
        )
        {
            Debug.Log(
                "Light Burst activation was blocked because the player did not have enough light."
            );

            return;
        }

        if (burstCoroutine != null)
        {
            StopCoroutine(
                burstCoroutine
            );
        }

        if (cooldownCoroutine != null)
        {
            StopCoroutine(
                cooldownCoroutine
            );
        }

        burstCoroutine =
            StartCoroutine(
                BurstRoutine()
            );

        cooldownCoroutine =
            StartCoroutine(
                CooldownRoutine()
            );

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

        Debug.Log(
            "Light burst active"
        );

        float timer = 0f;
        float dispelCheckInterval = 0.1f;

        while (timer < burstDuration)
        {
            DispelDarknessInRadius();
            CheckLightplatformInBurst();

            timer +=
                dispelCheckInterval;

            yield return
                new WaitForSeconds(
                    dispelCheckInterval
                );
        }

        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        burstCoroutine = null;

        Debug.Log(
            "Light burst ended"
        );
    }

    private void DispelDarknessInRadius()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                burstDispelRadius,
                darknessLayer
            );

        foreach (Collider2D hit in hits)
        {
            DarknessZone darknessZone =
                hit.GetComponentInParent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
            }
        }

        Debug.Log(
            "Light burst dispelled darkness zones: " +
            hits.Length
        );
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;

        Debug.Log(
            "Light burst cooldown started"
        );

        yield return
            new WaitForSeconds(
                burstDuration
            );

        isOnCooldown = false;
        cooldownCoroutine = null;

        Debug.Log(
            "Light burst cooldown ended"
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            burstDispelRadius
        );
    }

    public float GetBurstDispelRadius()
    {
        return burstDispelRadius;
    }

    private void CheckLightplatformInBurst()
    {
        Debug.Log(
            "Checking light platforms"
        );

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                burstDispelRadius
            );

        Debug.Log(
            "Ground hit count: " +
            hits.Length
        );

        foreach (Collider2D hit in hits)
        {
            Debug.Log(
                "Hit object: " +
                hit.gameObject.name
            );

            appear_and_disappeear_by_burst lightPlatform =
                hit.GetComponentInParent
                <appear_and_disappeear_by_burst>();

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