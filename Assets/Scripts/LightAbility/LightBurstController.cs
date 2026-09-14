using UnityEngine;
using System.Collections;

public class LightBurstController : MonoBehaviour
{
    [Header("Burst Settings")]

    // Burst remains active long enough for the player to use the temporary
    // safe area instead of behaving like a one-frame radial attack.
    [SerializeField] private float burstDuration = 2f;

    // The radius grows during the first second so both the visible effect and
    // darkness cut-out visibly expand away from the player.
    [SerializeField] private float burstExpansionDuration = 1f;

    // Cooldown is independent from visual timing even though the current values
    // intentionally make it available again shortly after the Burst ends.
    [SerializeField] private float burstCooldownDuration = 2f;

    // Starting with a small radius prevents the darkness opening from appearing
    // instantly at full size on the first frame.
    [SerializeField] private float startingBurstRadius = 0.2f;

    // This is the same expansion curve used by the previous Burst iterations.
    // Keeping it here preserves the original expansion feel while restoring the
    // older overall ability duration.
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

    // Audio remains separate from gameplay state so future sound changes do not
    // require changing how the Burst itself functions.
    [SerializeField] private AudioClip burstSound;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    // This secondary visual is allowed to pass through normal level geometry,
    // matching the gameplay decision that Burst is radial rather than obstructed.
    [SerializeField] private GameObject burstWallVisual;

    [Header("Burst Expiry Warning")]

    // The final part of the Burst flashes so the player can anticipate when the
    // temporary light protection and darkness opening are about to disappear.
    [SerializeField] private float flickerWarningDuration = 0.5f;

    // A short interval gives a readable warning without making the visual appear
    // permanently hidden during the final portion of the Burst.
    [SerializeField] private float flickerInterval = 0.1f;

    [Header("Reveal Mask")]
    [SerializeField] private GameObject revealMask;

    [Header("Darkness Dispel")]
    [SerializeField] private float burstDispelRadius = 3f;
    [SerializeField] private LayerMask darknessLayer;
    [SerializeField] private LayerMask GroundLayer;

    [Header("Debug")]
    [SerializeField] private bool showBurstDebug = true;
    [SerializeField] private int debugCircleSegments = 48;

    private bool isBurstActive = false;
    private bool isOnCooldown = false;

    // DarknessCutoutController reads this value so its CPU-generated mask uses
    // exactly the same radius as the gameplay Burst.
    private float currentBurstRadius = 0f;

    /*
     * The darkness experiment needs a stable world position for the current
     * Burst. While the ability is active this follows the player, preserving the
     * behaviour where the Burst safe area travels with CatMoth.
     */
    private Vector2 currentBurstOrigin;

    private Coroutine burstCoroutine;
    private Coroutine cooldownCoroutine;

    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;
    private PlayerDash playerDash;
    private PlayerAnimationController playerAnimationController;

    /*
     * Renderer references are cached so the expiry warning can blink only the
     * graphics. The Burst remains logically active and continues affecting the
     * darkness mask while its visuals are temporarily hidden.
     */
    private Renderer[] burstVisualRenderers;
    private Renderer[] burstWallVisualRenderers;

    private void Awake()
    {
        // The unlock component decides whether Light Burst is currently available.
        abilityUnlocks =
            GetComponent<PlayerAbilityUnlocks>();

        // Light cost comes from the shared player resource system.
        playerLightResource =
            GetComponent<PlayerLightResource>();

        // Channeling and Burst stay mutually exclusive so the player cannot run
        // both light-resource abilities simultaneously.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        // Burst remains blocked during a dash to preserve the existing player
        // controller behaviour.
        playerDash =
            GetComponent<PlayerDash>();

        // The animation controller is notified only after Burst activation has
        // succeeded so failed inputs never play the ability animation.
        playerAnimationController =
            GetComponent<PlayerAnimationController>();

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
            burstVisualRenderers =
                burstVisual.GetComponentsInChildren<Renderer>(
                    true
                );

            burstVisual.SetActive(false);
        }

        if (burstWallVisual != null)
        {
            burstWallVisualRenderers =
                burstWallVisual.GetComponentsInChildren<Renderer>(
                    true
                );

            burstWallVisual.SetActive(false);
        }

        TurnMaskOff();

        currentBurstRadius =
            startingBurstRadius;

        currentBurstOrigin =
            transform.position;

        Debug.Log(
            "LightBurstController initialised. Burst light cost: " +
            lightCost.ToString("0.0")
        );
    }

    private void Update()
    {
        /*
         * The active Burst follows the player. DarknessCutoutController queries
         * this origin when rebuilding its CPU mask, keeping the cut-out centred
         * on the same location as the gameplay ability.
         */
        if (isBurstActive)
        {
            currentBurstOrigin =
                transform.position;
        }

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

    /*
     * DarknessCutoutController was originally written to support several
     * persistent Burst openings. The restored Burst no longer persists, so this
     * compatibility method reports either one currently active Burst or none.
     */
    public int GetActiveBurstEffectCount()
    {
        return
            isBurstActive
                ? 1
                : 0;
    }

    /*
     * Only index 0 can exist now because the old-style Burst has one live
     * two-second area rather than a collection of lingering Burst openings.
     */
    public Vector2 GetBurstEffectOrigin(
        int index
    )
    {
        if (
            !isBurstActive ||
            index != 0
        )
        {
            return transform.position;
        }

        return currentBurstOrigin;
    }

    /*
     * Returning the current gameplay radius allows the CPU darkness mask to
     * expand using exactly the same curve and timing as Light Burst.
     */
    public float GetBurstEffectRadius(
        int index
    )
    {
        if (
            !isBurstActive ||
            index != 0
        )
        {
            return 0f;
        }

        return currentBurstRadius;
    }

    /*
     * DarknessZone and other gameplay systems can use this to determine whether
     * a world position is currently protected by the Burst opening.
     */
    public bool IsPositionInsideBurstEffect(
        Vector2 worldPosition
    )
    {
        if (!isBurstActive)
        {
            return false;
        }

        return
            Vector2.Distance(
                worldPosition,
                currentBurstOrigin
            ) <=
            currentBurstRadius;
    }

    public void ActivateBurst()
    {
        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
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
            Debug.Log(
                "Light Burst activation was blocked because the player is dashing."
            );

            return;
        }

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

        if (playerAnimationController != null)
        {
            playerAnimationController.PlayLightBurstAnimation();
        }

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

        currentBurstRadius =
            startingBurstRadius;

        // Capture the starting position immediately so DarknessCutoutController
        // can build the first frame of the circular opening correctly.
        currentBurstOrigin =
            transform.position;

        SetBurstRenderersVisible(
            true
        );

        if (burstVisual != null)
        {
            burstVisual.SetActive(true);
        }

        if (burstWallVisual != null)
        {
            burstWallVisual.SetActive(true);
        }

        TurnMaskOn();

        Debug.Log(
            "Light burst active."
        );

        float timer = 0f;

        // Darkness and hidden-platform checks run at a modest interval so the
        // ability remains responsive without performing overlap checks every frame.
        float dispelCheckInterval = 0.05f;
        float dispelCheckTimer = 0f;

        float flickerTimer = 0f;
        bool visualsVisible = true;

        while (timer < burstDuration)
        {
            timer +=
                Time.deltaTime;

            dispelCheckTimer +=
                Time.deltaTime;

            /*
             * The origin continues following CatMoth while Burst is alive.
             * DarknessCutoutController reads the same value when it rebuilds its
             * dynamic mask, preventing the visual opening from lagging behind.
             */
            currentBurstOrigin =
                transform.position;

            float normalisedExpansionTime =
                burstExpansionDuration > 0f
                    ? Mathf.Clamp01(
                        timer /
                        burstExpansionDuration
                    )
                    : 1f;

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

            if (
                dispelCheckTimer >=
                dispelCheckInterval
            )
            {
                // These interactions intentionally ignore walls because Burst was
                // later changed to affect darkness and hidden platforms radially.
                DispelDarknessInRadius();
                CheckLightPlatformInBurst();

                dispelCheckTimer = 0f;
            }

            float remainingBurstTime =
                burstDuration -
                timer;

            /*
             * Only renderers blink during the expiry warning. The logical Burst,
             * darkness cut-out and safe-area query remain active continuously.
             */
            if (
                remainingBurstTime <=
                flickerWarningDuration
            )
            {
                flickerTimer +=
                    Time.deltaTime;

                if (
                    flickerTimer >=
                    flickerInterval
                )
                {
                    visualsVisible =
                        !visualsVisible;

                    SetBurstRenderersVisible(
                        visualsVisible
                    );

                    flickerTimer = 0f;
                }
            }

            yield return null;
        }

        // Ensure the last gameplay check reaches the intended full radius before
        // the ability and darkness opening disappear.
        currentBurstRadius =
            burstDispelRadius;

        DispelDarknessInRadius();
        CheckLightPlatformInBurst();

        SetBurstRenderersVisible(
            true
        );

        /*
         * This flag is switched off before resetting the radius because the
         * darkness controller interprets false as zero active Burst openings.
         */
        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        if (burstWallVisual != null)
        {
            burstWallVisual.SetActive(false);
        }

        TurnMaskOff();

        currentBurstRadius =
            startingBurstRadius;

        currentBurstOrigin =
            transform.position;

        burstCoroutine = null;

        Debug.Log(
            "Light burst ended."
        );
    }

    private void SetBurstRenderersVisible(
        bool shouldBeVisible
    )
    {
        /*
         * Renderer.enabled is used rather than disabling the GameObjects because
         * the Burst must continue existing logically while the warning flashes.
         */
        if (burstVisualRenderers != null)
        {
            foreach (
                Renderer burstRenderer
                in burstVisualRenderers
            )
            {
                if (burstRenderer != null)
                {
                    burstRenderer.enabled =
                        shouldBeVisible;
                }
            }
        }

        if (burstWallVisualRenderers != null)
        {
            foreach (
                Renderer burstRenderer
                in burstWallVisualRenderers
            )
            {
                if (burstRenderer != null)
                {
                    burstRenderer.enabled =
                        shouldBeVisible;
                }
            }
        }
    }

    private void DispelDarknessInRadius()
    {
        /*
         * No wall obstruction test is performed here. This preserves the later
         * decision that Light Burst passes through tiles and platforms.
         */
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                currentBurstOrigin,
                currentBurstRadius,
                darknessLayer
            );

        int dispelledCount = 0;

        foreach (Collider2D hit in hits)
        {
            DarknessZone darknessZone =
                hit.GetComponentInParent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
                dispelledCount++;
            }
        }

        Debug.Log(
            "Light Burst darkness check. Dispelled: " +
            dispelledCount
        );
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;

        Debug.Log(
            "Light burst cooldown started."
        );

        yield return new WaitForSeconds(
            burstCooldownDuration
        );

        isOnCooldown = false;
        cooldownCoroutine = null;

        Debug.Log(
            "Light burst cooldown ended."
        );
    }

    private void OnDrawGizmosSelected()
    {
        // Yellow shows the maximum range that Burst can eventually reach.
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            burstDispelRadius
        );

        // Cyan shows the live gameplay radius during Play Mode.
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

        Vector3 centre =
            currentBurstOrigin;

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
        return burstDispelRadius;
    }

    public float GetCurrentBurstRadius()
    {
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
        /*
         * Hidden Burst platforms continue responding through geometry. This was
         * deliberately retained from the newer version rather than reverting to
         * the older wall-blocked implementation.
         */
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                currentBurstOrigin,
                currentBurstRadius,
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