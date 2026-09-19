using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LightBurstController : MonoBehaviour
{
    private enum BurstEffectPhase
    {
        Active,
        Holding,
        Reforming
    }

    /*
     * Each Burst keeps its own darkness deformation after the visible ability
     * ends. This lets an old opening hold and reform independently while the
     * player is already able to create another Burst elsewhere.
     */
    private class BurstEffectData
    {
        public BurstEffectPhase phase;

        public Vector2 originWorld;

        public float currentRadius;
        public float maximumRadius;

        public float holdTimer;
        public float reformTimer;
    }

    [Header("Burst Settings")]

    // The visible Burst lasts long enough to provide a readable temporary safe
    // area rather than disappearing immediately after its expansion.
    [SerializeField] private float burstDuration = 2f;

    // The radius reaches maximum size during the first second, restoring the
    // older Light Burst timing rather than the newer near-instant expansion.
    [SerializeField] private float burstExpansionDuration = 1f;

    // Cooldown begins when the Burst is cast and remains independent from the
    // lingering darkness deformation that continues after the visible ability.
    [SerializeField] private float burstCooldownDuration = 2f;

    // The Burst begins close to the player so the expansion can be seen instead
    // of immediately appearing at the complete darkness-clearing radius.
    [SerializeField] private float startingBurstRadius = 0.2f;

    // The original easing curve is retained so only the overall expansion timing
    // changes rather than altering the shape and feel of the expansion itself.
    [SerializeField]
    private AnimationCurve burstExpansionCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0.5f),
            new Keyframe(0.5f, 0.8f),
            new Keyframe(0.75f, 0.95f),
            new Keyframe(1f, 1f)
        );

    [Header("Burst Expiry Warning")]

    // The visible Burst flashes near the end so players receive a warning before
    // their active protection disappears.
    [SerializeField] private float flickerWarningDuration = 0.5f;

    // Only the renderers blink. Gameplay and the darkness cut-out remain active
    // continuously until the full Burst duration has finished.
    [SerializeField] private float flickerInterval = 0.1f;

    [Header("Burst Persistence")]

    // Once the visible Burst ends, the darkness opening remains at full radius
    // for a short period so the environment keeps the deformation created by it.
    [SerializeField] private float burstHoldDuration = 2.5f;

    // After holding, the saved opening gradually shrinks so darkness reforms
    // naturally instead of snapping back around the player.
    [SerializeField] private float burstReformDuration = 1.5f;

    [Header("Light Resource Cost")]
    [SerializeField] private float lightCost = 25f;

    [Header("Audio")]

    // Burst audio remains separate from its gameplay timing so sound changes do
    // not require altering how the ability or darkness deformation behaves.
    [SerializeField] private AudioClip burstSound;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    // This generated radial visual is allowed to pass through normal level
    // geometry, matching the radial darkness and platform behaviour.
    [SerializeField] private GameObject burstWallVisual;

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

    /*
     * This radius represents the currently visible Burst only. Older darkness
     * openings keep their own radii inside activeBurstEffects.
     */
    private float currentBurstRadius = 0f;

    private Coroutine burstCoroutine;
    private Coroutine cooldownCoroutine;

    /*
     * DarknessCutoutController reads these saved effects so the deformation can
     * continue after the actual Light Burst ability has visually disappeared.
     */
    private readonly List<BurstEffectData> activeBurstEffects =
        new List<BurstEffectData>();

    // This identifies the Burst currently attached to the visible cast. Once the
    // cast finishes, the same data remains in the list for its hold/reform stages.
    private BurstEffectData liveBurstEffect;

    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;

    // Animation is triggered only after the ability has successfully activated so
    // failed inputs never play CatMoth's Burst animation.
    private PlayerAnimationController playerAnimationController;

    /*
     * Renderers are cached because expiry flickering should hide only graphics.
     * The Burst GameObjects stay active so gameplay and particle state are not
     * repeatedly restarted while the warning flashes.
     */
    private Renderer[] burstVisualRenderers;
    private Renderer[] burstWallVisualRenderers;

    private void Awake()
    {
        // The unlock component decides whether Light Burst has been earned.
        abilityUnlocks =
            GetComponent<PlayerAbilityUnlocks>();

        // Burst spends light through the shared player resource.
        playerLightResource =
            GetComponent<PlayerLightResource>();

        // Channeling and Light Burst remain mutually exclusive.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        // Animation remains separate from Burst gameplay and is notified only
        // once the cast has passed every activation requirement.
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

        Debug.Log(
            "LightBurstController initialised. Burst light cost: " +
            lightCost.ToString("0.0")
        );
    }

    private void Update()
    {
        /*
         * Lingering darkness openings continue updating even after their visible
         * Burst has ended. Keeping this separate lets several old openings hold
         * and reform independently.
         */
        UpdateBurstEffectLifetimes();

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
        /*
         * This reports only the actual two-second ability. A darkness deformation
         * may still exist afterwards without the player counting as actively
         * using Light Burst.
         */
        return isBurstActive;
    }

    public bool IsOnCooldown()
    {
        return isOnCooldown;
    }

    public int GetActiveBurstEffectCount()
    {
        // DarknessCutoutController uses this count to read every active opening
        // without receiving direct access to the mutable internal list.
        return activeBurstEffects.Count;
    }

    public Vector2 GetBurstEffectOrigin(
        int index
    )
    {
        if (
            index < 0 ||
            index >= activeBurstEffects.Count
        )
        {
            return transform.position;
        }

        return
            activeBurstEffects[index].originWorld;
    }

    public float GetBurstEffectRadius(
        int index
    )
    {
        if (
            index < 0 ||
            index >= activeBurstEffects.Count
        )
        {
            return 0f;
        }

        return
            activeBurstEffects[index].currentRadius;
    }

    public bool IsPositionInsideBurstEffect(
        Vector2 worldPosition
    )
    {
        /*
         * The same saved openings used by the darkness visuals are also available
         * to gameplay systems so visual safety and gameplay safety can agree.
         */
        foreach (BurstEffectData effect in activeBurstEffects)
        {
            if (
                Vector2.Distance(
                    worldPosition,
                    effect.originWorld
                ) <=
                effect.currentRadius
            )
            {
                return true;
            }
        }

        return false;
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

        // CatMoth's Burst animation begins only after every gameplay requirement
        // succeeds so blocked casts never trigger misleading animation feedback.
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

        /*
         * The darkness opening is created immediately. During the visible Burst,
         * it follows exactly the same origin and radius as the gameplay effect.
         */
        liveBurstEffect =
            new BurstEffectData
            {
                phase =
                    BurstEffectPhase.Active,

                originWorld =
                    transform.position,

                currentRadius =
                    startingBurstRadius,

                maximumRadius =
                    burstDispelRadius,

                holdTimer = 0f,
                reformTimer = 0f
            };

        activeBurstEffects.Add(
            liveBurstEffect
        );

        // Restore renderer visibility in case the previous Burst ended during the
        // hidden part of its flicker warning.
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
             * During the visible Burst, the effect continues following CatMoth.
             * Once the cast ends, its last position becomes the fixed location
             * where the darkness deformation remains.
             */
            if (liveBurstEffect != null)
            {
                liveBurstEffect.originWorld =
                    transform.position;
            }

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

            // The saved darkness cut-out expands with exactly the same radius as
            // the visible ability so there is no mismatch during the active cast.
            if (liveBurstEffect != null)
            {
                liveBurstEffect.currentRadius =
                    currentBurstRadius;
            }

            if (
                dispelCheckTimer >=
                dispelCheckInterval
            )
            {
                DispelDarknessInRadius();
                CheckLightPlatformInBurst();

                dispelCheckTimer = 0f;
            }

            float remainingBurstTime =
                burstDuration -
                timer;

            /*
             * Only the visible renderers flash. Darkness remains continuously
             * cleared throughout this warning period so the safe area does not
             * rapidly appear and disappear with the flicker.
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

        currentBurstRadius =
            burstDispelRadius;

        if (liveBurstEffect != null)
        {
            liveBurstEffect.currentRadius =
                burstDispelRadius;

            /*
             * The visible ability has ended, but its final position and full
             * radius remain stored so DarknessCutoutController keeps the opening.
             */
            liveBurstEffect.phase =
                BurstEffectPhase.Holding;

            liveBurstEffect.holdTimer =
                0f;
        }

        // Run one final radial interaction at complete size before the visible
        // ability disappears.
        DispelDarknessInRadius();
        CheckLightPlatformInBurst();

        SetBurstRenderersVisible(
            true
        );

        /*
         * The player is no longer actively using Burst from this point onwards.
         * Only the environmental deformation continues.
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

        liveBurstEffect = null;
        burstCoroutine = null;

        Debug.Log(
            "Light burst ended. Darkness deformation is now lingering."
        );
    }

    private void UpdateBurstEffectLifetimes()
    {
        /*
         * Each saved opening has independent timers so casting another Burst does
         * not cancel an older deformation that is still holding or reforming.
         */
        for (
            int i = activeBurstEffects.Count - 1;
            i >= 0;
            i--
        )
        {
            BurstEffectData effect =
                activeBurstEffects[i];

            // The visible cast directly controls radius and position while active,
            // so this method should not advance that effect's lifetime yet.
            if (
                effect.phase ==
                BurstEffectPhase.Active
            )
            {
                continue;
            }

            if (
                effect.phase ==
                BurstEffectPhase.Holding
            )
            {
                effect.holdTimer +=
                    Time.deltaTime;

                if (
                    effect.holdTimer >=
                    burstHoldDuration
                )
                {
                    effect.phase =
                        BurstEffectPhase.Reforming;

                    effect.reformTimer =
                        0f;
                }

                continue;
            }

            effect.reformTimer +=
                Time.deltaTime;

            float reformProgress =
                burstReformDuration > 0f
                    ? Mathf.Clamp01(
                        effect.reformTimer /
                        burstReformDuration
                    )
                    : 1f;

            /*
             * Shrinking the stored radius makes DarknessCutoutController naturally
             * rebuild increasingly smaller openings rather than needing separate
             * reform logic inside the darkness script.
             */
            effect.currentRadius =
                Mathf.Lerp(
                    effect.maximumRadius,
                    0f,
                    reformProgress
                );

            if (reformProgress >= 1f)
            {
                activeBurstEffects.RemoveAt(
                    i
                );
            }
        }
    }

    private void SetBurstRenderersVisible(
        bool shouldBeVisible
    )
    {
        /*
         * Renderer.enabled is used rather than disabling the GameObjects because
         * the Burst remains logically active while the warning flashes.
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
         * Burst remains a true radial ability. Walls, floors and platforms do not
         * block darkness objects from being affected inside the current radius.
         */
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
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
        /*
         * Cooldown does not wait for the old darkness opening to finish reforming.
         * This lets the player create another Burst while a previous deformation
         * still exists elsewhere.
         */
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
        // Yellow shows the complete radial range Burst can eventually affect.
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            burstDispelRadius
        );

        // Cyan shows the currently expanding gameplay radius while testing.
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
         * Hidden Burst platforms continue responding solely to radial distance.
         * Ordinary level geometry therefore does not block platform activation.
         */
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
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