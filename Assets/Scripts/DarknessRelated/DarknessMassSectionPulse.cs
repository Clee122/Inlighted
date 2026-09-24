using System.Collections.Generic;
using UnityEngine;

public class DarknessMassSectionPulse : MonoBehaviour
{
    [Header("Scale Movement")]

    /*
     * Each darkness section changes size around its original scale rather than
     * replacing it. This allows several overlapping sections to behave like one
     * continuous mass while still expanding and contracting independently.
     */
    [SerializeField]
    private Vector2 scaleAmplitude =
        new Vector2(
            0.08f,
            0.12f
        );

    /*
     * Different X and Y speeds prevent the section from simply growing and
     * shrinking uniformly like a normal pulse. The uneven motion makes the
     * darkness feel less mechanical.
     */
    [SerializeField]
    private Vector2 scaleSpeed =
        new Vector2(
            0.7f,
            0.9f
        );

    /*
     * Phase offset allows neighbouring darkness sections to move at different
     * points in their cycle so the entire darkness mass does not breathe in sync.
     */
    [SerializeField]
    private float phaseOffset = 0f;

    [Header("Position Drift")]

    /*
     * A very small positional drift helps hide the fixed rectangular boundary.
     * The values should stay subtle so neighbouring sections continue overlapping.
     */
    [SerializeField]
    private Vector2 positionAmplitude =
        new Vector2(
            0.03f,
            0.04f
        );

    [SerializeField]
    private Vector2 positionSpeed =
        new Vector2(
            0.45f,
            0.6f
        );

    [Header("Player Proximity Outline")]

    /*
     * The player reference is used only for readability feedback. The darkness
     * should warn the player when they approach it without changing the actual
     * gameplay collision or light-cutout behaviour.
     */
    [SerializeField]
    private Transform playerTransform;

    /*
     * Distance is measured from the nearest point of the darkness renderer rather
     * than from its centre. This makes the warning behave sensibly for long or
     * stretched darkness sections.
     */
    [SerializeField]
    private float outlineActivationDistance = 4f;

    /*
     * Proximity does not need to be checked every rendered frame. A short interval
     * keeps the feedback responsive while avoiding unnecessary repeated distance
     * checks across many darkness sections.
     */
    [SerializeField]
    private float outlineCheckInterval = 0.1f;

    [Header("Outline Appearance")]

    /*
     * Red separates the darkness warning from the yellow interaction outline used
     * by puzzle pieces and reinforces the darkness hazard's existing red language.
     */
    [SerializeField]
    private Color outlineColour =
        new Color32(
            200,
            20,
            20,
            255
        );

    /*
     * Separate horizontal and vertical values allow the side and top/bottom
     * borders to be balanced independently without reusing the old serialised
     * single-float outlineThickness field.
     *
     * X controls the left/right border width.
     * Y controls the top/bottom border width.
     */
    [SerializeField]
    private Vector2 outlineThicknessXY =
        new Vector2(
            0.06f,
            0.10f
        );

    /*
     * The outline copies remain on the DarknessMass' original authored sorting
     * order. The real DarknessMass is moved one order above them at runtime so
     * its centre covers the red copies and leaves only their exposed edges visible.
     */
    [SerializeField]
    private int outlineSortingOrderOffset = 0;

    /*
     * The outline uses a second material based on the same darkness Shader Graph.
     * Its OutlineMode property is enabled so the same organic silhouette renders
     * as solid red while still supporting the shared Burst/Beam cut-out mask.
     */
    [SerializeField]
    private Material outlineMaterial;

    private Vector3 startingScale;
    private Vector3 startingLocalPosition;

    private SpriteRenderer targetRenderer;

    /*
     * The original sorting order is preserved so the generated red copies can
     * remain at the authored layer while the real DarknessMass is drawn one step
     * above them. This prevents the red copies from covering the darkness body.
     */
    private int startingSortingOrder;

    /*
     * The cut-out controller owns the CPU-generated Burst/Beam mask. Runtime
     * outline renderers register with it so the red border disappears through
     * the exact same openings as the main darkness.
     */
    private DarknessCutoutController darknessCutoutController;

    private readonly List<SpriteRenderer> outlineRenderers =
        new List<SpriteRenderer>();

    private readonly List<Transform> outlineTransforms =
        new List<Transform>();

    private float outlineCheckTimer = 0f;
    private bool outlineVisible = false;

    private void Awake()
    {
        /*
         * The authored transform remains the centre of the animation so level
         * designers can resize or reposition a darkness section without needing
         * to modify this script.
         */
        startingScale =
            transform.localScale;

        startingLocalPosition =
            transform.localPosition;

        /*
         * The existing DarknessMass SpriteRenderer is reused as the visual source
         * for the outline so the border inherits the same sprite and sorting layer.
         */
        targetRenderer =
            GetComponent<SpriteRenderer>();

        if (targetRenderer != null)
        {
            /*
             * Preserve the authored sorting order so the generated outline copies
             * can remain at that level while the real darkness draws immediately
             * above them. This keeps the red visible only around exposed edges.
             */
            startingSortingOrder =
                targetRenderer.sortingOrder;

            targetRenderer.sortingOrder =
                startingSortingOrder + 1;
        }

        /*
         * DarknessCutoutController owns the shared runtime mask. Keeping this
         * reference lets every generated outline copy register for the same mask
         * immediately after its material has been assigned.
         */
        darknessCutoutController =
            GetComponent<DarknessCutoutController>();

        /*
         * Recovering the player by tag keeps prefab setup convenient while still
         * allowing an explicit Inspector reference where one is already available.
         */
        if (playerTransform == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );

            if (player != null)
            {
                playerTransform =
                    player.transform;
            }
        }

        if (targetRenderer == null)
        {
            Debug.LogWarning(
                "DarknessMassSectionPulse could not find a SpriteRenderer for the proximity outline.",
                this
            );
        }
        else
        {
            CreateOutlineRenderers();
            SetOutlineVisible(false);
        }
    }

    private void Update()
    {
        float time =
            Time.time +
            phaseOffset;

        /*
         * X and Y use different sine cycles so the darkness does not retain the
         * appearance of a uniformly scaling rectangle.
         */
        float scaleX =
            1f +
            Mathf.Sin(
                time *
                scaleSpeed.x
            ) *
            scaleAmplitude.x;

        float scaleY =
            1f +
            Mathf.Sin(
                (
                    time +
                    1.37f
                ) *
                scaleSpeed.y
            ) *
            scaleAmplitude.y;

        transform.localScale =
            new Vector3(
                startingScale.x *
                scaleX,

                startingScale.y *
                scaleY,

                startingScale.z
            );

        /*
         * Small independent drift prevents the two sections from sharing a
         * perfectly static seam while their overlap keeps the mass connected.
         */
        float offsetX =
            Mathf.Sin(
                time *
                positionSpeed.x
            ) *
            positionAmplitude.x;

        float offsetY =
            Mathf.Sin(
                (
                    time +
                    2.11f
                ) *
                positionSpeed.y
            ) *
            positionAmplitude.y;

        transform.localPosition =
            startingLocalPosition +
            new Vector3(
                offsetX,
                offsetY,
                0f
            );

        UpdateOutlineProximity();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null)
        {
            return;
        }

        /*
         * Recalculating the outline offsets after the pulse movement keeps the
         * border thickness visually consistent as the darkness scales and drifts.
         */
        UpdateOutlineOffsets();
        SyncOutlineWithTarget();
    }

    private void CreateOutlineRenderers()
    {
        /*
         * Eight copies cover horizontal, vertical and diagonal directions.
         * Parenting them directly to DarknessMass means they automatically inherit
         * this script's scale and position movement without requiring another script.
         */
        for (int i = 0; i < 8; i++)
        {
            GameObject outlineObject =
                new GameObject(
                    "DarknessProximityOutline_" + i
                );

            Transform outlineTransform =
                outlineObject.transform;

            outlineTransform.SetParent(
                targetRenderer.transform,
                false
            );

            outlineTransform.localRotation =
                Quaternion.identity;

            outlineTransform.localScale =
                Vector3.one;

            SpriteRenderer outlineRenderer =
                outlineObject.AddComponent<SpriteRenderer>();

            /*
             * The dedicated outline material must be assigned before registration.
             * DarknessCutoutController creates an independent runtime material from
             * the renderer's current material so each shifted copy can keep its own
             * _MaskUVOffset without overwriting the other seven copies.
             */
            outlineRenderer.sharedMaterial =
                outlineMaterial != null
                    ? outlineMaterial
                    : targetRenderer.sharedMaterial;

            outlineTransforms.Add(
                outlineTransform
            );

            outlineRenderers.Add(
                outlineRenderer
            );

            /*
             * The outline uses the same Shader Graph cut-out properties as the
             * main darkness. Registering each copy here gives it the same runtime
             * _CutoutMask while preserving an individual UV mapping for its offset.
             */
            if (darknessCutoutController != null)
            {
                darknessCutoutController.RegisterRuntimeOutlineRenderer(
                    outlineRenderer
                );
            }
        }

        UpdateOutlineOffsets();
        SyncOutlineWithTarget();
    }

    private void UpdateOutlineProximity()
    {
        if (
            playerTransform == null ||
            targetRenderer == null
        )
        {
            return;
        }

        outlineCheckTimer +=
            Time.deltaTime;

        if (
            outlineCheckTimer <
            outlineCheckInterval
        )
        {
            return;
        }

        outlineCheckTimer = 0f;

        /*
         * Using the closest point of the renderer bounds means a large darkness
         * section warns the player when they approach any edge, rather than only
         * when they move near the centre of the sprite.
         */
        Vector3 closestPoint =
            targetRenderer.bounds.ClosestPoint(
                playerTransform.position
            );

        float distanceToDarkness =
            Vector2.Distance(
                playerTransform.position,
                closestPoint
            );

        bool shouldShowOutline =
            distanceToDarkness <=
            outlineActivationDistance;

        if (
            shouldShowOutline !=
            outlineVisible
        )
        {
            SetOutlineVisible(
                shouldShowOutline
            );
        }
    }

    private void UpdateOutlineOffsets()
    {
        if (
            targetRenderer == null ||
            outlineTransforms.Count != 8
        )
        {
            return;
        }

        /*
         * Dividing each desired world-space thickness by the current lossy scale
         * prevents the border becoming disproportionately thick or thin while the
         * DarknessMass pulse changes its X and Y scale.
         */
        Vector3 lossyScale =
            targetRenderer.transform.lossyScale;

        float safeScaleX =
            Mathf.Max(
                Mathf.Abs(lossyScale.x),
                0.0001f
            );

        float safeScaleY =
            Mathf.Max(
                Mathf.Abs(lossyScale.y),
                0.0001f
            );

        /*
         * X controls the visible left/right outline width while Y independently
         * controls the top/bottom width. This lets the organic silhouette be
         * visually balanced even when equal offsets look different on screen.
         */
        float localHorizontalOffset =
            outlineThicknessXY.x /
            safeScaleX;

        float localVerticalOffset =
            outlineThicknessXY.y /
            safeScaleY;

        Vector3[] offsets =
        {
            new Vector3(
                localHorizontalOffset,
                0f,
                0f
            ),

            new Vector3(
                -localHorizontalOffset,
                0f,
                0f
            ),

            new Vector3(
                0f,
                localVerticalOffset,
                0f
            ),

            new Vector3(
                0f,
                -localVerticalOffset,
                0f
            ),

            new Vector3(
                localHorizontalOffset,
                localVerticalOffset,
                0f
            ),

            new Vector3(
                -localHorizontalOffset,
                localVerticalOffset,
                0f
            ),

            new Vector3(
                localHorizontalOffset,
                -localVerticalOffset,
                0f
            ),

            new Vector3(
                -localHorizontalOffset,
                -localVerticalOffset,
                0f
            )
        };

        for (
            int i = 0;
            i < outlineTransforms.Count;
            i++
        )
        {
            outlineTransforms[i].localPosition =
                offsets[i];
        }
    }

    private void SyncOutlineWithTarget()
    {
        if (targetRenderer == null)
        {
            return;
        }

        foreach (
            SpriteRenderer outlineRenderer
            in outlineRenderers
        )
        {
            if (outlineRenderer == null)
            {
                continue;
            }

            /*
             * The generated copies continue following renderer-level properties
             * such as sprite changes and sorting, while their runtime materials are
             * left untouched so DarknessCutoutController can preserve independent
             * cut-out UV mappings for every outline copy.
             */
            outlineRenderer.sprite =
                targetRenderer.sprite;

            outlineRenderer.color =
                outlineColour;

            outlineRenderer.flipX =
                targetRenderer.flipX;

            outlineRenderer.flipY =
                targetRenderer.flipY;

            outlineRenderer.sortingLayerID =
                targetRenderer.sortingLayerID;

            /*
             * The red copies stay at the original authored sorting order while the
             * real DarknessMass renders one order above. The real darkness therefore
             * covers the centre of each copy and leaves only the offset edge visible.
             */
            outlineRenderer.sortingOrder =
                startingSortingOrder +
                outlineSortingOrderOffset;

            /*
             * Do not assign sharedMaterial here. Each outline renderer now owns a
             * runtime material instance created by DarknessCutoutController. Replacing
             * that material every frame would discard its individual cut-out mapping.
             */
            outlineRenderer.maskInteraction =
                targetRenderer.maskInteraction;

            outlineRenderer.enabled =
                outlineVisible;
        }
    }

    private void SetOutlineVisible(
        bool shouldBeVisible
    )
    {
        outlineVisible =
            shouldBeVisible;

        /*
         * Only the generated border copies are toggled. The actual DarknessMass,
         * its existing red interior and its cut-out shader remain untouched.
         */
        foreach (
            SpriteRenderer outlineRenderer
            in outlineRenderers
        )
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.enabled =
                    shouldBeVisible;
            }
        }
    }

    private void OnDestroy()
    {
        /*
         * Runtime outline renderers unregister from the cut-out controller before
         * this object is destroyed so the controller does not retain references to
         * generated SpriteRenderers that no longer exist.
         */
        if (darknessCutoutController != null)
        {
            foreach (
                SpriteRenderer outlineRenderer
                in outlineRenderers
            )
            {
                if (outlineRenderer == null)
                {
                    continue;
                }

                darknessCutoutController.UnregisterRuntimeOutlineRenderer(
                    outlineRenderer
                );
            }
        }

        /*
         * Restore the authored sorting order so leaving Play Mode or destroying
         * this component does not leave the real DarknessMass permanently raised.
         */
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder =
                startingSortingOrder;
        }
    }

    private void OnValidate()
    {
        /*
         * Preventing negative values keeps proximity behaviour and world-space
         * outline offsets predictable when settings are edited in the Inspector.
         */
        outlineActivationDistance =
            Mathf.Max(
                0f,
                outlineActivationDistance
            );

        outlineCheckInterval =
            Mathf.Max(
                0.02f,
                outlineCheckInterval
            );

        /*
         * Both axis values stay non-negative so the generated copies remain outside
         * the DarknessMass rather than moving through its centre.
         */
        outlineThicknessXY =
            new Vector2(
                Mathf.Max(
                    0f,
                    outlineThicknessXY.x
                ),
                Mathf.Max(
                    0f,
                    outlineThicknessXY.y
                )
            );
    }
}