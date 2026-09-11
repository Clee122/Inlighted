using System.Collections.Generic;
using UnityEngine;

public class DarknessCutoutController : MonoBehaviour
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

    // This controls the maximum distance removed around the Beam line.
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
     * The lower CPU-mask resolution deliberately trades some edge precision for
     * substantially lower processing cost. Visual styling can later disguise
     * the remaining jaggedness around the cut-out boundaries.
     */
    [SerializeField]
    private int maskWidth = 256;

    [SerializeField]
    private int maskHeight = 128;

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
            // The message uses the current controller name so missing renderer
            // problems are easier to identify after the script rename.
            Debug.LogError(
                "DarknessCutoutController could not find a SpriteRenderer."
            );

            enabled = false;
            return;
        }

        // A local material instance prevents this darkness object from changing
        // every renderer that uses the same shared material asset.
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

                    /*
                     * The exact locked Beam distance is shared from the Beam
                     * controller so the darkness ends at the same wall or Bloom
                     * Receiver instead of using its own separate fixed range.
                     */
                    beamLength =
                        lightBeamController.GetLockedBeamLength(),

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
                // Each Beam closes by reducing its own opening until the corridor
                // has completely reformed.
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
         * Projection determines how far this pixel sits along the fired Beam.
         * This remains stable for horizontal, diagonal and vertical shots and
         * keeps the cut-out limited to the Beam's wall-controlled length.
         */
        float distanceAlongBeam =
            Vector2.Dot(
                fromBeamOrigin,
                beamDirection
            );

        /*
         * The 2D cross-product magnitude gives the perpendicular relationship
         * between the pixel and the Beam line without using a slope equation.
         * This avoids the instability that appears as the Beam approaches vertical.
         */
        float crossDistance =
            Mathf.Abs(
                fromBeamOrigin.x *
                beamDirection.y -
                fromBeamOrigin.y *
                beamDirection.x
            );

        float distanceFromBeam;

        if (
            Mathf.Abs(
                beamDirection.x
            ) > 0.1f
        )
        {
            /*
             * Horizontal and diagonal shots preserve the selected behaviour where
             * darkness separates vertically above and below the Beam line.
             */
            distanceFromBeam =
                crossDistance /
                Mathf.Abs(
                    beamDirection.x
                );
        }
        else
        {
            /*
             * A near-vertical Beam cannot use vertical separation because every
             * point along the line can otherwise appear to have zero vertical
             * distance. In this case the darkness separates left and right,
             * preventing the entire darkness surface from being cleared.
             */
            distanceFromBeam =
                crossDistance /
                Mathf.Max(
                    Mathf.Abs(
                        beamDirection.y
                    ),
                    0.0001f
                );
        }

        float widthSignedDistance =
            distanceFromBeam -
            cutout.currentBeamPushDistance;

        float widthBoundaryMask =
            ConvertSignedDistanceToMask(
                widthSignedDistance
            );

        /*
         * These boundaries keep the opening between the firing point and the
         * exact endpoint captured from the Beam controller.
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

        // Gameplay safety uses the same wall-limited Beam endpoint as the visual
        // mask so neither system continues beyond the physical shot.
        if (
            distanceAlongBeam < 0f ||
            distanceAlongBeam > cutout.beamLength
        )
        {
            return false;
        }

        /*
         * Gameplay uses the same direction-aware width calculation as the visual
         * mask. Horizontal/diagonal shots keep vertical separation, while
         * near-vertical shots use horizontal separation instead.
         */
        float crossDistance =
            Mathf.Abs(
                fromBeamOrigin.x *
                beamDirection.y -
                fromBeamOrigin.y *
                beamDirection.x
            );

        float distanceFromBeam;

        if (
            Mathf.Abs(
                beamDirection.x
            ) > 0.1f
        )
        {
            distanceFromBeam =
                crossDistance /
                Mathf.Abs(
                    beamDirection.x
                );
        }
        else
        {
            distanceFromBeam =
                crossDistance /
                Mathf.Max(
                    Mathf.Abs(
                        beamDirection.y
                    ),
                    0.0001f
                );
        }

        return
            distanceFromBeam <=
            cutout.currentBeamPushDistance;
    }

    private void OnDestroy()
    {
        // The runtime-created texture is destroyed with the darkness object so
        // repeated Play Mode sessions do not leave unnecessary texture instances.
        if (cutoutMaskTexture != null)
        {
            Destroy(
                cutoutMaskTexture
            );
        }
    }
}