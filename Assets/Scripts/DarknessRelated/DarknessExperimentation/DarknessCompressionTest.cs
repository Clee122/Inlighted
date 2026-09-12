using UnityEngine;

public class DarknessCompressionTest : MonoBehaviour
{
    [Header("Compression Movement")]
    [SerializeField] private float compressionSpeed = 6f;

    [Header("Return Settings")]
    [SerializeField] private float returnSpeed = 3f;

    [Header("Boundary Spacing")]
    [SerializeField] private float burstBoundaryOffset = 0.15f;
    [SerializeField] private float beamBoundaryOffset = 0.15f;

    // Every darkness piece stores its starting position because the experiment
    // is testing temporary deformation rather than permanently rearranging the
    // darkness formation.
    private Vector3 originalPosition;

    // The controller calculates where the piece should be compressed towards.
    // Keeping that calculation separate from movement makes it easier to reuse
    // the same return behaviour across different darkness experiments.
    private Vector3 targetPosition;

    private bool isBeingCompressed = false;

    private void Awake()
    {
        originalPosition = transform.position;
        targetPosition = originalPosition;
    }

    private void Update()
    {
        if (isBeingCompressed)
        {
            // A predictable movement speed is intentionally used during this
            // low-fidelity test so we evaluate compression itself rather than
            // additional animation or easing effects.
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                compressionSpeed * Time.deltaTime
            );
        }
        else
        {
            // The same simple return behaviour used by the repulsion experiment
            // gives us a fairer comparison between the two main reactions.
            transform.position = Vector3.MoveTowards(
                transform.position,
                originalPosition,
                returnSpeed * Time.deltaTime
            );
        }
    }

    public void ApplyBurstCompression(
        Vector3 burstOrigin,
        float currentBurstRadius
    )
    {
        // Burst compression places affected darkness around the current edge
        // of the expanding light rather than simply throwing pieces farther
        // away. This tests whether darkness appears to bunch against the light.
        Vector3 directionFromBurst =
            originalPosition - burstOrigin;

        directionFromBurst.z = 0f;

        if (directionFromBurst.sqrMagnitude <= 0.001f)
        {
            directionFromBurst = Vector3.up;
        }

        directionFromBurst.Normalize();

        targetPosition =
            burstOrigin +
            directionFromBurst *
            (
                currentBurstRadius +
                burstBoundaryOffset
            );

        targetPosition.z = originalPosition.z;

        isBeingCompressed = true;
    }

    public void ApplyBeamCompression(
        Vector2 beamOrigin,
        Vector2 beamDirection,
        float beamHalfWidth
    )
    {
        Vector2 normalisedBeamDirection =
            beamDirection.normalized;

        // This perpendicular vector represents the two sides of the Beam.
        // Darkness will be compressed towards whichever side it originally
        // occupied instead of being pushed forwards along the Beam.
        Vector2 perpendicularDirection =
            new Vector2(
                -normalisedBeamDirection.y,
                normalisedBeamDirection.x
            );

        Vector2 fromBeamOriginToPiece =
            (Vector2)originalPosition -
            beamOrigin;

        float distanceAlongBeam =
            Vector2.Dot(
                fromBeamOriginToPiece,
                normalisedBeamDirection
            );

        float sideOfBeam =
            Vector2.Dot(
                fromBeamOriginToPiece,
                perpendicularDirection
            );

        // Preserve how far along the Beam the darkness originally sits. Only
        // its sideways position changes, producing a compressed corridor
        // instead of moving every piece towards one common point.
        Vector2 pointOnBeamAxis =
            beamOrigin +
            normalisedBeamDirection *
            distanceAlongBeam;

        float sideDirection =
            sideOfBeam >= 0f
                ? 1f
                : -1f;

        Vector2 compressedPosition =
            pointOnBeamAxis +
            perpendicularDirection *
            sideDirection *
            (
                beamHalfWidth +
                beamBoundaryOffset
            );

        targetPosition =
            new Vector3(
                compressedPosition.x,
                compressedPosition.y,
                originalPosition.z
            );

        isBeingCompressed = true;
    }

    public void StopCompression()
    {
        // Removing the light force allows the piece to visibly reform by
        // travelling back rather than instantly snapping to its starting point.
        isBeingCompressed = false;
        targetPosition = originalPosition;
    }

    public Vector3 GetOriginalPosition()
    {
        // The experiment controller uses the untouched position when deciding
        // whether the real expanding Burst has reached this piece.
        return originalPosition;
    }
}