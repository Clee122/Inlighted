using System.Collections.Generic;
using UnityEngine;

public class DarknessPolygonFlowGroupTest : MonoBehaviour
{
    [Header("Real Light Ability References")]
    [SerializeField] private LightBurstController lightBurstController;
    [SerializeField] private LightBeamController lightBeamController;

    [Header("Player / Beam References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform beamVisualTransform;

    [Header("Burst Flow")]
    [SerializeField] private float burstOutwardPushDistance = 1.5f;
    [SerializeField] private float burstTangentFlowDistance = 0.75f;

    [Header("Beam Flow")]
    [SerializeField] private float beamSidePushDistance = 1.5f;
    [SerializeField] private float beamForwardFlowDistance = 0.5f;
    [SerializeField] private float beamHalfWidth = 0.75f;

    [Header("Movement")]
    [SerializeField] private float deformationSpeed = 5f;
    [SerializeField] private float returnSpeed = 2.5f;

    [Header("Beam Reform Delay")]
    [SerializeField] private float beamHoldDuration = 1.5f;

    // Each child polygon needs its own stored copy of the original collider
    // points so every darkness section can deform independently and still
    // return exactly to the shape it had before the light affected it.
    private class PolygonState
    {
        public PolygonCollider2D collider;
        public Vector2[] originalPoints;
        public Vector2[] targetPoints;
    }

    private readonly List<PolygonState> polygonStates =
        new List<PolygonState>();

    // Beam deformation is intentionally held for a short period after the
    // visible Beam ends because the experiment is testing whether a lingering
    // corridor feels better than immediately reforming darkness.
    private float beamHoldTimer = 0f;
    private bool beamWasActive = false;

    private void Awake()
    {
        CacheChildPolygons();
    }

    private void Update()
    {
        UpdateBeamHoldState();

        bool burstActive =
            lightBurstController != null &&
            lightBurstController.IsBurstActive();

        bool beamInfluenceActive =
            IsBeamInfluenceActive();

        foreach (PolygonState state in polygonStates)
        {
            if (
                state.collider == null ||
                state.originalPoints == null
            )
            {
                continue;
            }

            // Beam is checked first because its directional corridor behaviour
            // is more specific than Burst if both abilities somehow overlap.
            if (beamInfluenceActive)
            {
                UpdateBeamTargets(state);
            }
            else if (burstActive)
            {
                UpdateBurstTargets(state);
            }
            else
            {
                ResetTargetsToOriginal(state);
            }

            MovePolygonTowardsTargets(
                state,
                burstActive || beamInfluenceActive
            );
        }
    }

    private void CacheChildPolygons()
    {
        polygonStates.Clear();

        // The parent controller automatically gathers every enabled or disabled
        // PolygonCollider2D below it so the experiment does not require the same
        // script to be attached manually to dozens of darkness pieces.
        PolygonCollider2D[] childPolygons =
            GetComponentsInChildren<PolygonCollider2D>(
                true
            );

        foreach (PolygonCollider2D childPolygon in childPolygons)
        {
            if (childPolygon == null)
            {
                continue;
            }

            Vector2[] originalPoints =
                childPolygon.points;

            Vector2[] targetPoints =
                new Vector2[originalPoints.Length];

            originalPoints.CopyTo(
                targetPoints,
                0
            );

            PolygonState state =
                new PolygonState
                {
                    collider = childPolygon,
                    originalPoints = originalPoints,
                    targetPoints = targetPoints
                };

            polygonStates.Add(state);
        }

        Debug.Log(
            "Polygon Flow experiment found " +
            polygonStates.Count +
            " child PolygonCollider2D components."
        );
    }

    private void UpdateBurstTargets(
        PolygonState state
    )
    {
        if (
            lightBurstController == null ||
            playerTransform == null
        )
        {
            ResetTargetsToOriginal(state);
            return;
        }

        float currentBurstRadius =
            lightBurstController.GetCurrentBurstRadius();

        for (
            int i = 0;
            i < state.originalPoints.Length;
            i++
        )
        {
            Vector3 originalWorldPoint =
                state.collider.transform.TransformPoint(
                    state.originalPoints[i]
                );

            Vector2 directionFromPlayer =
                (Vector2)originalWorldPoint -
                (Vector2)playerTransform.position;

            float distanceFromPlayer =
                directionFromPlayer.magnitude;

            // Only polygon vertices actually reached by the expanding real Burst
            // should deform. Untouched parts remain in their original positions.
            if (distanceFromPlayer > currentBurstRadius)
            {
                state.targetPoints[i] =
                    state.originalPoints[i];

                continue;
            }

            if (directionFromPlayer.sqrMagnitude <= 0.001f)
            {
                directionFromPlayer =
                    Vector2.up;
            }

            directionFromPlayer.Normalize();

            Vector2 outwardOffset =
                directionFromPlayer *
                burstOutwardPushDistance;

            // The tangent component is what makes this a Flow experiment rather
            // than simple repulsion. Affected boundary points also slide around
            // the Burst instead of moving directly away only.
            Vector2 tangentDirection =
                new Vector2(
                    -directionFromPlayer.y,
                    directionFromPlayer.x
                );

            if (directionFromPlayer.y < 0f)
            {
                tangentDirection *= -1f;
            }

            Vector2 tangentOffset =
                tangentDirection *
                burstTangentFlowDistance;

            Vector3 targetWorldPoint =
                originalWorldPoint +
                (Vector3)(
                    outwardOffset +
                    tangentOffset
                );

            state.targetPoints[i] =
                state.collider.transform.InverseTransformPoint(
                    targetWorldPoint
                );
        }
    }

    private void UpdateBeamTargets(
        PolygonState state
    )
    {
        if (
            playerTransform == null ||
            beamVisualTransform == null
        )
        {
            ResetTargetsToOriginal(state);
            return;
        }

        Vector2 beamOrigin =
            playerTransform.position;

        Vector2 beamDirection =
            beamVisualTransform.right.normalized;

        Vector2 beamPerpendicular =
            new Vector2(
                -beamDirection.y,
                beamDirection.x
            );

        for (
            int i = 0;
            i < state.originalPoints.Length;
            i++
        )
        {
            Vector3 originalWorldPoint =
                state.collider.transform.TransformPoint(
                    state.originalPoints[i]
                );

            Vector2 fromBeamOrigin =
                (Vector2)originalWorldPoint -
                beamOrigin;

            float distanceAlongBeam =
                Vector2.Dot(
                    fromBeamOrigin,
                    beamDirection
                );

            float distanceAcrossBeam =
                Vector2.Dot(
                    fromBeamOrigin,
                    beamPerpendicular
                );

            // Vertices behind the player are ignored because the fired Beam
            // only travels forwards from its origin.
            if (distanceAlongBeam < 0f)
            {
                state.targetPoints[i] =
                    state.originalPoints[i];

                continue;
            }

            // Only vertices near the Beam corridor are moved. This lets the
            // darkness separate around the actual shot rather than having the
            // entire polygon mass shift at once.
            if (
                Mathf.Abs(distanceAcrossBeam) >
                beamHalfWidth
            )
            {
                state.targetPoints[i] =
                    state.originalPoints[i];

                continue;
            }

            float sideDirection =
                distanceAcrossBeam >= 0f
                    ? 1f
                    : -1f;

            Vector2 sidewaysOffset =
                beamPerpendicular *
                sideDirection *
                beamSidePushDistance;

            // A smaller forward component gives the movement the same flowing
            // character as the earlier Flow test rather than only splitting
            // darkness vertically around the Beam.
            Vector2 forwardOffset =
                beamDirection *
                beamForwardFlowDistance;

            Vector3 targetWorldPoint =
                originalWorldPoint +
                (Vector3)(
                    sidewaysOffset +
                    forwardOffset
                );

            state.targetPoints[i] =
                state.collider.transform.InverseTransformPoint(
                    targetWorldPoint
                );
        }
    }

    private void ResetTargetsToOriginal(
        PolygonState state
    )
    {
        for (
            int i = 0;
            i < state.originalPoints.Length;
            i++
        )
        {
            state.targetPoints[i] =
                state.originalPoints[i];
        }
    }

    private void MovePolygonTowardsTargets(
        PolygonState state,
        bool lightInfluenceActive
    )
    {
        Vector2[] currentPoints =
            state.collider.points;

        float movementSpeed =
            lightInfluenceActive
                ? deformationSpeed
                : returnSpeed;

        for (
            int i = 0;
            i < currentPoints.Length;
            i++
        )
        {
            // Collider vertices move gradually rather than snapping so the
            // darkness can appear to deform and flow around the light.
            currentPoints[i] =
                Vector2.MoveTowards(
                    currentPoints[i],
                    state.targetPoints[i],
                    movementSpeed *
                    Time.deltaTime
                );
        }

        state.collider.points =
            currentPoints;
    }

    private void UpdateBeamHoldState()
    {
        if (lightBeamController == null)
        {
            beamWasActive = false;
            beamHoldTimer = 0f;
            return;
        }

        bool beamActive =
            lightBeamController.IsBeamActive();

        if (beamActive)
        {
            beamWasActive = true;
            beamHoldTimer = beamHoldDuration;
            return;
        }

        if (beamWasActive)
        {
            beamWasActive = false;
        }

        if (beamHoldTimer > 0f)
        {
            beamHoldTimer -= Time.deltaTime;
        }
    }

    private bool IsBeamInfluenceActive()
    {
        if (lightBeamController == null)
        {
            return false;
        }

        return
            lightBeamController.IsBeamActive() ||
            beamHoldTimer > 0f;
    }
}