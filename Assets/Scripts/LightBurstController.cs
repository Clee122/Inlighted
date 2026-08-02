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

    [Header("Reveal Mask")]
    [SerializeField] private GameObject revealMask;

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
        // The unlock system controls whether the player has earned access to
        // Light Burst before the input is allowed to activate the ability.
        abilityUnlocks =
            GetComponent<PlayerAbilityUnlocks>();

        // All Burst energy costs are handled through the shared player light
        // resource so abilities do not maintain separate energy values.
        playerLightResource =
            GetComponent<PlayerLightResource>();

        // Burst checks the channel state before spending light or displaying
        // visuals because healing and ability use must remain mutually exclusive.
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

        // Burst visuals begin disabled because they should appear only during
        // an active and successfully paid-for Burst.
        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        // The reveal mask must also begin disabled so it does not reveal hidden
        // areas before the player activates Light Burst.
        TurnMaskOff();

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
            // Burst input is ignored while channeling so the player cannot heal
            // and activate a light ability at the same time.
            Debug.Log(
                "Light Burst was blocked because the player is channeling."
            );

            return;
        }

        // The player should not be able to use Light Burst until the ability
        // has been unlocked through the intended level progression.
        if (
            abilityUnlocks != null &&
            !abilityUnlocks.HasLightBurst()
        )
        {
            Debug.Log(
                "Light Burst is locked."
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

        // Light is spent only after every other activation requirement passes.
        // This prevents rejected input from consuming the player's resource.
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

        // The reveal mask is enabled for the same period as the Burst so the
        // hidden-space effect remains synchronised with the visible ability.
        TurnMaskOn();

        Debug.Log(
            "Light burst active."
        );

        float timer = 0f;
        float dispelCheckInterval = 0.1f;

        // Darkness and Burst-activated platforms are checked repeatedly because
        // the player can move while the ability remains active.
        while (timer < burstDuration)
        {
            DispelDarknessInRadius();
            CheckLightPlatformInBurst();

            timer += dispelCheckInterval;

            yield return new WaitForSeconds(
                dispelCheckInterval
            );
        }

        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        // The mask must be disabled when Burst ends so hidden areas do not remain
        // revealed after the ability's active period.
        TurnMaskOff();

        burstCoroutine = null;

        Debug.Log(
            "Light burst ended."
        );
    }

    private void DispelDarknessInRadius()
    {
        // A circular overlap matches Burst's design as an area effect centred
        // on the player rather than a directional ability.
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
        // The cooldown begins with activation so Burst cannot be restarted while
        // its current active period is still running.
        isOnCooldown = true;

        Debug.Log(
            "Light burst cooldown started."
        );

        yield return new WaitForSeconds(
            burstDuration
        );

        isOnCooldown = false;
        cooldownCoroutine = null;

        Debug.Log(
            "Light burst cooldown ended."
        );
    }

    private void OnDrawGizmosSelected()
    {
        // The wire sphere visualises the gameplay radius used for darkness and
        // platform detection without requiring the game to be running.
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            burstDispelRadius
        );
    }

    public float GetBurstDispelRadius()
    {
        // Darkness systems use this value when checking whether an active Burst
        // still overlaps them and should prevent immediate reformation.
        return burstDispelRadius;
    }

    private void TurnMaskOn()
    {
        if (revealMask != null)
        {
            revealMask.SetActive(true);
        }
    }

    private void TurnMaskOff()
    {
        if (revealMask != null)
        {
            revealMask.SetActive(false);
        }
    }

    private void CheckLightPlatformInBurst()
    {
        // Only objects on the configured platform layer are checked so Burst does
        // not unnecessarily search every collider surrounding the player.
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                burstDispelRadius,
                GroundLayer
            );

        foreach (Collider2D hit in hits)
        {
            appear_and_disappeear_by_burst lightPlatform =
                hit.GetComponentInParent<appear_and_disappeear_by_burst>();

            if (lightPlatform != null)
            {
                lightPlatform.ActivatePlatform();
            }
        }
    }
}