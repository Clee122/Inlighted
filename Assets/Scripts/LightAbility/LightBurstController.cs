using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LightBurstController : MonoBehaviour
{
    private enum BurstEffectPhase
    {
        Expanding,
        Holding,
        Reforming
    }

    /*
     * Each cast keeps its own environmental state after the visible Burst ends.
     * This allows several Burst openings to linger/reform independently instead
     * of forcing the darkness system to own the ability's lifetime.
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

    // Cooldown remains separate from the environmental lifetime so another Burst
    // can become available without forcing an older darkness opening to disappear.
    [SerializeField] private float burstCooldownDuration = 2f;

    // Burst expands quickly so activation feels immediate while still retaining
    // visible outward growth for the VFX and darkness response.
    [SerializeField] private float burstExpansionDuration = 0.2f;

    // The Burst begins close to the player instead of instantly affecting the
    // entire maximum radius.
    [SerializeField] private float startingBurstRadius = 0.2f;

    // This curve controls how quickly the gameplay radius reaches maximum size.
    [SerializeField]
    private AnimationCurve burstExpansionCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0.5f),
            new Keyframe(0.5f, 0.8f),
            new Keyframe(0.75f, 0.95f),
            new Keyframe(1f, 1f)
        );

    [Header("Burst Persistence")]

    // After the visible Burst finishes, the environmental effect stays at full
    // radius so the player has time to move through the cleared darkness.
    [SerializeField] private float burstHoldDuration = 2.5f;

    // After holding, the environmental effect shrinks so darkness can reform
    // gradually rather than snapping back immediately.
    [SerializeField] private float burstReformDuration = 1.5f;

    [Header("Light Resource Cost")]
    [SerializeField] private float lightCost = 25f;

    [Header("Audio")]

    // Burst audio is assigned independently from the VFX so the final sound
    // can change without affecting gameplay timing.
    [SerializeField] private AudioClip burstSound;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    // This visual exists only during the short expanding cast. The lingering
    // environmental effect is represented by the darkness system instead.
    [SerializeField] private GameObject burstWallVisual;

    [Header("Reveal Mask")]
    [SerializeField] private GameObject revealMask;

    [Header("Darkness Dispel")]
    [SerializeField] private float burstDispelRadius = 3f;
    [SerializeField] private LayerMask darknessLayer;
    [SerializeField] private LayerMask GroundLayer;

    // Gameplay Burst interaction still respects walls even though the Burst VFX
    // itself may visually draw across normal level tiles.
    [SerializeField] private LayerMask wallLayer;

    [Header("Debug")]
    [SerializeField] private bool showBurstDebug = true;
    [SerializeField] private int debugCircleSegments = 48;

    private bool isBurstActive = false;
    private bool isOnCooldown = false;

    // This value represents only the currently expanding cast. Lingering Burst
    // effects keep their own radii inside activeBurstEffects.
    private float currentBurstRadius = 0f;

    private Coroutine burstCoroutine;
    private Coroutine cooldownCoroutine;

    /*
     * The ability owns every lingering Burst effect. Darkness visuals and safe
     * area gameplay read this list indirectly through the public getter methods.
     */
    private readonly List<BurstEffectData> activeBurstEffects =
        new List<BurstEffectData>();

    private BurstEffectData liveBurstEffect;

    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;
    private PlayerDash playerDash;

    private void Awake()
    {
        // The unlock system controls whether Light Burst has been earned.
        abilityUnlocks =
            GetComponent<PlayerAbilityUnlocks>();

        // All Burst costs come from the shared light resource.
        playerLightResource =
            GetComponent<PlayerLightResource>();

        // Channeling and Burst remain mutually exclusive.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        // Burst cannot begin during an active dash.
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

        Debug.Log(
            "LightBurstController initialised. Burst light cost: " +
            lightCost.ToString("0.0")
        );
    }

    private void Update()
    {
        /*
         * Lingering effects continue updating after the visible Burst coroutine
         * ends. Keeping this outside BurstRoutine also lets several older casts
         * reform independently while a newer Burst is fired.
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
        // This remains expansion-only so existing systems can still distinguish
        // the actual cast from the longer environmental consequence.
        return isBurstActive;
    }

    public bool IsOnCooldown()
    {
        return isOnCooldown;
    }

    public int GetActiveBurstEffectCount()
    {
        // Darkness systems use the count rather than receiving the mutable list,
        // preventing outside scripts from accidentally modifying ability state.
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
         * Other gameplay systems can query the same ability-owned areas that
         * drive the darkness cut-out, keeping safety and visuals consistent.
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
         * A new environmental effect is created immediately so the darkness can
         * follow the same expanding radius as the visible Burst from frame one.
         */
        liveBurstEffect =
            new BurstEffectData
            {
                phase =
                    BurstEffectPhase.Expanding,

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

        while (timer < burstExpansionDuration)
        {
            timer +=
                Time.deltaTime;

            dispelCheckTimer +=
                Time.deltaTime;

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

            /*
             * Only the currently casting Burst follows the player. Once expansion
             * finishes, its lingering area stays fixed at the cast location.
             */
            if (liveBurstEffect != null)
            {
                liveBurstEffect.originWorld =
                    transform.position;

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

            yield return null;
        }

        currentBurstRadius =
            burstDispelRadius;

        if (liveBurstEffect != null)
        {
            liveBurstEffect.currentRadius =
                burstDispelRadius;

            /*
             * The visible cast has finished, but the ability itself now owns the
             * full-radius lingering period before darkness is allowed to reform.
             */
            liveBurstEffect.phase =
                BurstEffectPhase.Holding;

            liveBurstEffect.holdTimer = 0f;
        }

        DispelDarknessInRadius();
        CheckLightPlatformInBurst();

        /*
         * The casting/VFX state ends here. The environmental Burst remains in
         * activeBurstEffects and continues independently.
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
            "Light burst expansion completed. Environmental effect is lingering."
        );
    }

    private void UpdateBurstEffectLifetimes()
    {
        /*
         * Each Burst owns its own timers so newer casts never cancel an older
         * opening that is still holding or reforming elsewhere in the level.
         */
        for (
            int i = activeBurstEffects.Count - 1;
            i >= 0;
            i--
        )
        {
            BurstEffectData effect =
                activeBurstEffects[i];

            // Expansion is controlled directly by BurstRoutine.
            if (
                effect.phase ==
                BurstEffectPhase.Expanding
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

                    effect.reformTimer = 0f;
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
             * Reform shrinks the ability-owned radius itself. Darkness and safe
             * area systems therefore automatically see the same closing opening.
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

    private void DispelDarknessInRadius()
    {
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
            /*
             * Gameplay interaction still respects walls even though the Burst
             * visual itself can be rendered over normal tiles.
             */
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

        Debug.Log(
            "Light Burst darkness check. Dispelled: " +
            dispelledCount +
            ", blocked by walls: " +
            blockedCount
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
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            burstDispelRadius
        );

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
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                currentBurstRadius,
                GroundLayer
            );

        foreach (Collider2D hit in hits)
        {
            // Platform activation remains blocked by normal walls even though the
            // Burst VFX itself can visually overlap those tiles.
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

        Vector2 targetPoint =
            targetCollider.ClosestPoint(
                burstOrigin
            );

        Vector2 direction =
            targetPoint -
            burstOrigin;

        float distance =
            direction.magnitude;

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