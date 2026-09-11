using UnityEngine;

public class DarknessRippleTest : MonoBehaviour
{
    [Header("Ripple Movement")]
    [SerializeField] private float movementDistance = 1.5f;
    [SerializeField] private float movementSpeed = 6f;

    [Header("Ripple Propagation")]
    [SerializeField] private float neighbourRadius = 1.2f;
    [SerializeField] private float propagationDelay = 0.08f;

    [Header("Return Settings")]
    [SerializeField] private float returnSpeed = 3f;

    // Every piece remembers where it belongs so the ripple can temporarily
    // disturb the mass without permanently changing the darkness layout.
    private Vector3 originalPosition;
    private Vector3 targetPosition;

    private bool isReacting = false;
    private bool hasPropagatedThisActivation = false;

    // A delayed propagation time makes the response travel through the group
    // rather than causing every neighbouring piece to move on the same frame.
    private float propagationTimer = 0f;

    private Vector2 currentReactionDirection;

    private void Awake()
    {
        originalPosition = transform.position;
        targetPosition = originalPosition;
    }

    private void Update()
    {
        if (isReacting)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                movementSpeed * Time.deltaTime
            );

            if (!hasPropagatedThisActivation)
            {
                propagationTimer -= Time.deltaTime;

                if (propagationTimer <= 0f)
                {
                    PropagateToNeighbours();
                    hasPropagatedThisActivation = true;
                }
            }
        }
        else
        {
            // The same return style is kept across the experiments so the
            // comparison remains focused on how darkness reacts to the light.
            transform.position = Vector3.MoveTowards(
                transform.position,
                originalPosition,
                returnSpeed * Time.deltaTime
            );
        }
    }

    public void StartRipple(
        Vector2 reactionDirection
    )
    {
        // A piece only starts once per active ripple chain. This prevents
        // neighbouring pieces from repeatedly restarting each other forever.
        if (isReacting)
        {
            return;
        }

        currentReactionDirection =
            reactionDirection.normalized;

        if (currentReactionDirection.sqrMagnitude <= 0.001f)
        {
            currentReactionDirection = Vector2.up;
        }

        targetPosition =
            originalPosition +
            (Vector3)(
                currentReactionDirection *
                movementDistance
            );

        isReacting = true;
        hasPropagatedThisActivation = false;
        propagationTimer = propagationDelay;
    }

    private void PropagateToNeighbours()
    {
        // Nearby experimental darkness pieces are treated as connected parts
        // of the same mass so the reaction can spread as a visible wave.
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                originalPosition,
                neighbourRadius
            );

        foreach (Collider2D hit in hits)
        {
            DarknessRippleTest neighbour =
                hit.GetComponent<DarknessRippleTest>();

            if (
                neighbour == null ||
                neighbour == this
            )
            {
                continue;
            }

            // The neighbour moves generally in the same direction as the piece
            // that activated it. This helps the ripple read as one travelling
            // disturbance rather than random independent movement.
            neighbour.StartRipple(
                currentReactionDirection
            );
        }
    }

    public void StopRipple()
    {
        isReacting = false;
        targetPosition = originalPosition;

        // Reset propagation state so the piece can participate normally the
        // next time Burst or Beam starts a new ripple chain.
        hasPropagatedThisActivation = false;
        propagationTimer = 0f;
    }

    public Vector3 GetOriginalPosition()
    {
        return originalPosition;
    }

    public bool IsReacting()
    {
        return isReacting;
    }
}