using UnityEngine;
using System.Collections;

public class LightBurstController : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private float burstDuration = 2f;

    // The gameplay radius expands separately from the total Burst duration so
    // the hitbox can match the visible VFX even if the particles remain active
    // after they have already reached their maximum size.
    [SerializeField] private float burstExpansionDuration = 1f;

    // The Burst begins close to the player instead of immediately affecting the
    // entire maximum radius.
    [SerializeField] private float startingBurstRadius = 0.2f;

    // This curve controls how quickly the gameplay radius grows over the
    // expansion period so it can be matched closely to the Burst VFX.
    [SerializeField]
    private AnimationCurve burstExpansionCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0.5f),
            new Keyframe(0.5f, 0.8f),
            new Keyframe(0.75f, 0.95f),
            new Keyframe(1f, 1f)
        );

    [Header("Light Resource Cost")]
    [SerializeField] private float lightCost = 25f;

    [Header("Audio")]
    // Burst audio is assigned independently from the VFX so the final sound
    // can be added later without changing the ability's gameplay behaviour.
    [SerializeField] private AudioClip burstSound;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    // The wall-aware radial mesh is controlled separately from the original
    // Burst VFX so it can be enabled only while the ability is active.
    [SerializeField] private GameObject burstWallVisual;

    [Header("Reveal Mask")]
    [SerializeField] private GameObject revealMask;

    [Header("Darkness Dispel")]
    [SerializeField] private float burstDispelRadius = 3f;
    [SerializeField] private LayerMask darknessLayer;
    [SerializeField] private LayerMask GroundLayer;

    // Walls are checked separately after targets are found inside the expanding
    // radius so Burst cannot affect objects through solid level geometry.
    [SerializeField] private LayerMask wallLayer;

    [Header("Debug")]
    [SerializeField] private bool showBurstDebug = true;
    [SerializeField] private int debugCircleSegments = 48;

    private bool isBurstActive = false;
    private bool isOnCooldown = false;

    // This stores the live gameplay radius reached by the expanding Burst.
    private float currentBurstRadius = 0f;

    private Coroutine burstCoroutine;
    private Coroutine cooldownCoroutine;

    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;

    // Existing Burst effects are allowed to continue through a dash, but this
    // reference temporarily blocks beginning a new Burst during the dash itself.
    private PlayerDash playerDash;

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

        // New Burst activation during dash remains disabled for this prototype.
        // This does not affect a Burst that was already active before dashing.
        playerDash =
            GetComponent<PlayerDash>();

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

        // The wall-aware radial mesh also begins hidden so geometry from a
        // previous Burst cannot remain visible before the ability activates.
        if (burstWallVisual != null)
        {
            burstWallVisual.SetActive(false);
        }

        // The reveal mask must also begin disabled so it does not reveal hidden
        // areas before the player activates Light Burst.
        TurnMaskOff();

        currentBurstRadius =
            startingBurstRadius;

        Debug.Log(
            "LightBurstController initialised. Burst light cost: " +
            lightCost.ToString("0.0")
        );
    }

    private void Update()
    {
        // Debug lines are drawn continuously during Play Mode while the Burst is
        // active so the expanding gameplay radius is easier to compare with VFX.
        if (
            showBurstDebug &&
            isBurstActive
        )
        {
            DrawDebugBurstCircle();
        }
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

        if (
            playerDash != null &&
            playerDash.IsDashing()
        )
        {
            // Casting Burst during dash remains disabled while this interaction
            // is still being tested. A Burst started before dash is not cancelled.
            Debug.Log(
                "Light Burst activation was blocked because the player is dashing."
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

        // Burst audio is triggered only after every gameplay requirement has
        // succeeded and the light cost has been paid. This prevents blocked
        // Burst attempts from producing misleading activation audio.
        if (
            burstSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.PlaySFX(
                burstSound
            );
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

        // Every Burst begins from the small inner radius so gameplay starts close
        // to the player and expands outward alongside the visible VFX.
        currentBurstRadius =
            startingBurstRadius;

        if (burstVisual != null)
        {
            burstVisual.SetActive(true);
        }

        // The radial mesh becomes visible at the same moment as the gameplay
        // Burst so its wall-aware shape can expand alongside the ability.
        if (burstWallVisual != null)
        {
            burstWallVisual.SetActive(true);
        }

        // The reveal mask is enabled for the same period as the Burst so the
        // hidden-space effect remains synchronised with the visible ability.
        TurnMaskOn();

        Debug.Log(
            "Light burst active."
        );

        float timer = 0f;
        float dispelCheckInterval = 0.05f;

        while (timer < burstDuration)
        {
            // Expansion uses its own timing value because the VFX can reach full
            // size before the overall Burst active period has finished.
            float normalisedExpansionTime =
                burstExpansionDuration > 0f
                    ? Mathf.Clamp01(
                        timer / burstExpansionDuration
                    )
                    : 1f;

            // The AnimationCurve shapes how quickly the radius grows so the
            // gameplay timing can follow the visible Burst more closely.
            float expansionAmount =
                Mathf.Clamp01(
                    burstExpansionCurve.Evaluate(
                        normalisedExpansionTime
                    )
                );

            currentBurstRadius =
                Mathf.Lerp(
                    startingBurstRadius,
                    burstDispelRadius,
                    expansionAmount
                );

            DispelDarknessInRadius();
            CheckLightPlatformInBurst();

            // This temporary log helps verify the radius while testing.
            Debug.Log(
                "Light Burst expansion: " +
                (normalisedExpansionTime * 100f).ToString("0") +
                "% | Radius: " +
                currentBurstRadius.ToString("0.00")
            );

            timer +=
                dispelCheckInterval;

            yield return new WaitForSeconds(
                dispelCheckInterval
            );
        }

        // The final check guarantees the gameplay Burst reaches the full range
        // even if timing differences prevent the loop landing exactly on 100%.
        currentBurstRadius =
            burstDispelRadius;

        DispelDarknessInRadius();
        CheckLightPlatformInBurst();

        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        // The wall-aware visual is hidden when Burst ends so its clipped ring
        // cannot remain visible after gameplay detection has stopped.
        if (burstWallVisual != null)
        {
            burstWallVisual.SetActive(false);
        }

        // The mask must be disabled when Burst ends so hidden areas do not remain
        // revealed after the ability's active period.
        TurnMaskOff();

        currentBurstRadius =
            startingBurstRadius;

        burstCoroutine = null;

        Debug.Log(
            "Light burst ended."
        );
    }

    private void DispelDarknessInRadius()
    {
        // Burst first finds darkness inside the current expanding radius. Each
        // target then requires a clear path so walls block the gameplay effect.
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                currentBurstRadius,
                darknessLayer
            );

        int dispelledCount = 0;
        int blockedCount = 0;

        foreach (Collider2D hit in hits)
        {
            if (!HasClearBurstPath(hit))
            {
                blockedCount++;

                continue;
            }

            DarknessZone darknessZone =
                hit.GetComponentInParent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
                dispelledCount++;
            }
        }

        // This temporary log confirms whether nearby darkness is being reached
        // normally or rejected because a wall sits between it and the player.
        Debug.Log(
            "Light Burst darkness check. Dispelled: " +
            dispelledCount +
            ", blocked by walls: " +
            blockedCount
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
        // The yellow circle shows the maximum configured Burst range.
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            burstDispelRadius
        );

        // The cyan circle shows the current gameplay radius when the Player is
        // selected in the Scene view.
        Gizmos.color =
            Color.cyan;

        float radiusToDraw =
            Application.isPlaying
                ? currentBurstRadius
                : startingBurstRadius;

        Gizmos.DrawWireSphere(
            transform.position,
            radiusToDraw
        );
    }

    private void DrawDebugBurstCircle()
    {
        if (debugCircleSegments < 3)
        {
            return;
        }

        // Debug.DrawLine creates a temporary circle from short line segments so
        // the live radius is easier to inspect during Play Mode than Gizmos alone.
        Vector3 centre =
            transform.position;

        Vector3 previousPoint =
            centre +
            Vector3.right *
            currentBurstRadius;

        for (
            int i = 1;
            i <= debugCircleSegments;
            i++
        )
        {
            float angle =
                (
                    (float)i /
                    debugCircleSegments
                ) *
                Mathf.PI *
                2f;

            Vector3 nextPoint =
                centre +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f
                ) *
                currentBurstRadius;

            Debug.DrawLine(
                previousPoint,
                nextPoint,
                Color.cyan,
                0f,
                false
            );

            previousPoint =
                nextPoint;
        }
    }

    public float GetBurstDispelRadius()
    {
        // Darkness systems can still access the maximum possible Burst range.
        return burstDispelRadius;
    }

    public float GetCurrentBurstRadius()
    {
        // Other systems can use the live radius when they need to know how far
        // the expanding Burst has currently reached.
        return currentBurstRadius;
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
        // Burst platforms use the same expanding radius and wall rule as
        // darkness so they cannot activate through solid level geometry.
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                currentBurstRadius,
                GroundLayer
            );

        foreach (Collider2D hit in hits)
        {
            if (!HasClearBurstPath(hit))
            {
                continue;
            }

            appear_and_disappeear_by_burst lightPlatform =
                hit.GetComponentInParent<appear_and_disappeear_by_burst>();

            if (lightPlatform != null)
            {
                lightPlatform.ActivatePlatform();
            }
        }
    }

    private bool HasClearBurstPath(
        Collider2D targetCollider
    )
    {
        if (targetCollider == null)
        {
            return false;
        }

        Vector2 burstOrigin =
            transform.position;

        // ClosestPoint checks the nearest part of the target collider instead
        // of always aiming at its centre, which works better for larger objects.
        Vector2 targetPoint =
            targetCollider.ClosestPoint(
                burstOrigin
            );

        Vector2 direction =
            targetPoint -
            burstOrigin;

        float distance =
            direction.magnitude;

        // A target already touching the Burst origin does not need a meaningful
        // wall check because there is effectively no space between them.
        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit2D wallHit =
            Physics2D.Raycast(
                burstOrigin,
                direction.normalized,
                distance,
                wallLayer
            );

        return wallHit.collider == null;
    }
}