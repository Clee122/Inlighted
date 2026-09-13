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
     * Beam cut-outs remain owned by this controller because this script is
     * responsible for generating and maintaining the darkness mask.
     *
     * Beam-specific tuning values such as width, expansion speed, hold time and
     * reform time are now owned by LightBeamController and copied into each
     * Beam cut-out when that Beam is fired.
     */
    private class BeamCutoutData
    {
        public CutoutPhase phase;

        public Vector2 originWorld;
        public Vector2 directionWorld = Vector2.right;

        public float currentBeamPushDistance;
        public float maximumBeamPushDistance;
        public float beamLength;

        // Each fired Beam stores its own expansion duration so existing corridors
        // continue behaving consistently and independently of later Beam shots.
        public float pushDuration;

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

    [Header("Reactive Darkness VFX")]

    /*
     * Sprite-based layers such as Jayden's red/black centre and supporting black
     * particle sprites receive the shared light cut-out through this array.
     */
    [SerializeField]
    private SpriteRenderer[] reactiveVfxRenderers;

    /*
     * ParticleSystemRenderer is a different renderer type from SpriteRenderer,
     * so the tendril particle system needs its own array even though it receives
     * the same cut-out texture.
     */
    [SerializeField]
    private ParticleSystemRenderer[] reactiveParticleRenderers;

    [Header("Dynamic Mask")]

    /*
     * The lower CPU-mask resolution deliberately trades some edge precision for
     * substantially lower processing cost.
     */
    [SerializeField]
    private int maskWidth = 256;

    [SerializeField]
    private int maskHeight = 128;

    /*
     * Softness is measured in world units around the cut-out boundary so the
     * generated mask does not produce a harsh pixelated edge.
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

    /*
     * Each sprite-based VFX renderer receives its own runtime material instance
     * so cut-out values do not modify shared material assets globally.
     */
    private readonly List<Material> reactiveVfxMaterials =
        new List<Material>();

    private readonly List<SpriteRenderer> validReactiveVfxRenderers =
        new List<SpriteRenderer>();

    /*
     * Particle systems also need independent runtime material instances so their
     * cut-out properties can be changed without modifying the original material
     * asset elsewhere in the project.
     */
    private readonly List<Material> reactiveParticleMaterials =
        new List<Material>();

    private readonly List<ParticleSystemRenderer> validReactiveParticleRenderers =
        new List<ParticleSystemRenderer>();

    private Texture2D cutoutMaskTexture;
    private Color32[] maskPixels;

    /*
     * Only Beam cut-outs are stored here. Burst effects remain inside
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

    /*
     * Sprite-based VFX still use their own UV remapping because a SpriteRenderer
     * has one consistent 0-1 UV rectangle.
     */
    private static readonly int MaskUVScaleID =
        Shader.PropertyToID("_MaskUVScale");

    private static readonly int MaskUVOffsetID =
        Shader.PropertyToID("_MaskUVOffset");

    /*
     * Particle billboards cannot use their own UVs to locate one shared darkness
     * mask because every particle starts again at 0-1 UV coordinates.
     *
     * Instead, these values tell the tendril shader where the main darkness
     * rectangle appears in camera viewport space.
     */
    private static readonly int MaskScreenMinID =
        Shader.PropertyToID("_MaskScreenMin");

    private static readonly int MaskScreenSizeID =
        Shader.PropertyToID("_MaskScreenSize");

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
        // every renderer that uses the same source material asset.
        darknessMaterial =
            darknessRenderer.material;

        /*
         * Cache valid sprite-based VFX renderers and their runtime materials.
         * Parallel lists keep each renderer matched with its own material when
         * its UV-remapping values are calculated later.
         */
        reactiveVfxMaterials.Clear();
        validReactiveVfxRenderers.Clear();

        if (reactiveVfxRenderers != null)
        {
            foreach (
                SpriteRenderer renderer
                in reactiveVfxRenderers
            )
            {
                if (renderer == null)
                {
                    continue;
                }

                Material runtimeMaterial =
                    renderer.material;

                validReactiveVfxRenderers.Add(
                    renderer
                );

                reactiveVfxMaterials.Add(
                    runtimeMaterial
                );

                Debug.Log(
                    "VFX DEBUG | Renderer: " +
                    renderer.name +
                    " | Material: " +
                    runtimeMaterial.name +
                    " | Has _CutoutMask: " +
                    runtimeMaterial.HasProperty(
                        CutoutMaskID
                    ) +
                    " | Has _MaskUVScale: " +
                    runtimeMaterial.HasProperty(
                        MaskUVScaleID
                    ) +
                    " | Has _MaskUVOffset: " +
                    runtimeMaterial.HasProperty(
                        MaskUVOffsetID
                    )
                );
            }
        }

        /*
         * Cache ParticleSystemRenderer materials separately because Particle
         * System shaders require screen-space mapping rather than sprite UVs.
         */
        reactiveParticleMaterials.Clear();
        validReactiveParticleRenderers.Clear();

        if (reactiveParticleRenderers != null)
        {
            foreach (
                ParticleSystemRenderer renderer
                in reactiveParticleRenderers
            )
            {
                if (renderer == null)
                {
                    continue;
                }

                Material runtimeMaterial =
                    renderer.material;

                validReactiveParticleRenderers.Add(
                    renderer
                );

                reactiveParticleMaterials.Add(
                    runtimeMaterial
                );

                Debug.Log(
                    "VFX PARTICLE DEBUG | Renderer: " +
                    renderer.name +
                    " | Material: " +
                    runtimeMaterial.name +
                    " | Has _CutoutMask: " +
                    runtimeMaterial.HasProperty(
                        CutoutMaskID
                    ) +
                    " | Has _MaskScreenMin: " +
                    runtimeMaterial.HasProperty(
                        MaskScreenMinID
                    ) +
                    " | Has _MaskScreenSize: " +
                    runtimeMaterial.HasProperty(
                        MaskScreenSizeID
                    )
                );
            }
        }

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
         * Burst is already managed continuously by LightBurstController.
         * Beam still needs local lifecycle detection and persistence management.
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

            /*
             * Particle shaders need the darkness rectangle converted into
             * viewport coordinates because individual particle UVs cannot
             * represent one shared world-space cut-out.
             */
            UpdateParticleScreenMaskMapping();

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
         * The mask is created at runtime because it represents temporary light
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

        AssignMaskToSpriteVfx();
        AssignMaskToParticleVfx();

        /*
         * Calculate the first particle mapping immediately so the tendrils have
         * valid screen-space coordinates before the first mask update occurs.
         */
        UpdateParticleScreenMaskMapping();

        BuildDynamicMask();
    }

    private void AssignMaskToSpriteVfx()
    {
        /*
         * Every sprite-based VFX layer receives the same runtime cut-out texture,
         * while its own world bounds determine which portion of that texture it
         * samples.
         */
        for (
            int i = 0;
            i < reactiveVfxMaterials.Count;
            i++
        )
        {
            Material reactiveMaterial =
                reactiveVfxMaterials[i];

            SpriteRenderer reactiveRenderer =
                validReactiveVfxRenderers[i];

            if (
                reactiveMaterial == null ||
                reactiveRenderer == null
            )
            {
                continue;
            }

            AssignMaskToSpriteMaterial(
                reactiveMaterial,
                reactiveRenderer.bounds,
                reactiveRenderer.name
            );
        }
    }

    private void AssignMaskToParticleVfx()
    {
        /*
         * Particle materials receive the same cut-out texture as the sprites.
         * Their screen-space alignment is handled separately because each
         * particle billboard has its own independent UV coordinates.
         */
        for (
            int i = 0;
            i < reactiveParticleMaterials.Count;
            i++
        )
        {
            Material reactiveMaterial =
                reactiveParticleMaterials[i];

            ParticleSystemRenderer reactiveRenderer =
                validReactiveParticleRenderers[i];

            if (
                reactiveMaterial == null ||
                reactiveRenderer == null
            )
            {
                continue;
            }

            bool hasCutoutMask =
                reactiveMaterial.HasProperty(
                    CutoutMaskID
                );

            bool hasScreenMin =
                reactiveMaterial.HasProperty(
                    MaskScreenMinID
                );

            bool hasScreenSize =
                reactiveMaterial.HasProperty(
                    MaskScreenSizeID
                );

            Debug.Log(
                "VFX PARTICLE DEBUG | " +
                reactiveRenderer.name +
                " | Has _CutoutMask: " +
                hasCutoutMask +
                " | Has _MaskScreenMin: " +
                hasScreenMin +
                " | Has _MaskScreenSize: " +
                hasScreenSize
            );

            if (!hasCutoutMask)
            {
                Debug.LogWarning(
                    "VFX PARTICLE DEBUG | " +
                    reactiveRenderer.name +
                    " cannot receive the cut-out because its shader does not expose _CutoutMask."
                );

                continue;
            }

            reactiveMaterial.SetTexture(
                CutoutMaskID,
                cutoutMaskTexture
            );
        }
    }

    private void AssignMaskToSpriteMaterial(
        Material reactiveMaterial,
        Bounds reactiveBounds,
        string rendererName
    )
    {
        bool hasCutoutMask =
            reactiveMaterial.HasProperty(
                CutoutMaskID
            );

        bool hasUvScale =
            reactiveMaterial.HasProperty(
                MaskUVScaleID
            );

        bool hasUvOffset =
            reactiveMaterial.HasProperty(
                MaskUVOffsetID
            );

        Debug.Log(
            "VFX DEBUG | " +
            rendererName +
            " | Has _CutoutMask: " +
            hasCutoutMask +
            " | Has _MaskUVScale: " +
            hasUvScale +
            " | Has _MaskUVOffset: " +
            hasUvOffset
        );

        /*
         * A renderer whose shader does not expose _CutoutMask cannot react to
         * Burst or Beam, so it is skipped rather than silently modifying an
         * unrelated shader property.
         */
        if (!hasCutoutMask)
        {
            Debug.LogWarning(
                "VFX DEBUG | " +
                rendererName +
                " cannot receive the cut-out because its shader does not expose _CutoutMask."
            );

            return;
        }

        reactiveMaterial.SetTexture(
            CutoutMaskID,
            cutoutMaskTexture
        );

        Bounds mainBounds =
            darknessRenderer.bounds;

        Vector2 mainSize =
            new Vector2(
                Mathf.Max(
                    mainBounds.size.x,
                    0.0001f
                ),
                Mathf.Max(
                    mainBounds.size.y,
                    0.0001f
                )
            );

        Vector2 maskUVScale =
            new Vector2(
                reactiveBounds.size.x /
                mainSize.x,

                reactiveBounds.size.y /
                mainSize.y
            );

        Vector2 maskUVOffset =
            new Vector2(
                (
                    reactiveBounds.min.x -
                    mainBounds.min.x
                ) /
                mainSize.x,

                (
                    reactiveBounds.min.y -
                    mainBounds.min.y
                ) /
                mainSize.y
            );

        if (hasUvScale)
        {
            reactiveMaterial.SetVector(
                MaskUVScaleID,
                maskUVScale
            );
        }

        if (hasUvOffset)
        {
            reactiveMaterial.SetVector(
                MaskUVOffsetID,
                maskUVOffset
            );
        }
    }

    private void UpdateParticleScreenMaskMapping()
    {
        /*
         * Particle billboards each have their own 0-1 UV space. Using Camera
         * viewport coordinates gives every particle and trail fragment one common
         * coordinate system that matches the main darkness rectangle.
         */
        Camera activeCamera =
            Camera.main;

        if (
            activeCamera == null ||
            darknessRenderer == null
        )
        {
            return;
        }

        Bounds darknessBounds =
            darknessRenderer.bounds;

        Vector3 viewportMin =
            activeCamera.WorldToViewportPoint(
                new Vector3(
                    darknessBounds.min.x,
                    darknessBounds.min.y,
                    darknessBounds.center.z
                )
            );

        Vector3 viewportMax =
            activeCamera.WorldToViewportPoint(
                new Vector3(
                    darknessBounds.max.x,
                    darknessBounds.max.y,
                    darknessBounds.center.z
                )
            );

        Vector2 screenMin =
            new Vector2(
                viewportMin.x,
                viewportMin.y
            );

        /*
         * The minimum size prevents division by zero if the darkness visual is
         * temporarily scaled to an extremely small value.
         */
        Vector2 screenSize =
            new Vector2(
                Mathf.Max(
                    viewportMax.x -
                    viewportMin.x,
                    0.0001f
                ),
                Mathf.Max(
                    viewportMax.y -
                    viewportMin.y,
                    0.0001f
                )
            );

        foreach (
            Material particleMaterial
            in reactiveParticleMaterials
        )
        {
            if (particleMaterial == null)
            {
                continue;
            }

            if (
                particleMaterial.HasProperty(
                    MaskScreenMinID
                )
            )
            {
                particleMaterial.SetVector(
                    MaskScreenMinID,
                    screenMin
                );
            }

            if (
                particleMaterial.HasProperty(
                    MaskScreenSizeID
                )
            )
            {
                particleMaterial.SetVector(
                    MaskScreenSizeID,
                    screenSize
                );
            }
        }
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
             * previously fired corridors can coexist while holding or reforming.
             *
             * Beam-specific darkness behaviour is owned by LightBeamController.
             * These values are copied into this cut-out when firing begins so
             * every Beam corridor keeps its own settings after the shot ends.
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

                    /*
                     * The Beam Inspector exposes the total darkness opening width,
                     * but this mask measures from the Beam centre line to one side.
                     * LightBeamController therefore supplies half of the total width.
                     */
                    maximumBeamPushDistance =
                        lightBeamController.GetDarknessCutoutHalfWidth(),

                    beamLength =
                        lightBeamController.GetLockedBeamLength(),

                    pushDuration =
                        lightBeamController.GetDarknessCutoutExpansionDuration(),

                    holdDuration =
                        lightBeamController.GetDarknessCutoutHoldDuration(),

                    reformDuration =
                        lightBeamController.GetDarknessCutoutReformDuration()
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
            /*
             * Expansion uses the settings captured when this particular Beam
             * fired so its opening remains independent of later Beam shots.
             */
            float pushSpeed =
                liveBeamCutout.maximumBeamPushDistance /
                Mathf.Max(
                    liveBeamCutout.pushDuration,
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