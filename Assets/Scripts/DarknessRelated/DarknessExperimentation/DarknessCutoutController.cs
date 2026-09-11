using System.Collections.Generic;
using UnityEngine;

public class DarknessCutoutController : MonoBehaviour
{
    private enum CutoutPhase
    {
        Active,
        Holding,
        Reforming
    }

    /*
     * Beam cut-outs remain owned by this controller because Beam persistence
     * has not been moved into LightBeamController. Burst lifetime is now owned
     * entirely by LightBurstController and is read separately below.
     */
    private class BeamCutoutData
    {
        public CutoutPhase phase;

        public Vector2 originWorld;
        public Vector2 directionWorld = Vector2.right;

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

    [Header("Beam Settings")]

    // This controls the maximum distance removed around the Beam line.
    [SerializeField]
    private float beamMaximumPushDistance = 1.5f;

    // Beam cut-outs expand briefly so the darkness still visibly reacts to firing.
    [SerializeField]
    private float beamPushDuration = 0.3f;

    [Header("Beam Persistence")]

    // Beam still owns its persistence here because that behaviour has not yet
    // been transferred into LightBeamController.
    [SerializeField]
    private float beamHoldDuration = 2.5f;

    // The Beam corridor gradually closes after its hold period.
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
     * Intermediate alpha values soften the low-resolution CPU mask edge.
     */
    [SerializeField]
    private float maskEdgeSoftness = 0.15f;

    [Header("Dynamic Mask Performance")]

    /*
     * The generated mask does not need to rebuild every rendered frame.
     * Limiting uploads reduces CPU and texture processing cost.
     */
    [SerializeField]
    private float maskUpdateRate = 45f;

    private float maskUpdateTimer = 0f;

    private Material darknessMaterial;
    private Texture2D cutoutMaskTexture;
    private Color32[] maskPixels;

    /*
     * Only Beam cut-outs are stored here now. Burst effects remain inside
     * LightBurstController so there is one authoritative Burst lifetime.
     */
    private readonly List<BeamCutoutData> activeBeamCutouts =
        new List<BeamCutoutData>();

    private bool wasBeamActive = false;

    // This identifies only the Beam currently being fired. Older Beam corridors
    // remain stored while they independently hold and reform.
    private BeamCutoutData liveBeamCutout;

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
                "DarknessCutoutController could not find a SpriteRenderer."
            );

            enabled = false;
            return;
        }

        // A local material instance prevents this darkness object from altering
        // every renderer that uses the same material asset.
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
         * Burst does not need detection here anymore because LightBurstController
         * owns its effects continuously. Only Beam still needs local lifecycle
         * detection and persistence management.
         */
        DetectBeam();
        UpdateBeamCutoutLifetimes();

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
         * The mask is created at runtime because it represents temporary
         * ability state rather than an authored texture asset.
         */
        cutoutMaskTexture =
            new Texture2D(
                maskWidth,
                maskHeight,
                TextureFormat.RGBA32,
                false
            );

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
             * Each Beam receives independent geometry and timing so several
             * previously fired corridors can coexist while reforming.
             */
            liveBeamCutout =
                new BeamCutoutData
                {
                    phase =
                        CutoutPhase.Active,

                    originWorld =
                        lightBeamController.transform.position,

                    directionWorld =
                        beamVisualTransform.right.normalized,

                    currentBeamPushDistance = 0f,

                    maximumBeamPushDistance =
                        beamMaximumPushDistance,

                    /*
                     * Darkness uses the exact endpoint captured by the Beam
                     * controller so the opening ends at the same wall or receiver.
                     */
                    beamLength =
                        lightBeamController.GetLockedBeamLength(),

                    holdDuration =
                        beamHoldDuration,

                    reformDuration =
                        beamReformDuration
                };

            activeBeamCutouts.Add(
                liveBeamCutout
            );
        }

        if (
            beamActive &&
            liveBeamCutout != null
        )
        {
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
             * Firing ends before persistence ends. The completed Beam corridor
             * stays open for its configured traversal window before reforming.
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

    private void UpdateBeamCutoutLifetimes()
    {
        /*
         * Beam remains managed locally until its lifetime is also moved into
         * LightBeamController. Iterating backwards permits safe removal.
         */
        for (
            int i = activeBeamCutouts.Count - 1;
            i >= 0;
            i--
        )
        {
            BeamCutoutData cutout =
                activeBeamCutouts[i];

            if (
                cutout.phase ==
                CutoutPhase.Active
            )
            {
                continue;
            }

            if (
                cutout.phase ==
                CutoutPhase.Holding
            )
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

            // Beam closes by reducing its corridor width until darkness has
            // completely returned to the previously cleared space.
            cutout.currentBeamPushDistance =
                Mathf.Lerp(
                    cutout.maximumBeamPushDistance,
                    0f,
                    reformProgress
                );

            if (
                reformProgress >= 1f
            )
            {
                activeBeamCutouts.RemoveAt(
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
                 * Mask pixels are evaluated in world space because Burst and Beam
                 * positions and distances are already expressed in world units.
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
         * Texture upload happens only at the configured mask rate because Apply
         * is one of the more expensive operations in this CPU-mask approach.
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
        float darknessAmount = 1f;

        /*
         * Burst persistence now comes directly from LightBurstController.
         * Darkness does not start, hold or reform Burst openings itself anymore.
         */
        if (lightBurstController != null)
        {
            int burstEffectCount =
                lightBurstController.GetActiveBurstEffectCount();

            for (
                int i = 0;
                i < burstEffectCount;
                i++
            )
            {
                Vector2 burstOrigin =
                    lightBurstController.GetBurstEffectOrigin(
                        i
                    );

                float burstRadius =
                    lightBurstController.GetBurstEffectRadius(
                        i
                    );

                float burstDarknessAmount =
                    GetBurstDarknessAmount(
                        worldPosition,
                        burstOrigin,
                        burstRadius
                    );

                darknessAmount =
                    Mathf.Min(
                        darknessAmount,
                        burstDarknessAmount
                    );

                if (
                    darknessAmount <= 0f
                )
                {
                    return 0f;
                }
            }
        }

        // Beam still uses locally stored cut-outs because its persistence has
        // intentionally been left in this controller for now.
        foreach (
            BeamCutoutData cutout
            in activeBeamCutouts
        )
        {
            float beamDarknessAmount =
                GetBeamDarknessAmount(
                    worldPosition,
                    cutout
                );

            darknessAmount =
                Mathf.Min(
                    darknessAmount,
                    beamDarknessAmount
                );

            if (
                darknessAmount <= 0f
            )
            {
                break;
            }
        }

        return darknessAmount;
    }

    private float GetBurstDarknessAmount(
        Vector2 worldPosition,
        Vector2 burstOrigin,
        float burstRadius
    )
    {
        float distanceFromCentre =
            Vector2.Distance(
                worldPosition,
                burstOrigin
            );

        /*
         * LightBurstController supplies the radius, including its expansion,
         * holding and reforming phases. Darkness only converts that radius into
         * the visual mask and does not modify its lifetime.
         */
        float signedDistance =
            distanceFromCentre -
            burstRadius;

        return
            ConvertSignedDistanceToMask(
                signedDistance
            );
    }

    private float GetBeamDarknessAmount(
        Vector2 worldPosition,
        BeamCutoutData cutout
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

        float distanceAlongBeam =
            Vector2.Dot(
                fromBeamOrigin,
                beamDirection
            );

        /*
         * Cross-product magnitude provides perpendicular distance without a slope
         * calculation, keeping the Beam stable when fired nearly vertically.
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
            // Horizontal and diagonal shots preserve vertical separation.
            distanceFromBeam =
                crossDistance /
                Mathf.Abs(
                    beamDirection.x
                );
        }
        else
        {
            // Near-vertical shots separate darkness left and right so a vertical
            // Beam cannot accidentally clear the entire darkness surface.
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
         * A short SmoothStep transition softens the generated mask boundary
         * without requiring a substantially higher-resolution CPU texture.
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
         * Burst safety is queried from the same ability-owned radius that drives
         * the visual opening. This keeps damage behaviour aligned during hold
         * and reform rather than maintaining another Burst timer here.
         */
        if (
            lightBurstController != null &&
            lightBurstController.IsPositionInsideBurstEffect(
                worldPosition
            )
        )
        {
            return true;
        }

        foreach (
            BeamCutoutData cutout
            in activeBeamCutouts
        )
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

        return false;
    }

    private bool IsPositionInsideBeamCutout(
        Vector2 worldPosition,
        BeamCutoutData cutout
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

        // Gameplay uses the same endpoint as the visual corridor so the player's
        // safe area never extends beyond the actual Beam.
        if (
            distanceAlongBeam < 0f ||
            distanceAlongBeam > cutout.beamLength
        )
        {
            return false;
        }

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
        /*
         * The runtime-created mask texture is cleaned up with the darkness object
         * so repeated Play Mode sessions do not leave unnecessary allocations.
         */
        if (cutoutMaskTexture != null)
        {
            Destroy(
                cutoutMaskTexture
            );
        }
    }
}