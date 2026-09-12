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

    private Vector3 startingScale;
    private Vector3 startingLocalPosition;

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
    }
}
