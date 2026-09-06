using System.Collections.Generic;
using UnityEngine;

public class LightPuzzleController : MonoBehaviour
{
    [Header("Puzzle State")]

    // This value is serialised so we can watch the puzzle change between
    // unsolved and solved during Play Mode while testing the level.
    [SerializeField] private bool isSolved = false;

    [Header("Solved Visual Feedback")]

    // These renderers belong only to this puzzle instance. Keeping the list on
    // the shared controller means solving one puzzle changes only the pieces
    // assigned to that puzzle rather than affecting other puzzle sections.
    [SerializeField]
    private List<SpriteRenderer> puzzleRenderers =
        new List<SpriteRenderer>();

    // #FFDA73 is the shared default solved colour because the warm gold stands
    // out clearly against the level's blue/teal environment and communicates
    // that the entire light puzzle has reached a completed state.
    [SerializeField]
    private Color solvedColour =
        new Color32(
            255,
            218,
            115,
            255
        );

    [Header("Debug")]

    // Logging can be disabled once the puzzle behaviour has been confirmed.
    [SerializeField] private bool showDebugLogs = true;

    // Each puzzle piece may begin with a different colour, so storing them
    // individually lets a fresh playthrough restore every renderer correctly.
    private readonly List<Color> originalColours =
        new List<Color>();

    private void Awake()
    {
        // Every scene instance begins unsolved when gameplay starts.
        // This prevents the runtime completion state from carrying into a
        // fresh playthrough of the level.
        isSolved = false;

        CacheOriginalColours();
        RestoreOriginalColours();

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " started UNSOLVED.",
                this
            );
        }
    }

    private void CacheOriginalColours()
    {
        originalColours.Clear();

        // Each renderer's starting colour is stored separately because mirrors,
        // receivers and laser pieces may use different artwork or tint values.
        foreach (SpriteRenderer puzzleRenderer in puzzleRenderers)
        {
            if (puzzleRenderer == null)
            {
                originalColours.Add(
                    Color.white
                );

                continue;
            }

            originalColours.Add(
                puzzleRenderer.color
            );
        }
    }

    private void RestoreOriginalColours()
    {
        // Restoring the original appearance at the start of gameplay keeps the
        // unsolved state visually consistent even after repeated Play Mode tests.
        for (
            int i = 0;
            i < puzzleRenderers.Count;
            i++
        )
        {
            SpriteRenderer puzzleRenderer =
                puzzleRenderers[i];

            if (puzzleRenderer == null)
            {
                continue;
            }

            if (i >= originalColours.Count)
            {
                continue;
            }

            puzzleRenderer.color =
                originalColours[i];
        }
    }

    public void SolvePuzzle()
    {
        // A puzzle only needs to complete once during the current playthrough.
        if (isSolved)
        {
            return;
        }

        isSolved = true;

        ApplySolvedVisuals();

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " changed to SOLVED.",
                this
            );
        }
    }

    private void ApplySolvedVisuals()
    {
        for (
            int i = 0;
            i < puzzleRenderers.Count;
            i++
        )
        {
            SpriteRenderer puzzleRenderer =
                puzzleRenderers[i];

            if (puzzleRenderer == null)
            {
                continue;
            }

            /*
             * The solved RGB colour is shared across the puzzle so all pieces
             * visually read as belonging to one completed system. The original
             * alpha is preserved so translucent artwork does not unexpectedly
             * become opaque when the puzzle is solved.
             */
            Color rendererSolvedColour =
                solvedColour;

            if (i < originalColours.Count)
            {
                rendererSolvedColour.a =
                    originalColours[i].a;
            }
            else
            {
                rendererSolvedColour.a =
                    puzzleRenderer.color.a;
            }

            puzzleRenderer.color =
                rendererSolvedColour;
        }
    }

    public bool IsSolved()
    {
        // Mirrors, LaserPointers and receivers all query this shared state so
        // every component belonging to this puzzle agrees on its completion.
        return isSolved;
    }

    private void OnValidate()
    {
        /*
         * Unity keeps serialised Inspector values even when a script's default
         * value changes. This migration recognises the previous teal default
         * used by LightPuzzleController and replaces it with the new gold
         * #FFDA73 default automatically across existing puzzle instances.
         *
         * Colours that have been deliberately customised to something else are
         * left untouched so this does not overwrite future per-puzzle choices.
         */
        Color previousDefaultColour =
            new Color(
                0.45f,
                1f,
                0.75f,
                1f
            );

        Color newDefaultColour =
            new Color32(
                255,
                218,
                115,
                255
            );

        if (
            ColoursApproximatelyMatch(
                solvedColour,
                previousDefaultColour
            )
        )
        {
            solvedColour =
                newDefaultColour;
        }
    }

    private bool ColoursApproximatelyMatch(
        Color firstColour,
        Color secondColour
    )
    {
        // A small tolerance accounts for minor floating-point differences in
        // Unity's serialised colour values while still protecting custom colours.
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