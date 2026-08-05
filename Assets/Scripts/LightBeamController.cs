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

        // The aiming preview is recalculated only while aiming. Once the player
        // fires, Update stops changing the Beam trajectory.
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

            // The yellow Beam is positioned once using the locked indicator
            // result instead of being recalculated from the mouse every frame.
            ApplyLockedBeamVisual();
        }

        // Gameplay detection uses the same locked dimensions as the visual so
        // darkness dispelling remains aligned with the yellow Beam.
        ApplyLockedBeamCollisionValues();

        Debug.Log(
            "Light beam active with locked trajectory."
        );

        float timer = 0f;

        while (timer < beamActiveDuration)
        {
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

    private void ApplyLockedBeamVisual()
    {
        if (beamVisual == null)
        {
            return;
        }

        // The fired visual copies the exact origin and angle used by the final
        // aiming indicator rather than following the current mouse position.
        beamVisual.transform.position =
            lockedBeamOrigin;

        beamVisual.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                lockedBeamAngle
            );

        beamVisual.transform.localScale =
            new Vector3(
                lockedBeamSize.x,
                lockedBeamSize.y,
                1f
            );

        // The Beam Visual uses the same centred-sprite arrangement as the
        // indicator, keeping both visuals the same size, shape and length.
        beamVisual.transform.Translate(
            Vector3.right *
            (lockedBeamSize.x * 0.5f),
            Space.Self
        );
    }

    private void ApplyLockedBeamCollisionValues()
    {
        // Existing darkness and gate checks use the lastBeam values. Replacing
        // them with the locked result keeps gameplay fixed throughout the shot.
        lastBeamCenter =
            lockedBeamCenter;

        lastBeamSize =
            lockedBeamSize;

        lastBeamDirection =
            lockedBeamDirection;

        lastBeamAngle =
            lockedBeamAngle;
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