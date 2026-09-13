using UnityEngine;

public class DarknessFlowTest : MonoBehaviour
{
    [Header("Flow Settings")]
    [SerializeField] private float flowSpeed = 6f;
    [SerializeField] private float flowAlongBoundaryDistance = 1.5f;
    [SerializeField] private float burstBoundaryOffset = 0.15f;
    [SerializeField] private float beamBoundaryOffset = 0.15f;

    [Header("Return Settings")]
    [SerializeField] private float returnSpeed = 3f;

    // Each piece keeps its original position so the experiment tests temporary
    // light-driven movement rather than permanently changing the darkness layout.
    private Vector3 originalPosition;

    private Vector3 targetPosition;
    private bool isFlowing = false;

    private void Awake()
    {
        originalPosition = transform.position;
        targetPosition = originalPosition;
    }

    private void Update()
    {
        if (isFlowing)
        {
            // A fixed speed keeps this low-fidelity test predictable so we can
            // compare Flow against Repulsion and Compression fairly.
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                flowSpeed * Time.deltaTime
            );
        }
        else
        {
            // All experiments use the same simple return behaviour for now so
            // the main variable remains the response while light is active.
            transform.position = Vector3.MoveTowards(
                transform.position,
                originalPosition,
                returnSpeed * Time.deltaTime
            );
        }
    }

    public void ApplyBurstFlow(
        Vector3 burstOrigin,
        float currentBurstRadius
    )
    {
        Vector3 radialDirection =
            originalPosition - burstOrigin;

        radialDirection.z = 0f;

        if (radialDirection.sqrMagnitude <= 0.001f)
        {
            radialDirection = Vector3.up;
        }

        radialDirection.Normalize();

        // First move the piece to the outside of the Burst boundary so it does
        // not remain inside the illuminated area.
        Vector3 boundaryPosition =
            burstOrigin +
            radialDirection *
            (
                currentBurstRadius +
                burstBoundaryOffset
            );

        // The tangent direction makes the darkness travel around the circular
        // Burst instead of simply moving directly away from the player.
        Vector3 tangentDirection =
            new Vector3(
                -radialDirection.y,
                radialDirection.x,
                0f
            );

        if (radialDirection.y < 0f)
        {
            tangentDirection *= -1f;
        }

        if (
            Mathf.Abs(radialDirection.y) < 0.05f &&
            radialDirection.x < 0f
        )
        {
            tangentDirection *= -1f;
        }

        targetPosition =
            boundaryPosition +
            tangentDirection *
            flowAlongBoundaryDistance;

        targetPosition.z = originalPosition.z;

        isFlowing = true;
    }

    public void ApplyBeamFlow(
        Vector2 beamOrigin,
        Vector2 beamDirection,
        float beamHalfWidth
    )
    {
        Vector2 normalisedBeamDirection =
            beamDirection.normalized;

        // This vector represents the two sides of the Beam. The darkness first
        // moves out of the Beam corridor, then travels along its edge.
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

        float sideDirection =
            sideOfBeam >= 0f
                ? 1f
                : -1f;

        // Find the corresponding point on the Beam axis so the piece keeps its
        // approximate position along the length of the shot.
        Vector2 pointOnBeamAxis =
            beamOrigin +
            normalisedBeamDirection *
            distanceAlongBeam;

        Vector2 beamEdgePosition =
            pointOnBeamAxis +
            perpendicularDirection *
            sideDirection *
            (
                beamHalfWidth +
                beamBoundaryOffset
            );

        // After reaching the Beam edge, the darkness also travels forwards
        // along the Beam direction. This tests whether the darkness appears to
        // flow along the light corridor instead of merely being pushed aside.
        Vector2 flowedPosition =
            beamEdgePosition +
            normalisedBeamDirection *
            flowAlongBoundaryDistance;

        targetPosition =
            new Vector3(
                flowedPosition.x,
                flowedPosition.y,
                originalPosition.z
            );

        isFlowing = true;
    }

    public void StopFlow()
    {
        // Once neither ability affects this piece, it returns to its original
        // position instead of snapping back instantly.
        isFlowing = false;
        targetPosition = originalPosition;
    }

    public Vector3 GetOriginalPosition()
    {
        // The controller uses the untouched position when checking whether the
        // real abilities have reached this darkness piece.
        return originalPosition;
    }
}