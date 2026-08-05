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
    [SerializeField] private float beamCooldown = 2f;
    [SerializeField] private float beamCheckInterval = 0.05f;

    [Header("Beam Origin")]
    [SerializeField] private Transform beamOrigin;

    [Header("Beam Visuals")]
    [SerializeField] private GameObject beamIndicatorVisual;
    [SerializeField] private GameObject beamVisual;

    // The Particle System is controlled directly so every Beam activation
    // begins cleanly instead of reusing particles from an earlier shot.
    private ParticleSystem beamParticleSystem;

    private bool isAiming = false;
    private bool isBeamActive = false;
    private bool isOnCooldown = false;

    private Coroutine beamCoroutine;
    private Coroutine cooldownCoroutine;

    private Vector2 lastBeamCenter;
    private Vector2 lastBeamSize;

    private Vector2 lastBeamDirection =
        Vector2.right;

    private float lastBeamAngle = 0f;

    private Camera mainCamera;
    private PlayerAbilityUnlocks abilityUnlocks;
    private PlayerLightResource playerLightResource;
    private PlayerLightChannel playerLightChannel;

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
            // The aiming indicator is detached because the script controls its
            // world position and rotation independently from the player.
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
            // Beam Visual should be a neutral pivot object. The imported VFX
            // remains as its child so it can keep its required local rotation.
            beamVisual.transform.SetParent(
                null,
                true
            );

            beamVisual.transform.localScale =
                Vector3.one;

            // The prefab stores its Particle System inside the hierarchy rather
            // than directly on the Beam Visual pivot.
            beamParticleSystem =
                beamVisual.GetComponentInChildren<ParticleSystem>(
                    true
                );

            if (beamParticleSystem == null)
            {
                Debug.LogError(
                    "LightBeamController could not find a ParticleSystem inside " +
                    beamVisual.name +
                    "."
                );
            }
            else
            {
                // The script controls playback so the imported effect does not
                // remain active before the player fires the ability.
                beamParticleSystem.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );

                Debug.Log(
                    "LightBeamController found Particle System: " +
                    beamParticleSystem.gameObject.name
                );
            }

            beamVisual.SetActive(false);
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
            " light."
        );
    }

    private IEnumerator BeamRoutine()
    {
        isBeamActive = true;

        if (beamVisual != null)
        {
            beamVisual.SetActive(true);

            if (beamParticleSystem != null)
            {
                // Clearing existing particles ensures every shot begins from
                // the start of the effect instead of continuing an older shot.
                beamParticleSystem.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );

                beamParticleSystem.Play(
                    true
                );

                Debug.Log(
                    "Light Beam Particle System started."
                );
            }
        }

        Debug.Log(
            "Light beam active"
        );

        float timer = 0f;

        while (timer < beamActiveDuration)
        {
            UpdateBeamPreview(
                beamVisual
            );

            DispelDarknessInBeam();
            CheckLightGateInBeam();

            timer +=
                beamCheckInterval;

            yield return new WaitForSeconds(
                beamCheckInterval
            );
        }

        isBeamActive = false;

        if (beamVisual != null)
        {
            if (beamParticleSystem != null)
            {
                // The effect is cleared when gameplay detection ends so the
                // visible Beam cannot remain after it stops affecting darkness.
                beamParticleSystem.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }

            beamVisual.SetActive(false);
        }

        beamCoroutine = null;

        Debug.Log(
            "Light beam ended"
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

        if (visualObject == null)
        {
            return;
        }

        if (visualObject == beamVisual)
        {
            // The fired VFX pivot remains exactly at Beam Origin so the visible
            // Beam starts from the player instead of being centred over them.
            visualObject.transform.position =
                originPosition;

            // The neutral pivot rotates towards the mouse. Any special imported
            // rotation, such as X -90, stays on the child VFX object.
            visualObject.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );

            // The Particle System is not stretched or moved halfway forwards
            // because it needs to emit outward from the Beam Origin.
            visualObject.transform.localScale =
                Vector3.one;
        }
        else
        {
            // The aiming indicator uses a centred sprite, so it must sit halfway
            // between the player and the final Beam endpoint.
            visualObject.transform.position =
                boxCenter;

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
        }
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
            "Light beam cooldown started"
        );

        yield return new WaitForSeconds(
            beamCooldown
        );

        isOnCooldown = false;
        cooldownCoroutine = null;

        Debug.Log(
            "Light beam cooldown ended"
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
        // This remains available so existing Player Input event references do
        // not break even though the Beam now uses direct mouse aiming.
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