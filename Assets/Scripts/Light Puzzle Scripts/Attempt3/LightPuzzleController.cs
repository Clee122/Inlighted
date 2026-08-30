using UnityEngine;

public class LightPuzzleController : MonoBehaviour
{
    [Header("Puzzle State")]
    // This value is serialised so we can watch the puzzle change between
    // unsolved and solved during Play Mode while testing the level.
    [SerializeField] private bool isSolved = false;

    [Header("Debug")]
    // Logging can be disabled once the puzzle behaviour has been confirmed.
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        // Every scene instance begins unsolved when gameplay starts.
        // This prevents the runtime completion state from carrying into a
        // fresh playthrough of the level.
        isSolved = false;

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " started UNSOLVED.",
                this
            );
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

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " changed to SOLVED.",
                this
            );
        }
    }

    public bool IsSolved()
    {
        // Mirrors, LaserPointers and receivers all query this shared state so
        // every component belonging to this puzzle agrees on its completion.
        return isSolved;
    }
}