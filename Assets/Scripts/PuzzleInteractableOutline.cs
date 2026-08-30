using System.Collections.Generic;
using UnityEngine;

public class PuzzleInteractableOutline : MonoBehaviour
{
    [Header("Outline Target")]

    // The outline uses the existing puzzle-piece artwork so mirrors, laser
    // sources and later replacement art can all share the same interaction system.
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Outline Appearance")]

    // A bright yellow outline communicates that this puzzle piece can currently
    // be interacted with while remaining distinct from the permanent solved state.
    [SerializeField]
    private Color outlineColour =
        new Color32(
            255,
            230,
            109,
            255
        );

    // Outline thickness is measured approximately in world-space units rather
    // than by enlarging the sprite. This keeps thin or heavily stretched puzzle
    // pieces readable on every side instead of only showing above and below.
    [SerializeField] private float outlineThickness = 0.06f;

    // Drawing the outline behind the original artwork allows the coloured
    // copies to remain visible only around the outer edge of the puzzle piece.
    [SerializeField] private int sortingOrderOffset = -1;

    private readonly List<SpriteRenderer> outlineRenderers =
        new List<SpriteRenderer>();

    private readonly List<Transform> outlineTransforms =
        new List<Transform>();

    private bool isVisible;

    private void Awake()
    {
        FindTargetRenderer();
        CreateOutlineRenderers();

        // Puzzle pieces begin without feedback until their interaction script
        // confirms that the player is close enough to use them.
        SetVisible(false);
    }

    private void FindTargetRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        targetRenderer =
            GetComponent<SpriteRenderer>();

        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponentInChildren<SpriteRenderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogWarning(
                "PuzzleInteractableOutline could not find a SpriteRenderer. " +
                "Assign the visible puzzle-piece SpriteRenderer in the Inspector.",
                this
            );
        }
    }

    private void CreateOutlineRenderers()
    {
        if (targetRenderer == null)
        {
            return;
        }

        /*
         * Eight copies surround the real sprite from every direction.
         * Using offsets instead of a larger duplicate gives long, thin or
         * non-uniformly scaled puzzle objects a much more consistent outline.
         */
        for (int i = 0; i < 8; i++)
        {
            GameObject outlineObject =
                new GameObject(
                    "InteractionOutline_" + i
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

            outlineTransforms.Add(
                outlineTransform
            );

            outlineRenderers.Add(
                outlineRenderer
            );
        }

        UpdateOutlineOffsets();
        SyncOutlineWithTarget();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null)
        {
            return;
        }

        /*
         * Synchronising every frame keeps the outline compatible with mirrors
         * rotating and with future sprite swaps or renderer changes.
         */
        UpdateOutlineOffsets();
        SyncOutlineWithTarget();
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
         * The target may be heavily stretched, such as the current mirror
         * placeholder with a very small X scale. Local offsets would therefore
         * become almost invisible horizontally.
         *
         * Dividing by the target's world scale compensates for that distortion,
         * giving the outline approximately equal thickness in world space.
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

        float localHorizontalOffset =
            outlineThickness /
            safeScaleX;

        float localVerticalOffset =
            outlineThickness /
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

        foreach (SpriteRenderer outlineRenderer in outlineRenderers)
        {
            if (outlineRenderer == null)
            {
                continue;
            }

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

            outlineRenderer.sortingOrder =
                targetRenderer.sortingOrder +
                sortingOrderOffset;

            outlineRenderer.sharedMaterial =
                targetRenderer.sharedMaterial;

            outlineRenderer.maskInteraction =
                targetRenderer.maskInteraction;

            outlineRenderer.enabled =
                isVisible;
        }
    }

    public void SetVisible(
        bool shouldBeVisible
    )
    {
        isVisible =
            shouldBeVisible;

        // Only the generated outline renderers are toggled. The actual puzzle
        // artwork remains untouched regardless of interaction range.
        foreach (SpriteRenderer outlineRenderer in outlineRenderers)
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.enabled =
                    shouldBeVisible;
            }
        }
    }

    private void OnValidate()
    {
        // Preventing a negative thickness avoids accidentally placing the
        // generated copies inside the original puzzle-piece artwork.
        outlineThickness =
            Mathf.Max(
                0f,
                outlineThickness
            );

        /*
         * Unity keeps Inspector values that were already serialised before the
         * script default changed. This recognises the previous cyan default and
         * automatically replaces it with the new yellow interaction colour.
         *
         * Any outline that was deliberately customised to another colour is left
         * unchanged so future per-object adjustments are preserved.
         */
        Color previousDefaultColour =
            new Color32(
                110,
                220,
                255,
                255
            );

        Color newDefaultColour =
            new Color32(
                255,
                230,
                109,
                255
            );

        if (
            ColoursApproximatelyMatch(
                outlineColour,
                previousDefaultColour
            )
        )
        {
            outlineColour =
                newDefaultColour;
        }
    }

    private bool ColoursApproximatelyMatch(
        Color firstColour,
        Color secondColour
    )
    {
        // A small tolerance accounts for Unity's floating-point colour
        // serialisation while still protecting deliberately customised colours.
        const float tolerance = 0.01f;

        return
            Mathf.Abs(
                firstColour.r -
                secondColour.r
            ) <= tolerance &&
            Mathf.Abs(
                firstColour.g -
                secondColour.g
            ) <= tolerance &&
            Mathf.Abs(
                firstColour.b -
                secondColour.b
            ) <= tolerance &&
            Mathf.Abs(
                firstColour.a -
                secondColour.a
            ) <= tolerance;
    }
}