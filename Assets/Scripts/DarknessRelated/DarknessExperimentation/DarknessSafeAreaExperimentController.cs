using UnityEngine;

public class DarknessSafeAreaExperimentController : MonoBehaviour
{
    [Header("Real Light Ability References")]
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Player / Beam References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform beamVisualTransform;

    [Header("Beam Safe Area")]
    [SerializeField] private float beamHalfWidth = 0.75f;
    [SerializeField] private float beamRange = 6f;

    [Header("Beam Reform Delay")]
    [SerializeField] private float beamSafeAreaHoldDuration = 2f;

    // The last fired Beam corridor is stored so darkness can remain logically
    // displaced for a short period after the visible Beam has disappeared.
    private Vector2 storedBeamOrigin;
    private Vector2 storedBeamDirection = Vector2.right;

    private float beamHoldTimer = 0f;
    private bool wasBeamActive = false;

    private void Update()
    {
        UpdateBeamSafeArea();
    }

    public bool IsPositionSafe(
        Vector2 worldPosition
    )
    {
        if (IsInsideActiveBurst(worldPosition))
        {
            return true;
        }

        if (IsInsideBeamSafeArea(worldPosition))
        {
            return true;
        }

        return false;
    }

    private bool IsInsideActiveBurst(
        Vector2 worldPosition
    )
    {
        if (
            lightBurstController == null ||
            playerTransform == null ||
            !lightBurstController.IsBurstActive()
        )
        {
            return false;
        }

        float currentBurstRadius =
            lightBurstController.GetCurrentBurstRadius();

        float distanceFromBurstCentre =
            Vector2.Distance(
                worldPosition,
                playerTransform.position
            );

        // The real live Burst radius is used so the gameplay safe area expands
        // alongside the actual ability instead of becoming full-sized instantly.
        return
            distanceFromBurstCentre <=
            currentBurstRadius;
    }

    private void UpdateBeamSafeArea()
    {
        if (
            lightBeamController == null ||
            playerTransform == null ||
            beamVisualTransform == null
        )
        {
            wasBeamActive = false;
            beamHoldTimer = 0f;

            return;
        }

        bool beamActive =
            lightBeamController.IsBeamActive();

        if (
            beamActive &&
            !wasBeamActive
        )
        {
            // The Beam's trajectory is captured once when firing begins because
            // the real Beam itself is locked rather than following later Player
            // movement during or after the shot.
            storedBeamOrigin =
                playerTransform.position;

            storedBeamDirection =
                beamVisualTransform.right.normalized;

            beamHoldTimer =
                beamSafeAreaHoldDuration;
        }

        if (beamActive)
        {
            // Keep refreshing the hold timer while the Beam is still visible so
            // the configured delay starts only after the actual shot finishes.
            beamHoldTimer =
                beamSafeAreaHoldDuration;
        }
        else if (beamHoldTimer > 0f)
        {
            beamHoldTimer -=
                Time.deltaTime;
        }

        wasBeamActive =
            beamActive;
    }

    private bool IsInsideBeamSafeArea(
        Vector2 worldPosition
    )
    {
        bool beamCurrentlyActive =
            lightBeamController != null &&
            lightBeamController.IsBeamActive();

        bool beamSafeAreaActive =
            beamCurrentlyActive ||
            beamHoldTimer > 0f;

        if (!beamSafeAreaActive)
        {
            return false;
        }

        Vector2 fromBeamOrigin =
            worldPosition -
            storedBeamOrigin;

        float distanceAlongBeam =
            Vector2.Dot(
                fromBeamOrigin,
                storedBeamDirection
            );

        // Darkness behind the firing point or beyond the Beam's maximum range
        // remains dangerous because the actual ability never illuminated it.
        if (
            distanceAlongBeam < 0f ||
            distanceAlongBeam > beamRange
        )
        {
            return false;
        }

        Vector2 perpendicularDirection =
            new Vector2(
                -storedBeamDirection.y,
                storedBeamDirection.x
            );

        float distanceAcrossBeam =
            Mathf.Abs(
                Vector2.Dot(
                    fromBeamOrigin,
                    perpendicularDirection
                )
            );

        // This continuous mathematical corridor is the important difference
        // from the earlier individual-darkness collider experiments. Every
        // point inside the Beam path is treated consistently as safe.
        return
            distanceAcrossBeam <=
            beamHalfWidth;
    }
}