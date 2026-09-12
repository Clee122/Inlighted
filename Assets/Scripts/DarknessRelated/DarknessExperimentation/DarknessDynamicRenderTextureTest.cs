using System.Collections.Generic;
using UnityEngine;

public class DarknessDynamicRenderTextureTest : MonoBehaviour
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
     * Every light cast receives its own data instance so repeated Burst and Beam
     * casts can coexist. New casts do not replace openings that are already
     * lingering or reforming elsewhere in the darkness.
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

    [Header("Render Texture Mask")]

    /*
     * This is the RenderTexture asset created for the experiment. The script
     * writes the final combined mask into this texture so the darkness shader
     * only has to sample one result regardless of how many cut-outs exist.
     */
    [SerializeField]
    private RenderTexture darknessCutoutMask;

    /*
     * The stamp material performs the actual Burst and Beam mask calculations
     * on the GPU. This replaces the previous CPU loop that evaluated every
     * pixel of the darkness texture in C#.
     */
    [SerializeField]
    private Material cutoutStampMaterial;

    [Header("Burst Persistence")]

    // Burst remains fully open after expansion so its environmental consequence
    // lasts longer than the short casting animation itself.
    [SerializeField]
    private float burstHoldDuration = 2.5f;

    // Each Burst opening independently shrinks closed after its hold period.
    [SerializeField]
    private float burstReformDuration = 1.5f;

    [Header("Beam Settings")]

    // Beam range should match the gameplay Light Beam so the darkness is never
    // visually removed beyond the distance that the ability can actually reach.
    [SerializeField]
    private float beamWorldLength = 6f;

    // The selected Beam behaviour separates darkness vertically away from the
    // fired Beam line by this maximum world-space distance.
    [SerializeField]
    private float beamMaximumPushDistance = 1.5f;

    // Beam reaches its maximum opening over a short period so the darkness
    // appears to react instead of simply disappearing in a single frame.
    [SerializeField]
    private float beamPushDuration = 0.3f;

    [Header("Beam Persistence")]

    // Each Beam corridor remains open independently after the visible shot ends.
    [SerializeField]
    private float beamHoldDuration = 2.5f;

    // Beam reform narrows each stored corridor until darkness closes again.
    [SerializeField]
    private float beamReformDuration = 1.5f;

    [Header("Mask Edge")]

    /*
     * This value is passed to the GPU stamp shader so the mathematical edge can
     * be softened without increasing RenderTexture resolution dramatically.
     */
    [SerializeField]
    private float edgeSoftness = 0.01f;

    private readonly List<CutoutData> activeCutouts =
        new List<CutoutData>();

    private bool wasBurstActive = false;
    private bool wasBeamActive = false;

    private CutoutData liveBurstCutout;
    private CutoutData liveBeamCutout;

    /*
     * Two temporary RenderTextures are used as a ping-pong pair. Reading from
     * and writing to the same RenderTexture in one GPU pass is unsafe, so every
     * cut-out reads the previous mask and writes its result into the other one.
     */
    private RenderTexture workingMaskA;
    private RenderTexture workingMaskB;

    private Material darknessMaterial;

    private static readonly int CutoutMaskID =
        Shader.PropertyToID("_CutoutMask");

    private static readonly int CurrentMaskID =
        Shader.PropertyToID("_CurrentMask");

    private static readonly int CutoutTypeID =
        Shader.PropertyToID("_CutoutType");

    private static readonly int OriginUVID =
        Shader.PropertyToID("_OriginUV");

    private static readonly int DirectionUVID =
        Shader.PropertyToID("_DirectionUV");

    private static readonly int RadiusUVID =
        Shader.PropertyToID("_RadiusUV");

    private static readonly int BeamLengthUVID =
        Shader.PropertyToID("_BeamLengthUV");

    private static readonly int BeamPushDistanceUVID =
        Shader.PropertyToID("_BeamPushDistanceUV");

    private static readonly int EdgeSoftnessID =
        Shader.PropertyToID("_EdgeSoftness");

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
                "DarknessDynamicRenderTextureTest could not find a SpriteRenderer."
            );

            enabled = false;
            return;
        }

        if (darknessCutoutMask == null)
        {
            Debug.LogError(
                "DarknessDynamicRenderTextureTest needs RT_DarknessCutoutMaskTest assigned."
            );

            enabled = false;
            return;
        }

        if (cutoutStampMaterial == null)
        {
            Debug.LogError(
                "DarknessDynamicRenderTextureTest needs M_DarknessCutoutStampTest assigned."
            );

            enabled = false;
            return;
        }

        // A local darkness material instance prevents this test from changing
        // other renderers that happen to use the same shared material asset.
        darknessMaterial =
            darknessRenderer.material;

        CreateWorkingRenderTextures();

        // The final darkness shader samples this texture as its alpha mask.
        darknessMaterial.SetTexture(
            CutoutMaskID,
            darknessCutoutMask
        );

        RebuildMask();
    }

    private void Update()
    {
        DetectBurst();
        DetectBeam();

        UpdateCutoutLifetimes();

        /*
         * The GPU mask can be rebuilt every frame because C# is no longer
         * iterating through hundreds of thousands of individual texture pixels.
         * This is intended to remove the stepping seen during Beam and reform.
         */
        RebuildMask();
    }

    private void CreateWorkingRenderTextures()
    {
        /*
         * The temporary textures match the user's RenderTexture asset so all
         * stamp passes use the same resolution, filtering and colour format.
         */
        workingMaskA =
            new RenderTexture(
                darknessCutoutMask.descriptor
            );

        workingMaskA.name =
            "RT_DarknessWorkingMaskA";

        workingMaskA.filterMode =
            darknessCutoutMask.filterMode;

        workingMaskA.wrapMode =
            TextureWrapMode.Clamp;

        workingMaskA.Create();

        workingMaskB =
            new RenderTexture(
                darknessCutoutMask.descriptor
            );

        workingMaskB.name =
            "RT_DarknessWorkingMaskB";

        workingMaskB.filterMode =
            darknessCutoutMask.filterMode;

        workingMaskB.wrapMode =
            TextureWrapMode.Clamp;

        workingMaskB.Create();
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
             * Every cast creates a new Burst entry. This is the key difference
             * from the original single-property Shader Graph implementation.
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
             * During the live cast the newest Burst follows the current ability
             * radius. Once the cast ends, this cut-out becomes independent.
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
            // Reaching full expansion hands responsibility from the ability to
            // the persistent environmental cut-out stored in this controller.
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
             * The fired Beam direction is captured once. This prevents later
             * aiming or Player movement from dragging an existing darkness gap.
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
            float pushSpeed =
                beamMaximumPushDistance /
                Mathf.Max(
                    beamPushDuration,
                    0.01f
                );

            // Beam opening progresses every frame so the GPU receives a smooth
            // width instead of the discrete snapshots produced by the CPU mask.
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
            // The visual Beam can disappear while its environmental corridor
            // remains stored at full width for the configured hold duration.
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
         * Completed entries are removed from the list, so iteration runs
         * backwards to avoid skipping the element after a removal.
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
                // Burst darkness returns by smoothly reducing the stored radius.
                cutout.currentRadius =
                    Mathf.Lerp(
                        cutout.maximumRadius,
                        0f,
                        reformProgress
                    );
            }
            else
            {
                // Beam darkness closes from both sides by reducing the split
                // distance around its stored line.
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

    private void RebuildMask()
    {
        if (
            workingMaskA == null ||
            workingMaskB == null ||
            darknessCutoutMask == null
        )
        {
            return;
        }

        /*
         * Each rebuild begins with completely visible darkness. Individual
         * light interactions then subtract their shapes from this white mask.
         */
        ClearRenderTextureToWhite(
            workingMaskA
        );

        RenderTexture source =
            workingMaskA;

        RenderTexture destination =
            workingMaskB;

        foreach (CutoutData cutout in activeCutouts)
        {
            SetStampMaterialValues(
                cutout
            );

            /*
             * The previous mask is sampled by _CurrentMask while the stamp shader
             * applies one additional cut-out. Ping-ponging avoids sampling from
             * the same RenderTexture that is currently being written.
             */
            cutoutStampMaterial.SetTexture(
                CurrentMaskID,
                source
            );

            Graphics.Blit(
                source,
                destination,
                cutoutStampMaterial
            );

            RenderTexture temporary =
                source;

            source =
                destination;

            destination =
                temporary;
        }

        /*
         * The final ping-pong result is copied into the persistent RenderTexture
         * asset sampled by the darkness material.
         */
        Graphics.Blit(
            source,
            darknessCutoutMask
        );
    }

    private void SetStampMaterialValues(
        CutoutData cutout
    )
    {
        /*
         * Renderer.bounds provides the actual world-space rectangle occupied by
         * the visible darkness. Using that rectangle directly keeps mask UVs
         * aligned with what the player sees.
         */
        Bounds darknessWorldBounds =
            darknessRenderer.bounds;

        float darknessWorldWidth =
            darknessWorldBounds.size.x;

        float darknessWorldHeight =
            darknessWorldBounds.size.y;

        /*
         * Convert the light interaction directly into the 0-1 UV range of the
         * visible darkness. Values outside the darkness remain outside 0-1 rather
         * than being clamped onto the edge.
         */
        float originUVX =
            darknessWorldWidth > 0f
                ? (
                    cutout.originWorld.x -
                    darknessWorldBounds.min.x
                ) /
                darknessWorldWidth
                : 0f;

        float originUVY =
            darknessWorldHeight > 0f
                ? (
                    cutout.originWorld.y -
                    darknessWorldBounds.min.y
                ) /
                darknessWorldHeight
                : 0f;

        cutoutStampMaterial.SetVector(
            OriginUVID,
            new Vector4(
                originUVX,
                originUVY,
                0f,
                0f
            )
        );

        cutoutStampMaterial.SetFloat(
            EdgeSoftnessID,
            edgeSoftness
        );

        if (cutout.type == CutoutType.Burst)
        {
            /*
             * The same world-space Burst radius becomes different X and Y UV
             * values when the darkness rectangle is not square. This preserves
             * a circular world-space opening.
             */
            float radiusUVX =
                darknessWorldWidth > 0f
                    ? cutout.currentRadius /
                      darknessWorldWidth
                    : 0f;

            float radiusUVY =
                darknessWorldHeight > 0f
                    ? cutout.currentRadius /
                      darknessWorldHeight
                    : 0f;

            cutoutStampMaterial.SetFloat(
                CutoutTypeID,
                0f
            );

            cutoutStampMaterial.SetVector(
                RadiusUVID,
                new Vector4(
                    Mathf.Max(
                        radiusUVX,
                        0.0001f
                    ),
                    Mathf.Max(
                        radiusUVY,
                        0.0001f
                    ),
                    0f,
                    0f
                )
            );

            return;
        }

        /*
         * Beam direction is converted into the same UV proportions as the
         * darkness bounds so the mask does not distort when the visual rectangle
         * is much wider than it is tall.
         */
        Vector2 directionUV =
            new Vector2(
                darknessWorldWidth > 0f
                    ? cutout.directionWorld.x /
                      darknessWorldWidth
                    : cutout.directionWorld.x,

                darknessWorldHeight > 0f
                    ? cutout.directionWorld.y /
                      darknessWorldHeight
                    : cutout.directionWorld.y
            );

        if (directionUV.sqrMagnitude > 0.000001f)
        {
            directionUV.Normalize();
        }
        else
        {
            directionUV =
                Vector2.right;
        }

        float beamLengthUV =
            darknessWorldWidth > 0f
                ? cutout.beamLength /
                  darknessWorldWidth
                : 0f;

        float beamPushDistanceUV =
            darknessWorldHeight > 0f
                ? cutout.currentBeamPushDistance /
                  darknessWorldHeight
                : 0f;

        cutoutStampMaterial.SetFloat(
            CutoutTypeID,
            1f
        );

        cutoutStampMaterial.SetVector(
            DirectionUVID,
            new Vector4(
                directionUV.x,
                directionUV.y,
                0f,
                0f
            )
        );

        cutoutStampMaterial.SetFloat(
            BeamLengthUVID,
            beamLengthUV
        );

        cutoutStampMaterial.SetFloat(
            BeamPushDistanceUVID,
            beamPushDistanceUV
        );
    }

    private void ClearRenderTextureToWhite(
        RenderTexture renderTexture
    )
    {
        RenderTexture previous =
            RenderTexture.active;

        RenderTexture.active =
            renderTexture;

        /*
         * White represents completely intact darkness. Every GPU stamp starts
         * from this state and reduces selected areas towards black.
         */
        GL.Clear(
            false,
            true,
            Color.white
        );

        RenderTexture.active =
            previous;
    }

    public bool IsPositionInsideLightCutout(
        Vector2 worldPosition
    )
    {
        /*
         * Gameplay still uses the same mathematical cut-out data that creates
         * the GPU mask. This keeps safe space aligned with what the player sees
         * without requiring expensive GPU texture readback.
         */
        foreach (CutoutData cutout in activeCutouts)
        {
            if (
                cutout.type ==
                CutoutType.Burst
            )
            {
                float distanceFromBurst =
                    Vector2.Distance(
                        worldPosition,
                        cutout.originWorld
                    );

                if (
                    distanceFromBurst <=
                    cutout.currentRadius
                )
                {
                    return true;
                }

                continue;
            }

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

        if (
            distanceAlongBeam < 0f ||
            distanceAlongBeam > cutout.beamLength
        )
        {
            return false;
        }

        /*
         * Projection gives a stable point on the Beam line at every angle and
         * avoids slope calculations that become unstable for steep directions.
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
        /*
         * Only the runtime-created working textures are destroyed here. The
         * project RenderTexture asset itself must remain untouched.
         */
        if (workingMaskA != null)
        {
            workingMaskA.Release();

            Destroy(
                workingMaskA
            );
        }

        if (workingMaskB != null)
        {
            workingMaskB.Release();

            Destroy(
                workingMaskB
            );
        }
    }
}