using UnityEngine;

public class DarknessRepulsionExperimentController : MonoBehaviour
{
    [Header("Real Light Ability References")]
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Ability Transform References")]
    [SerializeField] private Transform playerTransform;

    // Assign the root GameObject of the fired Beam visual here.
    // LightBeamController rotates that object to the locked firing angle,
    // allowing this experiment to read the real Beam direction without
    // modifying the production Beam controller.
    [SerializeField] private Transform beamVisualTransform;

    // The experimental pieces are cached once rather than searched for every
    // frame because their population is fixed during this controlled test.
    private DarknessRepulsionTest[] darknessPieces;

    private void Awake()
    {
        darknessPieces =
            GetComponentsInChildren<DarknessRepulsionTest>(
                true
            );
    }

    private void Update()
    {
        UpdateRepulsionExperiment();
    }

    private void UpdateRepulsionExperiment()
    {
        foreach (
            DarknessRepulsionTest darknessPiece
            in darknessPieces
        )
        {
            if (darknessPiece == null)
            {
                continue;
            }

            // Beam gets priority when both abilities somehow overlap because
            // its directional behaviour is more specific than Burst's radial
            // response and should remain readable during the experiment.
            if (
                IsDarknessInsideActiveBeam(
                    darknessPiece
                )
            )
            {
                ApplyBeamRepulsion(
                    darknessPiece
                );

                continue;
            }

            if (
                IsDarknessInsideActiveBurst(
                    darknessPiece
                )
            )
            {
                darknessPiece.ApplyBurstRepulsion(
                    playerTransform.position
                );

                continue;
            }

            // A piece returns home only when neither real light ability is
            // currently affecting it.
            darknessPiece.StopRepulsion();
        }
    }

    private bool IsDarknessInsideActiveBurst(
        DarknessRepulsionTest darknessPiece
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

        // Using the controller's live radius means the darkness reacts as the
        // real Burst expands rather than jumping instantly to maximum range.
        return
            distanceFromPlayer <=
            currentBurstRadius;
    }

    private bool IsDarknessInsideActiveBeam(
        DarknessRepulsionTest darknessPiece
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

        // This uses the Beam controller's existing overlap calculation so the
        // experiment responds to the same expanding gameplay Beam region rather
        // than creating a separate approximation of the ability.
        return
            lightBeamController
                .IsBoundsOverlappingActiveBeam(
                    darknessCollider.bounds
                );
    }

    private void ApplyBeamRepulsion(
        DarknessRepulsionTest darknessPiece
    )
    {
        if (
            playerTransform == null ||
            beamVisualTransform == null
        )
        {
            return;
        }

        // The fired Beam visual is rotated by LightBeamController to match the
        // locked trajectory, so its local right direction gives this experiment
        // the direction of the actual shot.
        Vector2 beamDirection =
            beamVisualTransform.right;

        darknessPiece.ApplyBeamRepulsion(
            playerTransform.position,
            beamDirection
        );
    }
}
