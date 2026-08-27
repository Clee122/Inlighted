using UnityEngine;

public class LightPuzzleController : MonoBehaviour
{
    [Header("Puzzle State")]
    // This shared solved state belongs only to this specific puzzle instance.
    // Other puzzles should use their own LightPuzzleController component.
    [SerializeField] private bool isSolved = false;

    [Header("Debug")]
    // This log makes it easier to confirm when the current puzzle is solved
    // during testing without changing any of the gameplay behaviour.
    [SerializeField] private bool showDebugLogs = true;

    public void SolvePuzzle()
    {
        // The introductory puzzle only needs to be solved once, so repeated
        // receiver hits should not trigger the completion logic again.
        if (isSolved)
        {
            return;
        }

        isSolved = true;

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " has been permanently solved.",
                this
            );
        }
    }

    public bool IsSolved()
    {
        // Mirrors, the LaserPointer and receivers use this one shared state
        // so they all agree on whether this particular puzzle is complete.
        return isSolved;
    }
}