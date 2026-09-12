using UnityEngine;

public class DarknessFlowExperimentController : MonoBehaviour
{
    [Header("Real Light Ability References")]
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Ability Transform References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform beamVisualTransform;

    [Header("Beam Flow")]
    [SerializeField] private float beamHalfWidth = 0.75f;

    // The darkness pieces are cached once because this controlled experiment
    // uses a fixed arrangement throughout the test.
    private DarknessFlowTest[] darknessPieces;

    private void Awake()
    {
        darknessPieces =
            GetComponentsInChildren<DarknessFlowTest>(
                true
            );
    }

    private void Update()
    {
        UpdateFlowExperiment();
    }

    private void UpdateFlowExperiment()
    {
        foreach (
            DarknessFlowTest darknessPiece
            in darknessPieces
        )
        {
            if (darknessPiece == null)
            {
                continue;
            }

            // Beam is checked first so its directional behaviour takes priority
            // if both abilities somehow overlap the same piece.
            if (
                IsInsideActiveBeam(
                    darknessPiece
                )
            )
            {
                ApplyBeamFlow(
                    darknessPiece
                );

                continue;
            }

            if (
                IsInsideActiveBurst(
                    darknessPiece
                )
            )
            {
                darknessPiece.ApplyBurstFlow(
                    playerTransform.position,
                    lightBurstController.GetCurrentBurstRadius()
                );

                continue;
            }

            darknessPiece.StopFlow();
        }
    }

    private bool IsInsideActiveBurst(
        DarknessFlowTest darknessPiece
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

        float distanceFromPlayer =
            Vector2.Distance(
                playerTransform.position,
                darknessPiece.GetOriginalPosition()
            );

        // Using the live Burst radius keeps this experiment aligned with the
        // real expanding Light Burst rather than testing against a fake radius.
        return
            distanceFromPlayer <=
            currentBurstRadius;
    }

    private bool IsInsideActiveBeam(
        DarknessFlowTest darknessPiece
    )
    {
        if (
            lightBeamController == null ||
            !lightBeamController.IsBeamActive()
        )
        {
            return false;
        }

        Collider2D darknessCollider =
            darknessPiece.GetComponent<Collider2D>();

        if (darknessCollider == null)
        {
            return false;
        }

        // This intentionally uses the same Beam overlap method as the earlier
        // experiments so we can observe the current Beam behaviour consistently
        // before deciding how its darkness detection should be redesigned.
        return
            lightBeamController
                .IsBoundsOverlappingActiveBeam(
                    darknessCollider.bounds
                );
    }

    private void ApplyBeamFlow(
        DarknessFlowTest darknessPiece
    )
    {
        if (
            playerTransform == null ||
            beamVisualTransform == null
        )
        {
            return;
        }

        // The real fired Beam visual already carries the locked firing angle, so
        // its local right direction gives this experiment the current Beam path.
        Vector2 beamDirection =
            beamVisualTransform.right;

        darknessPiece.ApplyBeamFlow(
            playerTransform.position,
            beamDirection,
            beamHalfWidth
        );
    }
}