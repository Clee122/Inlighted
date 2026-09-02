using System.Collections.Generic;
using UnityEngine;

public class DarknessIdleMotionGroupTest : MonoBehaviour
{
    [Header("Scale Variation")]
    [SerializeField] private float minimumScaleMultiplier = 0.9f;
    [SerializeField] private float maximumScaleMultiplier = 1.1f;
    [SerializeField] private float scaleChangeSpeed = 0.6f;

    [Header("Timing Variation")]
    [SerializeField] private float minimumWaitTime = 0.1f;
    [SerializeField] private float maximumWaitTime = 0.6f;

    // Each child needs its own animation state so the darkness pieces do not
    // pulse together. The parent controls them centrally, but each piece still
    // receives independent scale targets and timing.
    private class DarknessPieceState
    {
        public Transform pieceTransform;
        public Vector3 originalScale;
        public Vector3 targetScale;
        public float waitTimer;
        public bool isWaiting;
    }

    private readonly List<DarknessPieceState> darknessPieces =
        new List<DarknessPieceState>();

    private void Awake()
    {
        CacheDarknessPieces();
    }

    private void Update()
    {
        UpdateIdleMotion();
    }

    private void CacheDarknessPieces()
    {
        darknessPieces.Clear();

        // Only children with DarknessCompressionTest are included so unrelated
        // objects under the experiment parent are not accidentally animated.
        DarknessCompressionTest[] compressionPieces =
            GetComponentsInChildren<DarknessCompressionTest>(
                true
            );

        foreach (
            DarknessCompressionTest compressionPiece
            in compressionPieces
        )
        {
            Transform pieceTransform =
                compressionPiece.transform;

            DarknessPieceState state =
                new DarknessPieceState
                {
                    pieceTransform = pieceTransform,
                    originalScale = pieceTransform.localScale,
                    waitTimer = 0f,
                    isWaiting = false
                };

            ChooseNewTargetScale(
                state
            );

            darknessPieces.Add(
                state
            );
        }
    }

    private void UpdateIdleMotion()
    {
        foreach (
            DarknessPieceState state
            in darknessPieces
        )
        {
            if (
                state.pieceTransform == null
            )
            {
                continue;
            }

            if (state.isWaiting)
            {
                state.waitTimer -=
                    Time.deltaTime;

                if (
                    state.waitTimer <= 0f
                )
                {
                    state.isWaiting = false;

                    ChooseNewTargetScale(
                        state
                    );
                }

                continue;
            }

            // Every piece moves gradually towards its own target scale. This
            // creates local movement across the darkness mass without making
            // the entire formation expand and shrink at the same time.
            state.pieceTransform.localScale =
                Vector3.MoveTowards(
                    state.pieceTransform.localScale,
                    state.targetScale,
                    scaleChangeSpeed *
                    Time.deltaTime
                );

            if (
                Vector3.Distance(
                    state.pieceTransform.localScale,
                    state.targetScale
                ) <= 0.001f
            )
            {
                state.isWaiting = true;

                state.waitTimer =
                    Random.Range(
                        minimumWaitTime,
                        maximumWaitTime
                    );
            }
        }
    }

    private void ChooseNewTargetScale(
        DarknessPieceState state
    )
    {
        float randomScaleMultiplier =
            Random.Range(
                minimumScaleMultiplier,
                maximumScaleMultiplier
            );

        // Uniform scaling preserves each piece's shape while making individual
        // regions of the darkness subtly expand and contract independently.
        state.targetScale =
            new Vector3(
                state.originalScale.x *
                randomScaleMultiplier,
                state.originalScale.y *
                randomScaleMultiplier,
                state.originalScale.z
            );
    }
}