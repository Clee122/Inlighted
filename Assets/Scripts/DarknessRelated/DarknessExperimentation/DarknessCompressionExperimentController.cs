using UnityEngine;

public class DarknessCompressionExperimentController : MonoBehaviour
{
    [Header("Real Light Ability References")]
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Ability Transform References")]
    [SerializeField] private Transform playerTransform;

    // This should reference the same fired Beam visual used by the real
    // LightBeamController. Its rotation gives this experiment the direction
    // of the locked Beam without modifying the production ability script.
    [SerializeField] private Transform beamVisualTransform;

    [Header("Beam Compression")]
    [SerializeField] private float beamHalfWidth = 0.75f;

    // The test pieces are cached because this controlled experiment uses a
    // fixed group of darkness objects and does not need to search every frame.
    private DarknessCompressionTest[] darknessPieces;

    private void Awake()
    {
        darknessPieces =
            GetComponentsInChildren<DarknessCompressionTest>(
                true
            );
    }

    private void Update()
    {
        UpdateCompressionExperiment();
    }

    private void UpdateCompressionExperiment()
    {
        foreach (
            DarknessCompressionTest darknessPiece
            in darknessPieces
        )
        {
            if (darknessPiece == null)
            {
                continue;
            }

            // Beam receives priority if both abilities somehow affect the same
            // piece because its directional corridor is the more specific
            // behaviour being tested.
            if (
                IsInsideActiveBeam(
                    darknessPiece
                )
            )
            {
                ApplyBeamCompression(
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
                ApplyBurstCompression(
                    darknessPiece
                );

                continue;
            }

            // Darkness reforms only when neither real light ability currently
            // affects that piece.
            darknessPiece.StopCompression();
        }
    }

    private bool IsInsideActiveBurst(
        DarknessCompressionTest darknessPiece
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

        float distanceFromBurst =
            Vector2.Distance(
                playerTransform.position,
                darknessPiece.GetOriginalPosition()
            );

        // Using the live Burst radius means pieces begin compressing only when
        // the actual expanding ability reaches their original location.
        return
            distanceFromBurst <=
            currentBurstRadius;
    }

    private void ApplyBurstCompression(
        DarknessCompressionTest darknessPiece
    )
    {
        float currentBurstRadius =
            lightBurstController.GetCurrentBurstRadius();

        darknessPiece.ApplyBurstCompression(
            playerTransform.position,
            currentBurstRadius
        );
    }

    private bool IsInsideActiveBeam(
        DarknessCompressionTest darknessPiece
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

        // Reusing the real Beam overlap test keeps this experiment synchronised
        // with the same expanding gameplay region used by the production Beam.
        return
            lightBeamController
                .IsBoundsOverlappingActiveBeam(
                    darknessCollider.bounds
                );
    }

    private void ApplyBeamCompression(
        DarknessCompressionTest darknessPiece
    )
    {
        if (
            playerTransform == null ||
            beamVisualTransform == null
        )
        {
            return;
        }

        // The fired Beam visual rotates to the real locked firing direction,
        // allowing the experiment to calculate which sides of the corridor
        // the darkness should compress towards.
        Vector2 beamDirection =
            beamVisualTransform.right;

        darknessPiece.ApplyBeamCompression(
            playerTransform.position,
            beamDirection,
            beamHalfWidth
        );
    }
}
