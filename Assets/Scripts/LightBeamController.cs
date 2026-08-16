using UnityEngine;
using System.Collections;

public class LightBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    [SerializeField] private float beamRange = 6f;
    [SerializeField] private float beamWidth = 1.5f;
    [SerializeField] private LayerMask darknessLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("Light Resource Cost")]
    [SerializeField] private float lightCost = 15f;

    [Header("Beam Timing")]
    [SerializeField] private float beamActiveDuration = 0.5f;

    // The Beam reaches its final length before the ability ends so it can travel
    // out from the player and still remain visible at full length for a moment.
    [SerializeField] private float beamExpansionDuration = 0.2f;

    [SerializeField] private float beamCooldown = 2f;
    [SerializeField] private float beamCheckInterval = 0.05f;

    [Header("Beam Origin")]
    [SerializeField] private Transform beamOrigin;

    [Header("Beam Visuals")]
    [SerializeField] private GameObject beamIndicatorVisual;
    [SerializeField] private GameObject beamVisual;

    // The fired beam uses a Line Renderer so its end point can grow outwards
    // and stop at the same distance found by the wall check.
    [SerializeField] private LineRenderer beamLineRenderer;

    // The particle effect is controlled separately so it can restart cleanly
    // for each shot and use the same overall timing as the beam.
    [SerializeField] private ParticleSystem beamParticles;

    // Keeps the fired beam lined up with the aiming indicator without changing
    // the position used by the gameplay checks.
    [SerializeField]
    private Vector2 beamVisualOffset =
        new Vector2(0f, 0.5f);

    // Lets the visible beam thickness be adjusted without changing the width
    // used for darkness and gate detection.
    [SerializeField] private float beamVisualWidthMultiplier = 1f;

    private bool isAiming = false;
    private bool isBeamActive = false;
    private bool isOnCooldown = false;

    private Coroutine beamCoroutine;
    private Coroutine cooldownCoroutine;

    // These values contain the current aiming result while the red indicator
    // follows the mouse and shortens when it encounters a wall.
    private Vector2 lastBeamCenter;
    private Vector2 lastBeamSize;

    private Vector2 lastBeamDirection =
        Vector2.right;

    private float lastBeamAngle = 0f;

    // These values capture the final aiming result when the player confirms
    // the shot so the active Beam cannot move or rotate with the mouse.
    private Vector2 lockedBeamCenter;
    private Vector2 lockedBeamSize;

    private Vector2 lockedBeamDirection =
        Vector2.right;

    private float lockedBeamAngle = 0f;
    private Vector2 lockedBeamOrigin;

    // The current Line Renderer start point is kept so any local offset already
    // set on the beam prefab is not lost when its length is changed.
    private Vector3 beamLineStartLocalPosition =
        Vector3.zero;

    // The original particle lifetime is kept so the playback speed can adjust
    // automatically if the Beam Active Duration is changed later.
    private float originalParticleLifetime = 1f;

    private Camera mainCamera;
    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;

    // Beam aiming remains available during dash, but this reference lets the
    // actual firing action wait until dash movement has finished.
    private PlayerDash playerDash;

    private void Awake()
    {
        mainCamera = Camera.main;

        abilityUnlocks =
            GetComponent<PlayerAbilityUnlocks>();

        playerLightResource =
            GetComponent<PlayerLightResource>();

        // Beam aiming must be blocked before a preview begins while channeling.
        playerLightChannel =
            GetComponent<PlayerLightChannel>();

        // Dash does not block aiming, but firing checks this state so the player
        // can prepare their shot while moving and commit after the dash finishes.
        playerDash =
            GetComponent<PlayerDash>();

        if (playerLightResource == null)
        {
            Debug.LogError(
                "LightBeamController could not find PlayerLightResource on " +
                gameObject.name +
                ". Light Beam will not fire until the component is added."
            );
        }

        if (beamIndicatorVisual != null)
        {
            // The indicator is detached because the script controls its world
            // position independently while the player continues moving.
            beamIndicatorVisual.transform.SetParent(
                null,
                true
            );

            beamIndicatorVisual.transform.localScale =
                Vector3.one;

            beamIndicatorVisual.SetActive(false);
        }

        if (beamVisual != null)
        {
            // The fired Beam is detached so it can remain fixed at the position
            // where the player confirmed the shot.
            beamVisual.transform.SetParent(
                null,
                true
            );

            beamVisual.transform.localScale =
                Vector3.one;

            // Use the child references automatically if they were not assigned
            // in the Inspector.
            if (beamLineRenderer == null)
            {
                beamLineRenderer =
                    beamVisual.GetComponentInChildren<LineRenderer>(
                        true
                    );
            }

            if (beamParticles == null)
            {
                beamParticles =
                    beamVisual.GetComponentInChildren<ParticleSystem>(
                        true
                    );
            }

            beamVisual.SetActive(false);
        }

        if (
            beamLineRenderer != null &&
            beamLineRenderer.positionCount > 0
        )
        {
            // Keep the existing local start point instead of assuming the line
            // begins at exactly zero.
            beamLineStartLocalPosition =
                beamLineRenderer.GetPosition(
                    0
                );
        }

        if (beamParticles != null)
        {
            ParticleSystem.MainModule main =
                beamParticles.main;

            // Use the longest starting lifetime as the timing reference.
            originalParticleLifetime =
                Mathf.Max(
                    0.01f,
                    main.startLifetime.constantMax
                );

            ConfigureBeamParticleWallCollision();
        }

        Debug.Log(
            "LightBeamController initialised. Beam light cost: " +
            lightCost.ToString("0.0")
        );
    }

    private void Update()
    {
        if (!isAiming)
        {
            return;
        }

        // The aiming preview is recalculated only while aiming. This also means
        // the indicator keeps following the player while they perform a dash.
        UpdateBeamPreview(
            beamIndicatorVisual
        );

        if (
            UnityEngine.InputSystem.Mouse.current ==
            null
        )
        {
            return;
        }

        if (
            UnityEngine.InputSystem.Mouse.current
                .leftButton
                .wasPressedThisFrame
        )
        {
            ConfirmFireBeam();
        }

        if (
            UnityEngine.InputSystem.Mouse.current
                .rightButton
                .wasPressedThisFrame
        )
        {
            CancelBeamAim();
        }
    }

    public bool IsBeamActive()
    {
        return isBeamActive;
    }

    public bool IsAiming()
    {
        return isAiming;
    }

    // Called by the Beam input.
    public void FireBeam()
    {
        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            // Beam input is ignored rather than cancelling channeling, ensuring
            // channeling and ability use remain mutually exclusive.
            Debug.Log(
                "Light Beam aiming was blocked because the player is channeling."
            );

            return;
        }

        BeginBeamAim();
    }

    public void BeginBeamAim()
    {
        // This second channel check protects against another script directly
        // calling BeginBeamAim instead of going through FireBeam.
        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            Debug.Log(
                "Light Beam aiming was blocked because the player is channeling."
            );

            return;
        }

        if (
            abilityUnlocks != null &&
            !abilityUnlocks.HasLightBeam()
        )
        {
            Debug.Log(
                "Light Beam is locked"
            );

            return;
        }

        if (isOnCooldown)
        {
            Debug.Log(
                "Light Beam aiming could not begin because the ability is on cooldown."
            );

            return;
        }

        if (isBeamActive)
        {
            Debug.Log(
                "Light Beam aiming could not begin because the beam is already active."
            );

            return;
        }

        if (isAiming)
        {
            Debug.Log(
                "Light Beam is already being aimed."
            );

            return;
        }

        isAiming = true;

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.SetActive(true);
        }

        if (beamVisual != null)
        {
            beamVisual.SetActive(false);
        }

        UpdateBeamPreview(
            beamIndicatorVisual
        );

        Debug.Log(
            "Light beam aiming started. No light has been spent yet."
        );
    }

    private void CancelBeamAim()
    {
        if (!isAiming)
        {
            return;
        }

        isAiming = false;

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.SetActive(false);
        }

        Debug.Log(
            "Light beam aiming cancelled. No light was spent."
        );
    }

    private void ConfirmFireBeam()
    {
        if (!isAiming)
        {
            Debug.Log(
                "Light Beam could not fire because the player was not aiming."
            );

            return;
        }

        if (
            playerDash != null &&
            playerDash.IsDashing()
        )
        {
            // The aiming preview remains active while dashing. Only the committed
            // shot is blocked so the player can fire as soon as dash finishes.
            Debug.Log(
                "Light Beam firing was blocked because the player is dashing."
            );

            return;
        }

        if (
            playerLightChannel != null &&
            playerLightChannel.IsChanneling()
        )
        {
            CancelBeamAim();

            Debug.Log(
                "Light Beam firing was blocked because the player is channeling."
            );

            return;
        }

        if (isOnCooldown)
        {
            Debug.Log(
                "Light Beam could not fire because it is on cooldown."
            );

            return;
        }

        if (isBeamActive)
        {
            Debug.Log(
                "Light Beam could not fire because it is already active."
            );

            return;
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "Light Beam could not fire because PlayerLightResource is missing."
            );

            return;
        }

        if (
            !playerLightResource.TrySpendLight(
                lightCost,
                "Light Beam"
            )
        )
        {
            Debug.Log(
                "Light Beam firing was blocked because the player did not have enough light."
            );

            return;
        }

        // The final red indicator result becomes the fixed fired trajectory.
        // The active Beam will continue using these values even if the player
        // moves the mouse or changes position after confirming the shot.
        lockedBeamCenter =
            lastBeamCenter;

        lockedBeamSize =
            lastBeamSize;

        lockedBeamDirection =
            lastBeamDirection;

        lockedBeamAngle =
            lastBeamAngle;

        lockedBeamOrigin =
            beamOrigin != null
                ? (Vector2)beamOrigin.position
                : (Vector2)transform.position;

        isAiming = false;

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.SetActive(false);
        }

        if (beamCoroutine != null)
        {
            StopCoroutine(
                beamCoroutine
            );
        }

        beamCoroutine =
            StartCoroutine(
                BeamRoutine()
            );

        if (cooldownCoroutine != null)
        {
            StopCoroutine(
                cooldownCoroutine
            );
        }

        cooldownCoroutine =
            StartCoroutine(
                CooldownRoutine()
            );

        Debug.Log(
            "Light Beam successfully fired after spending " +
            lightCost.ToString("0.0") +
            " light. Locked length: " +
            lockedBeamSize.x.ToString("0.00")
        );
    }

    private IEnumerator BeamRoutine()
    {
        isBeamActive = true;

        if (beamVisual != null)
        {
            beamVisual.SetActive(true);

            // The fired Beam is positioned once using the locked aiming result.
            // Its length then grows without following later mouse movement.
            PrepareLockedBeamVisual();
        }

        // Expansion is capped by the full Beam duration so changing either
        // value in the Inspector cannot make the growth outlive the ability.
        float actualExpansionDuration =
            Mathf.Clamp(
                beamExpansionDuration,
                0f,
                beamActiveDuration
            );

        float timer = 0f;
        float gameplayCheckTimer = 0f;

        Debug.Log(
            "Light beam active with locked trajectory."
        );

        while (timer < beamActiveDuration)
        {
            float expansionProgress =
                actualExpansionDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        timer /
                        actualExpansionDuration
                    );

            float currentBeamLength =
                Mathf.Lerp(
                    0f,
                    lockedBeamSize.x,
                    expansionProgress
                );

            // Grow the visible line from the player towards the locked end point.
            UpdateBeamLineLength(
                currentBeamLength
            );

            // Gameplay uses the same growing length so it does not get ahead
            // of the visible Beam.
            ApplyCurrentBeamCollisionValues(
                currentBeamLength
            );

            if (
                gameplayCheckTimer <= 0f &&
                currentBeamLength > 0.001f
            )
            {
                DispelDarknessInBeam();
                CheckLightGateInBeam();

                gameplayCheckTimer =
                    Mathf.Max(
                        0.001f,
                        beamCheckInterval
                    );
            }

            gameplayCheckTimer -=
                Time.deltaTime;

            timer +=
                Time.deltaTime;

            // Update every frame so the outward movement stays smooth.
            yield return null;
        }

        isBeamActive = false;

        if (beamParticles != null)
        {
            // Clear particles between shots so the next Beam starts cleanly.
            beamParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );
        }

        if (beamVisual != null)
        {
            beamVisual.SetActive(false);
        }

        beamCoroutine = null;

        Debug.Log(
            "Light beam ended."
        );
    }

    private void UpdateBeamPreview(
        GameObject visualObject
    )
    {
        Vector2 originPosition =
            beamOrigin != null
                ? beamOrigin.position
                : transform.position;

        Vector2 direction =
            GetMouseAimDirection(
                originPosition
            );

        float actualRange =
            GetBeamRangeBeforeWall(
                originPosition,
                direction
            );

        Vector2 boxCenter =
            originPosition +
            direction *
            (actualRange * 0.5f);

        Vector2 boxSize =
            new Vector2(
                actualRange,
                beamWidth
            );

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) *
            Mathf.Rad2Deg;

        lastBeamCenter = boxCenter;
        lastBeamSize = boxSize;
        lastBeamDirection = direction;
        lastBeamAngle = angle;

        if (visualObject != null)
        {
            visualObject.transform.position =
                originPosition;

            visualObject.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );

            visualObject.transform.localScale =
                new Vector3(
                    actualRange,
                    beamWidth,
                    1f
                );

            // The indicator uses a centred sprite, so it is moved halfway along
            // the calculated range to begin at the player and end at the wall.
            visualObject.transform.Translate(
                Vector3.right *
                (actualRange * 0.5f),
                Space.Self
            );
        }
    }

    private void PrepareLockedBeamVisual()
    {
        if (beamVisual == null)
        {
            return;
        }

        // Rotate the visual offset with the shot so the same adjustment works
        // when aiming horizontally, vertically or diagonally.
        Vector2 rotatedVisualOffset =
            Quaternion.Euler(
                0f,
                0f,
                lockedBeamAngle
            ) *
            beamVisualOffset;

        // The fired visual keeps the exact locked origin and angle from the
        // final aiming preview, with only the visual alignment offset added.
        beamVisual.transform.position =
            lockedBeamOrigin +
            rotatedVisualOffset;

        beamVisual.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                lockedBeamAngle
            );

        // Keep the root at normal scale so changing Beam length does not stretch
        // the particle effect.
        beamVisual.transform.localScale =
            Vector3.one;

        if (beamLineRenderer != null)
        {
            // Keep visual thickness separate from the gameplay Beam width so it
            // can be matched to the aiming indicator without changing collision.
            float visualWidth =
                beamWidth *
                beamVisualWidthMultiplier;

            beamLineRenderer.startWidth =
                visualWidth;

            beamLineRenderer.endWidth =
                visualWidth;
        }

        // Start each shot at zero length so it visibly travels out from the player.
        UpdateBeamLineLength(
            0f
        );

        if (beamParticles != null)
        {
            ParticleSystem.MainModule main =
                beamParticles.main;

            // Speed the particle effect up or down to follow Beam Active Duration.
            main.simulationSpeed =
                originalParticleLifetime /
                Mathf.Max(
                    0.01f,
                    beamActiveDuration
                );

            // Restart the effect from the beginning for every shot.
            beamParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );

            beamParticles.Play(
                true
            );
        }
    }

    private void UpdateBeamLineLength(
        float currentLength
    )
    {
        if (beamLineRenderer == null)
        {
            return;
        }

        beamLineRenderer.positionCount = 2;

        beamLineRenderer.SetPosition(
            0,
            beamLineStartLocalPosition
        );

        // The Beam root already carries the locked rotation, so the line only
        // needs to extend along its local X axis.
        beamLineRenderer.SetPosition(
            1,
            beamLineStartLocalPosition +
            Vector3.right *
            Mathf.Max(
                0f,
                currentLength
            )
        );
    }

    private void ApplyCurrentBeamCollisionValues(
        float currentLength
    )
    {
        // Darkness and gate checks use the same growing length as the visible
        // Beam so gameplay remains lined up with the effect.
        lastBeamCenter =
            lockedBeamOrigin +
            lockedBeamDirection *
            (currentLength * 0.5f);

        lastBeamSize =
            new Vector2(
                currentLength,
                lockedBeamSize.y
            );

        lastBeamDirection =
            lockedBeamDirection;

        lastBeamAngle =
            lockedBeamAngle;
    }

    private void ConfigureBeamParticleWallCollision()
    {
        if (beamParticles == null)
        {
            return;
        }

        ParticleSystem.CollisionModule collision =
            beamParticles.collision;

        // Use the same wall layer as the Beam raycast so particles stop on the
        // same surfaces that stop the ability.
        collision.enabled = true;
        collision.type =
            ParticleSystemCollisionType.World;

        collision.mode =
            ParticleSystemCollisionMode.Collision2D;

        collision.collidesWith =
            wallLayer;

        // Remove particles when they hit a wall instead of allowing them to
        // bounce or continue through it.
        collision.lifetimeLoss = 1f;
        collision.bounce = 0f;
        collision.dampen = 1f;
    }

    private Vector2 GetMouseAimDirection(
        Vector2 originPosition
    )
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (
            mainCamera == null ||
            UnityEngine.InputSystem.Mouse.current ==
            null
        )
        {
            return lastBeamDirection;
        }

        Vector2 mouseScreenPosition =
            UnityEngine.InputSystem.Mouse.current
                .position
                .ReadValue();

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(
                mouseScreenPosition
            );

        mouseWorldPosition.z = 0f;

        Vector2 direction =
            (
                (Vector2)mouseWorldPosition -
                originPosition
            ).normalized;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return lastBeamDirection;
        }

        return direction;
    }

    private float GetBeamRangeBeforeWall(
        Vector2 originPosition,
        Vector2 direction
    )
    {
        RaycastHit2D wallHit =
            Physics2D.Raycast(
                originPosition,
                direction,
                beamRange,
                wallLayer
            );

        if (wallHit.collider != null)
        {
            return wallHit.distance;
        }

        return beamRange;
    }

    private void DispelDarknessInBeam()
    {
        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                lastBeamCenter,
                lastBeamSize,
                lastBeamAngle,
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
            "Light beam dispelled darkness zones: " +
            hits.Length
        );
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;

        Debug.Log(
            "Light beam cooldown started."
        );

        yield return new WaitForSeconds(
            beamCooldown
        );

        isOnCooldown = false;
        cooldownCoroutine = null;

        Debug.Log(
            "Light beam cooldown ended."
        );
    }

    public bool IsBoundsOverlappingActiveBeam(
        Bounds darknessBounds
    )
    {
        if (!isBeamActive)
        {
            return false;
        }

        Vector2 beamRight =
            lastBeamDirection.normalized;

        Vector2 beamUp =
            new Vector2(
                -beamRight.y,
                beamRight.x
            );

        Vector2 difference =
            (Vector2)darknessBounds.center -
            lastBeamCenter;

        float distanceAlongBeam =
            Mathf.Abs(
                Vector2.Dot(
                    difference,
                    beamRight
                )
            );

        float distanceAcrossBeam =
            Mathf.Abs(
                Vector2.Dot(
                    difference,
                    beamUp
                )
            );

        Vector2 boundsExtents =
            darknessBounds.extents;

        float darknessProjectionAlongBeam =
            Mathf.Abs(
                Vector2.Dot(
                    Vector2.right *
                    boundsExtents.x,
                    beamRight
                )
            ) +
            Mathf.Abs(
                Vector2.Dot(
                    Vector2.up *
                    boundsExtents.y,
                    beamRight
                )
            );

        float darknessProjectionAcrossBeam =
            Mathf.Abs(
                Vector2.Dot(
                    Vector2.right *
                    boundsExtents.x,
                    beamUp
                )
            ) +
            Mathf.Abs(
                Vector2.Dot(
                    Vector2.up *
                    boundsExtents.y,
                    beamUp
                )
            );

        float beamHalfLength =
            lastBeamSize.x * 0.5f;

        float beamHalfWidth =
            lastBeamSize.y * 0.5f;

        bool overlapsAlongBeam =
            distanceAlongBeam <=
            beamHalfLength +
            darknessProjectionAlongBeam;

        bool overlapsAcrossBeam =
            distanceAcrossBeam <=
            beamHalfWidth +
            darknessProjectionAcrossBeam;

        return
            overlapsAlongBeam &&
            overlapsAcrossBeam;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 originPosition =
            beamOrigin != null
                ? beamOrigin.position
                : transform.position;

        Vector2 direction =
            lastBeamDirection.normalized;

        Vector2 boxCenter =
            originPosition +
            direction *
            (beamRange * 0.5f);

        Vector2 boxSize =
            new Vector2(
                beamRange,
                beamWidth
            );

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) *
            Mathf.Rad2Deg;

        Gizmos.color = Color.yellow;

        Matrix4x4 oldMatrix =
            Gizmos.matrix;

        Gizmos.matrix =
            Matrix4x4.TRS(
                boxCenter,
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                ),
                Vector3.one
            );

        Gizmos.DrawWireCube(
            Vector3.zero,
            boxSize
        );

        Gizmos.matrix = oldMatrix;
    }

    public void OnMoveForBeam(
        UnityEngine.InputSystem.InputAction
            .CallbackContext context
    )
    {
        // No longer needed for mouse aiming, but kept so existing Player Input
        // events do not break.
    }

    private void CheckLightGateInBeam()
    {
        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                lastBeamCenter,
                lastBeamSize,
                lastBeamAngle,
                darknessLayer
            );

        foreach (Collider2D hit in hits)
        {
            spawn_platform gate =
                hit.GetComponentInParent<spawn_platform>();

            if (gate != null)
            {
                gate.Activatespawn();

                Debug.Log(
                    "Light Beam activated gate: " +
                    gate.gameObject.name
                );
            }
        }
    }
}