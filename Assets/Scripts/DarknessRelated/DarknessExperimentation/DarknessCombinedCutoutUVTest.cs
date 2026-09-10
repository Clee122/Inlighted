using System.Collections.Generic;
using UnityEngine;

public class DarknessCombinedCutoutUVTest : MonoBehaviour
{
    private enum CutoutType
    {
        Burst,
        Beam
    }

    private enum CutoutPhase
    {
        Active,
        Holding,
        Reforming
    }

    /*
     * Every light cast receives its own CutoutData instance. This allows an
     * arbitrary number of Burst and Beam openings to exist at the same time
     * instead of newer casts overwriting older lingering cut-outs.
     */
    private class CutoutData
    {
        public CutoutType type;
        public CutoutPhase phase;

        public Vector2 originWorld;
        public Vector2 directionWorld = Vector2.right;

        public float currentRadius;
        public float maximumRadius;

        public float currentBeamPushDistance;
        public float maximumBeamPushDistance;
        public float beamLength;

        public float holdTimer;
        public float reformTimer;
        public float holdDuration;
        public float reformDuration;
    }

    [Header("Light Ability References")]
    [SerializeField]
    private LightBurstController lightBurstController;

    [SerializeField]
    private LightBeamController lightBeamController;

    [Header("Beam Visual")]
    [SerializeField]
    private Transform beamVisualTransform;

    [Header("Darkness Visual")]
    [SerializeField]
    private SpriteRenderer darknessRenderer;

    [Header("Burst Persistence")]

    // Burst openings remain fully displaced after the cast finishes so the
    // player has time to traverse or interact with revealed puzzle elements.
    [SerializeField]
    private float burstHoldDuration = 2.5f;

    // Burst reform shrinks each individual circular opening independently.
    [SerializeField]
    private float burstReformDuration = 1.5f;

    [Header("Beam Settings")]

    // The stored Beam length matches the gameplay range so the generated mask
    // does not create safe space beyond where the actual ability travelled.
    [SerializeField]
    private float beamWorldLength = 6f;

    // This controls the maximum vertical distance removed around the Beam line.
    [SerializeField]
    private float beamMaximumPushDistance = 1.5f;

    // Beam cut-outs expand briefly rather than immediately appearing at full width.
    [SerializeField]
    private float beamPushDuration = 0.3f;

    [Header("Beam Persistence")]

    // Each fired Beam corridor owns its own hold timer so firing another Beam
    // cannot cancel a corridor that is already lingering.
    [SerializeField]
    private float beamHoldDuration = 2.5f;

    // Each Beam corridor closes independently after its hold period finishes.
    [SerializeField]
    private float beamReformDuration = 1.5f;

    [Header("Dynamic Mask")]

    /*
     * The mask resolution controls how accurately the cut-out shape is sampled.
     * A moderate resolution is kept because edge smoothing now handles aliasing
     * without requiring an excessively expensive high-resolution texture.
     */
    [SerializeField]
    private int maskWidth = 512;

    [SerializeField]
    private int maskHeight = 256;

    /*
     * Softness is measured in world units around the cut-out boundary.
     * Instead of changing immediately from fully cleared to fully dark, pixels
     * near the boundary receive intermediate alpha values to reduce jagged edges.
     */
    [SerializeField]
    private float maskEdgeSoftness = 0.15f;

    [Header("Dynamic Mask Performance")]

    /*
     * The generated mask does not need to rebuild every rendered frame.
     * Updating it at a controlled rate reduces CPU and texture-upload cost while
     * retaining a responsive enough visual result for this experiment.
     */
    [SerializeField]
    private float maskUpdateRate = 45f;

    private float maskUpdateTimer = 0f;

    private Material darknessMaterial;
    private Texture2D cutoutMaskTexture;
    private Color32[] maskPixels;

    private readonly List<CutoutData> activeCutouts =
        new List<CutoutData>();

    private bool wasBurstActive = false;
    private bool wasBeamActive = false;

    // These references identify only the cut-out belonging to the ability that
    // is currently being fired. Once a cast ends, its cut-out remains in the
    // list while later casts create completely separate instances.
    private CutoutData liveBurstCutout;
    private CutoutData liveBeamCutout;

    private static readonly int CutoutMaskID =
        Shader.PropertyToID("_CutoutMask");

    private void Awake()
    {
        if (darknessRenderer == null)
        {
            darknessRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (darknessRenderer == null)
        {
            Debug.LogError(
                "DarknessCombinedCutoutUVTest could not find a SpriteRenderer."
            );

            enabled = false;
            return;
        }

        // A local material instance prevents this experimental darkness object
        // from changing every renderer that uses the same shared material asset.
        darknessMaterial =
            darknessRenderer.material;

        CreateMaskTexture();
    }

    private void Update()
    {
        if (
            darknessMaterial == null ||
            darknessRenderer == null ||
            darknessRenderer.sprite == null
        )
        {
            return;
        }

        /*
         * Ability state and gameplay cut-out lifetimes continue updating every
         * frame so gameplay remains responsive even when the visual mask itself
         * is rebuilt at a controlled frequency.
         */
        DetectBurst();
        DetectBeam();

        UpdateCutoutLifetimes();

        maskUpdateTimer +=
            Time.deltaTime;

        float maskUpdateInterval =
            maskUpdateRate > 0f
                ? 1f / maskUpdateRate
                : 0f;

        if (
            maskUpdateInterval <= 0f ||
            maskUpdateTimer >= maskUpdateInterval
        )
        {
            maskUpdateTimer = 0f;

            BuildDynamicMask();
        }
    }

    private void CreateMaskTexture()
    {
        maskWidth =
            Mathf.Max(
                16,
                maskWidth
            );

        maskHeight =
            Mathf.Max(
                16,
                maskHeight
            );

        /*
         * The texture is created at runtime because it represents temporary
         * gameplay state rather than an authored darkness texture asset.
         */
        cutoutMaskTexture =
            new Texture2D(
                maskWidth,
                maskHeight,
                TextureFormat.RGBA32,
                false
            );

        // Bilinear filtering works together with grey edge pixels to interpolate
        // the generated mask more smoothly between neighbouring samples.
        cutoutMaskTexture.filterMode =
            FilterMode.Bilinear;

        cutoutMaskTexture.wrapMode =
            TextureWrapMode.Clamp;

        maskPixels =
            new Color32[
                maskWidth *
                maskHeight
            ];

        darknessMaterial.SetTexture(
            CutoutMaskID,
            cutoutMaskTexture
        );

        BuildDynamicMask();
    }

    private void DetectBurst()
    {
        if (lightBurstController == null)
        {
            wasBurstActive = false;
            liveBurstCutout = null;

            return;
        }

        bool burstActive =
            lightBurstController.IsBurstActive();

        if (
            burstActive &&
            !wasBurstActive
        )
        {
            /*
             * A completely new data object is created for every Burst cast.
             * Previous Burst openings remain untouched in activeCutouts.
             */
            liveBurstCutout =
                new CutoutData
                {
                    type = CutoutType.Burst,
                    phase = CutoutPhase.Active,

                    originWorld =
                        lightBurstController.transform.position,

                    currentRadius =
                        lightBurstController.GetCurrentBurstRadius(),

                    maximumRadius =
                        lightBurstController.GetBurstDispelRadius(),

                    holdDuration =
                        burstHoldDuration,

                    reformDuration =
                        burstReformDuration
                };

            activeCutouts.Add(
                liveBurstCutout
            );
        }

        if (
            burstActive &&
            liveBurstCutout != null
        )
        {
            /*
             * Only the currently active Burst follows the live ability radius.
             * Previous cut-outs retain their own independent position and lifetime.
             */
            liveBurstCutout.originWorld =
                lightBurstController.transform.position;

            liveBurstCutout.currentRadius =
                lightBurstController.GetCurrentBurstRadius();
        }

        if (
            !burstActive &&
            wasBurstActive &&
            liveBurstCutout != null
        )
        {
            /*
             * When expansion finishes, this cut-out becomes an independent
             * environmental effect and remains at its full radius while holding.
             */
            liveBurstCutout.phase =
                CutoutPhase.Holding;

            liveBurstCutout.currentRadius =
                liveBurstCutout.maximumRadius;

            liveBurstCutout.holdTimer = 0f;

            liveBurstCutout = null;
        }

        wasBurstActive =
            burstActive;
    }

    private void DetectBeam()
    {
        if (
            lightBeamController == null ||
            beamVisualTransform == null
        )
        {
            wasBeamActive = false;
            liveBeamCutout = null;

            return;
        }

        bool beamActive =
            lightBeamController.IsBeamActive();

        if (
            beamActive &&
            !wasBeamActive
        )
        {
            /*
             * Each Beam receives its own origin, direction and lifetime so
             * several lingering corridors can coexist without replacing one another.
             */
            liveBeamCutout =
                new CutoutData
                {
                    type = CutoutType.Beam,
                    phase = CutoutPhase.Active,

                    originWorld =
                        lightBeamController.transform.position,

                    directionWorld =
                        beamVisualTransform.right.normalized,

                    currentBeamPushDistance = 0f,

                    maximumBeamPushDistance =
                        beamMaximumPushDistance,

                    beamLength =
                        beamWorldLength,

                    holdDuration =
                        beamHoldDuration,

                    reformDuration =
                        beamReformDuration
                };

            activeCutouts.Add(
                liveBeamCutout
            );
        }

        if (
            beamActive &&
            liveBeamCutout != null
        )
        {
            // The corridor reaches full width over a short duration so the
            // darkness still appears to react instead of disappearing instantly.
            float pushSpeed =
                beamMaximumPushDistance /
                Mathf.Max(
                    beamPushDuration,
                    0.01f
                );

            liveBeamCutout.currentBeamPushDistance =
                Mathf.MoveTowards(
                    liveBeamCutout.currentBeamPushDistance,
                    liveBeamCutout.maximumBeamPushDistance,
                    pushSpeed *
                    Time.deltaTime
                );
        }

        if (
            !beamActive &&
            wasBeamActive &&
            liveBeamCutout != null
        )
        {
            /*
             * Beam ending finishes only the cast. Its corridor remains stored and
             * enters its independent hold period before darkness reforms.
             */
            liveBeamCutout.phase =
                CutoutPhase.Holding;

            liveBeamCutout.currentBeamPushDistance =
                liveBeamCutout.maximumBeamPushDistance;

            liveBeamCutout.holdTimer = 0f;

            liveBeamCutout = null;
        }

        wasBeamActive =
            beamActive;
    }

    private void UpdateCutoutLifetimes()
    {
        /*
         * Iterating backwards allows completed cut-outs to be safely removed
         * without changing the index of entries that still need to be processed.
         */
        for (
            int i = activeCutouts.Count - 1;
            i >= 0;
            i--
        )
        {
            CutoutData cutout =
                activeCutouts[i];

            if (cutout.phase == CutoutPhase.Active)
            {
                continue;
            }

            if (cutout.phase == CutoutPhase.Holding)
            {
                cutout.holdTimer +=
                    Time.deltaTime;

                if (
                    cutout.holdTimer >=
                    cutout.holdDuration
                )
                {
                    cutout.phase =
                        CutoutPhase.Reforming;

                    cutout.reformTimer = 0f;
                }

                continue;
            }

            cutout.reformTimer +=
                Time.deltaTime;

            float reformProgress =
                cutout.reformDuration > 0f
                    ? Mathf.Clamp01(
                        cutout.reformTimer /
                        cutout.reformDuration
                    )
                    : 1f;

            if (cutout.type == CutoutType.Burst)
            {
                // Each Burst closes by shrinking its own radius independently.
                cutout.currentRadius =
                    Mathf.Lerp(
                        cutout.maximumRadius,
                        0f,
                        reformProgress
                    );
            }
            else
            {
                // Each Beam closes by reducing its own vertical opening until the
                // corridor has completely reformed.
                cutout.currentBeamPushDistance =
                    Mathf.Lerp(
                        cutout.maximumBeamPushDistance,
                        0f,
                        reformProgress
                    );
            }

            if (reformProgress >= 1f)
            {
                activeCutouts.RemoveAt(
                    i
                );
            }
        }
    }

    private void BuildDynamicMask()
    {
        Bounds spriteBounds =
            darknessRenderer.sprite.bounds;

        for (
            int y = 0;
            y < maskHeight;
            y++
        )
        {
            float uvY =
                (
                    y +
                    0.5f
                ) /
                maskHeight;

            for (
                int x = 0;
                x < maskWidth;
                x++
            )
            {
                float uvX =
                    (
                        x +
                        0.5f
                    ) /
                    maskWidth;

                /*
                 * Each mask pixel is converted into world space because all
                 * ability sizes and positions are already defined in world units.
                 */
                Vector3 localPosition =
                    new Vector3(
                        Mathf.Lerp(
                            spriteBounds.min.x,
                            spriteBounds.max.x,
                            uvX
                        ),
                        Mathf.Lerp(
                            spriteBounds.min.y,
                            spriteBounds.max.y,
                            uvY
                        ),
                        0f
                    );

                Vector2 worldPosition =
                    darknessRenderer.transform.TransformPoint(
                        localPosition
                    );

                /*
                 * A value of 1 means fully visible darkness and 0 means fully
                 * cleared darkness. Values between them form the anti-aliased
                 * transition around Burst and Beam boundaries.
                 */
                float darknessAmount =
                    GetDarknessAmountAtPosition(
                        worldPosition
                    );

                byte maskValue =
                    (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(
                            darknessAmount
                        ) *
                        255f
                    );

                int pixelIndex =
                    y *
                    maskWidth +
                    x;

                maskPixels[pixelIndex] =
                    new Color32(
                        maskValue,
                        maskValue,
                        maskValue,
                        255
                    );
            }
        }

        cutoutMaskTexture.SetPixels32(
            maskPixels
        );

        /*
         * Apply uploads the rebuilt texture to the GPU. Because this operation is
         * relatively expensive, it only occurs at the configured update rate.
         */
        cutoutMaskTexture.Apply(
            false,
            false
        );
    }

    private float GetDarknessAmountAtPosition(
        Vector2 worldPosition
    )
    {
        // Begin with completely visible darkness. Every active cut-out can lower
        // the value, and taking the minimum naturally combines overlapping holes.
        float darknessAmount = 1f;

        foreach (CutoutData cutout in activeCutouts)
        {
            float cutoutDarknessAmount;

            if (cutout.type == CutoutType.Burst)
            {
                cutoutDarknessAmount =
                    GetBurstDarknessAmount(
                        worldPosition,
                        cutout
                    );
            }
            else
            {
                cutoutDarknessAmount =
                    GetBeamDarknessAmount(
                        worldPosition,
                        cutout
                    );
            }

            darknessAmount =
                Mathf.Min(
                    darknessAmount,
                    cutoutDarknessAmount
                );

            // Nothing can become more transparent than zero, so there is no
            // reason to evaluate additional cut-outs once this pixel is cleared.
            if (darknessAmount <= 0f)
            {
                break;
            }
        }

        return darknessAmount;
    }

    private float GetBurstDarknessAmount(
        Vector2 worldPosition,
        CutoutData cutout
    )
    {
        float distanceFromCentre =
            Vector2.Distance(
                worldPosition,
                cutout.originWorld
            );

        /*
         * Signed distance is negative inside the cut-out and positive outside.
         * Mapping a small range around zero to 0-1 creates a smooth transition
         * around the circle instead of a hard pixel staircase.
         */
        float signedDistance =
            distanceFromCentre -
            cutout.currentRadius;

        return
            ConvertSignedDistanceToMask(
                signedDistance
            );
    }

    private float GetBeamDarknessAmount(
        Vector2 worldPosition,
        CutoutData cutout
    )
    {
        if (
            cutout.directionWorld.sqrMagnitude <=
            0.000001f
        )
        {
            return 1f;
        }

        Vector2 beamDirection =
            cutout.directionWorld.normalized;

        Vector2 fromBeamOrigin =
            worldPosition -
            cutout.originWorld;

        /*
         * Projection onto the Beam direction gives a stable distance along the
         * fired ray for every angle. This replaces slope-based calculations that
         * could become unstable and produce large visual jumps for steep shots.
         */
        float distanceAlongBeam =
            Vector2.Dot(
                fromBeamOrigin,
                beamDirection
            );

        /*
         * Reconstructing the closest point on the Beam line avoids dividing by
         * direction.x. This keeps the darkness opening stable for shallow,
         * diagonal and near-vertical shots.
         */
        Vector2 closestPointOnBeam =
            cutout.originWorld +
            beamDirection *
            distanceAlongBeam;

        /*
         * The selected darkness behaviour splits vertically in world space.
         * Only vertical distance from the Beam line controls how far the darkness
         * is cleared above and below the fired Beam.
         */
        float verticalDistanceFromBeam =
            Mathf.Abs(
                worldPosition.y -
                closestPointOnBeam.y
            );

        float widthSignedDistance =
            verticalDistanceFromBeam -
            cutout.currentBeamPushDistance;

        float widthBoundaryMask =
            ConvertSignedDistanceToMask(
                widthSignedDistance
            );

        /*
         * Separate boundaries keep the cut-out in front of the firing point and
         * prevent it from extending past the configured Beam range.
         */
        float startBoundaryMask =
            ConvertSignedDistanceToMask(
                -distanceAlongBeam
            );

        float endBoundaryMask =
            ConvertSignedDistanceToMask(
                distanceAlongBeam -
                cutout.beamLength
            );

        /*
         * A point is fully cleared only when it is within the vertical opening
         * and also falls between the start and end of the Beam.
         */
        return
            Mathf.Max(
                widthBoundaryMask,
                Mathf.Max(
                    startBoundaryMask,
                    endBoundaryMask
                )
            );
    }

    private float ConvertSignedDistanceToMask(
        float signedDistance
    )
    {
        float softness =
            Mathf.Max(
                maskEdgeSoftness,
                0.0001f
            );

        /*
         * The transition spans a small distance on both sides of the mathematical
         * boundary. SmoothStep keeps the generated edge visually softer without
         * requiring a much larger runtime texture.
         */
        float normalisedEdge =
            Mathf.InverseLerp(
                -softness,
                softness,
                signedDistance
            );

        return
            Mathf.SmoothStep(
                0f,
                1f,
                normalisedEdge
            );
    }

    public bool IsPositionInsideLightCutout(
        Vector2 worldPosition
    )
    {
        /*
         * Gameplay continues to use the exact mathematical cut-out rather than
         * the feathered edge so partially transparent boundary pixels do not
         * produce ambiguous darkness damage behaviour.
         */
        return
            IsPositionInsideAnyCutout(
                worldPosition
            );
    }

    private bool IsPositionInsideAnyCutout(
        Vector2 worldPosition
    )
    {
        foreach (CutoutData cutout in activeCutouts)
        {
            if (
                cutout.type ==
                CutoutType.Burst
            )
            {
                if (
                    IsPositionInsideBurstCutout(
                        worldPosition,
                        cutout
                    )
                )
                {
                    return true;
                }
            }
            else
            {
                if (
                    IsPositionInsideBeamCutout(
                        worldPosition,
                        cutout
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPositionInsideBurstCutout(
        Vector2 worldPosition,
        CutoutData cutout
    )
    {
        float distance =
            Vector2.Distance(
                worldPosition,
                cutout.originWorld
            );

        return
            distance <=
            cutout.currentRadius;
    }

    private bool IsPositionInsideBeamCutout(
        Vector2 worldPosition,
        CutoutData cutout
    )
    {
        if (
            cutout.directionWorld.sqrMagnitude <=
            0.000001f
        )
        {
            return false;
        }

        Vector2 beamDirection =
            cutout.directionWorld.normalized;

        Vector2 fromBeamOrigin =
            worldPosition -
            cutout.originWorld;

        float distanceAlongBeam =
            Vector2.Dot(
                fromBeamOrigin,
                beamDirection
            );

        // Darkness behind the firing point or beyond the Beam's configured range
        // remains dangerous because the light never actually cleared that region.
        if (
            distanceAlongBeam < 0f ||
            distanceAlongBeam > cutout.beamLength
        )
        {
            return false;
        }

        /*
         * Gameplay reconstructs the same closest point on the Beam line used by
         * the visual mask so safe space remains aligned with the visible opening.
         */
        Vector2 closestPointOnBeam =
            cutout.originWorld +
            beamDirection *
            distanceAlongBeam;

        float verticalDistanceFromBeam =
            Mathf.Abs(
                worldPosition.y -
                closestPointOnBeam.y
            );

        return
            verticalDistanceFromBeam <=
            cutout.currentBeamPushDistance;
    }

    private void OnDestroy()
    {
        // The runtime-created texture is destroyed with the experiment object so
        // repeated Play Mode sessions do not leave unnecessary texture instances.
        if (cutoutMaskTexture != null)
        {
            Destroy(
                cutoutMaskTexture
            );
        }
    }
}