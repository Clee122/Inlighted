using UnityEngine;

public class DarknessRippleExperimentController : MonoBehaviour
{
    [Header("Real Light Ability References")]
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Ability Transform References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform beamVisualTransform;

    // The experimental pieces are cached because this controlled setup uses
    // a fixed darkness formation throughout the comparison.
    private DarknessRippleTest[] darknessPieces;

    private bool wasBurstActive = false;
    private bool wasBeamActive = false;

    private void Awake()
    {
        darknessPieces =
            GetComponentsInChildren<DarknessRippleTest>(
                true
            );
    }

    private void Update()
    {
        HandleBurstRipple();
        HandleBeamRipple();
        HandleRippleReset();
    }

    private void HandleBurstRipple()
    {
        if (
            lightBurstController == null ||
            playerTransform == null
        )
        {
            return;
        }

        bool burstActive =
            lightBurstController.IsBurstActive();

        if (!burstActive)
        {
            wasBurstActive = false;
            return;
        }

        float currentBurstRadius =
            lightBurstController.GetCurrentBurstRadius();

        foreach (
            DarknessRippleTest darknessPiece
            in darknessPieces
        )
        {
            if (
                darknessPiece == null ||
                darknessPiece.IsReacting()
            )
            {
                continue;
            }

            float distanceFromPlayer =
                Vector2.Distance(
                    playerTransform.position,
                    darknessPiece.GetOriginalPosition()
                );

            if (distanceFromPlayer > currentBurstRadius)
            {
                continue;
            }

            // Only darkness directly reached by the real expanding Burst starts
            // the chain. From there, neighbouring pieces spread the reaction.
            Vector2 directionAwayFromBurst =
                (Vector2)darknessPiece.GetOriginalPosition() -
                (Vector2)playerTransform.position;

            darknessPiece.StartRipple(
                directionAwayFromBurst
            );
        }

        wasBurstActive = true;
    }

    private void HandleBeamRipple()
    {
        if (
            lightBeamController == null ||
            playerTransform == null ||
            beamVisualTransform == null
        )
        {
            return;
        }

        bool beamActive =
            lightBeamController.IsBeamActive();

        if (!beamActive)
        {
            wasBeamActive = false;
            return;
        }

        Vector2 beamDirection =
            beamVisualTransform.right.normalized;

        Vector2 perpendicularDirection =
            new Vector2(
                -beamDirection.y,
                beamDirection.x
            );

        foreach (
            DarknessRippleTest darknessPiece
            in darknessPieces
        )
        {
            if (
                darknessPiece == null ||
                darknessPiece.IsReacting()
            )
            {
                continue;
            }

            Collider2D darknessCollider =
                darknessPiece.GetComponent<Collider2D>();

            if (darknessCollider == null)
            {
                continue;
            }

            // The initial Beam contact deliberately uses the same real Beam
            // overlap behaviour as the earlier experiments. This lets us see
            // whether ripple propagation can compensate for fragmented direct
            // hit detection before redesigning the Beam itself.
            bool isInsideBeam =
                lightBeamController
                    .IsBoundsOverlappingActiveBeam(
                        darknessCollider.bounds
                    );

            if (!isInsideBeam)
            {
                continue;
            }

            Vector2 fromPlayerToPiece =
                (Vector2)darknessPiece.GetOriginalPosition() -
                (Vector2)playerTransform.position;

            float sideOfBeam =
                Vector2.Dot(
                    fromPlayerToPiece,
                    perpendicularDirection
                );

            Vector2 rippleDirection =
                sideOfBeam >= 0f
                    ? perpendicularDirection
                    : -perpendicularDirection;

            darknessPiece.StartRipple(
                rippleDirection
            );
        }

        wasBeamActive = true;
    }

    private void HandleRippleReset()
    {
        if (
            lightBurstController == null ||
            lightBeamController == null
        )
        {
            return;
        }

        bool anyLightActive =
            lightBurstController.IsBurstActive() ||
            lightBeamController.IsBeamActive();

        if (anyLightActive)
        {
            return;
        }

        // Once both real abilities have ended, the entire connected mass is
        // released so every piece can reform towards its original position.
        foreach (
            DarknessRippleTest darknessPiece
            in darknessPieces
        )
        {
            if (darknessPiece != null)
            {
                darknessPiece.StopRipple();
            }
        }

        wasBurstActive = false;
        wasBeamActive = false;
    }
}