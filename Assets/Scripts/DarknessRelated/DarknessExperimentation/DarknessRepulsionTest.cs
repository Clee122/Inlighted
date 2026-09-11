using UnityEngine;

public class DarknessRepulsionTest : MonoBehaviour
{
    [Header("Repulsion Settings")]
    [SerializeField] private float burstRepulsionDistance = 2f;
    [SerializeField] private float beamRepulsionDistance = 2f;
    [SerializeField] private float repulsionSpeed = 6f;

    [Header("Return Settings")]
    [SerializeField] private float returnSpeed = 3f;

    // Every experimental darkness piece remembers its starting position.
    // This lets all of the behavioural tests share the same basic rule:
    // light displaces the darkness, then it returns home afterwards.
    private Vector3 originalPosition;

    // The desired displaced position is calculated by the bridge script.
    // Keeping detection outside this component means this script only needs
    // to represent the behaviour of one darkness piece.
    private Vector3 targetPosition;

    private bool isBeingRepelled = false;

    private void Awake()
    {
        originalPosition = transform.position;
        targetPosition = originalPosition;
    }

    private void Update()
    {
        if (isBeingRepelled)
        {
            // MoveTowards gives us intentionally simple and predictable motion
            // for the first low-fidelity experiment. More organic movement can
            // be tested later only if basic repulsion proves successful.
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                repulsionSpeed * Time.deltaTime
            );
        }
        else
        {
            // All darkness pieces currently use the same return behaviour so
            // differences between experiments come from their light reaction
            // rather than from different reformation rules.
            transform.position = Vector3.MoveTowards(
                transform.position,
                originalPosition,
                returnSpeed * Time.deltaTime
            );
        }
    }

    public void ApplyBurstRepulsion(
        Vector3 burstOrigin
    )
    {
        // Burst behaves like an explosion of light, so every darkness piece
        // travels radially away from the player's current position.
        Vector3 directionAwayFromBurst =
            originalPosition - burstOrigin;

        directionAwayFromBurst.z = 0f;

        if (directionAwayFromBurst.sqrMagnitude <= 0.001f)
        {
            directionAwayFromBurst = Vector3.up;
        }

        directionAwayFromBurst.Normalize();

        targetPosition =
            originalPosition +
            directionAwayFromBurst *
            burstRepulsionDistance;

        isBeingRepelled = true;
    }

    public void ApplyBeamRepulsion(
    Vector2 beamOrigin,
    Vector2 beamDirection
)
    {
        // Beam pushes darkness sideways rather than forwards. This gives Beam a
        // different response from Burst and tests whether splitting the darkness
        // communicates a directional corridor more clearly.
        Vector2 perpendicularDirection =
            new Vector2(
                -beamDirection.y,
                beamDirection.x
            ).normalized;

        Vector2 fromBeamToPiece =
            (Vector2)originalPosition -
            beamOrigin;

        // The dot product identifies which side of the Beam axis the darkness
        // originally occupied so pieces above and below move apart.
        float side =
            Vector2.Dot(
                fromBeamToPiece,
                perpendicularDirection
            );

        if (side < 0f)
        {
            perpendicularDirection *= -1f;
        }

        targetPosition =
            originalPosition +
            (Vector3)(
                perpendicularDirection *
                beamRepulsionDistance
            );

        isBeingRepelled = true;
    }

    public void StopRepulsion()
    {
        // Removing the light force does not teleport the piece back.
        // Update() instead allows it to visibly reform by travelling home.
        isBeingRepelled = false;
        targetPosition = originalPosition;
    }

    public Vector3 GetOriginalPosition()
    {
        // The bridge needs the untouched starting position when checking
        // whether the expanding Burst has actually reached this piece yet.
        return originalPosition;
    }
}