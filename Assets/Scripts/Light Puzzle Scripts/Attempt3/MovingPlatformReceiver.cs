using UnityEngine;

public class MovingPlatformReceiver : MonoBehaviour
{
    [Header("Puzzle")]
    // This receiver can permanently complete the puzzle when illuminated.
    // Each puzzle should reference its own LightPuzzleController instance.
    [SerializeField] private LightPuzzleController puzzleController;

    [SerializeField] private bool completesPuzzleOnActivate = true;

    [Header("Moving Platform")]
    public GameObject MovingPlatform;

    // The origin is recorded automatically from the platform's starting local
    // position. This prevents copied or moved puzzles from using outdated
    // coordinates and trying to reposition themselves as soon as Play begins.
    [SerializeField] private Vector3 MP_Origin;

    // The end goal remains editable because this is the destination the level
    // designer intentionally chooses for this particular platform.
    public Vector3 MP_EndGoal;

    public float Speed = 2f;

    private Vector3 MP_Target;

    private void Start()
    {
        if (MovingPlatform == null)
        {
            Debug.LogError(
                "MovingPlatformReceiver requires a MovingPlatform to be assigned.",
                this
            );

            return;
        }

        // Wherever the platform has been placed inside the puzzle becomes its
        // resting position. This makes the puzzle safe to reposition as a group.
        MP_Origin =
            MovingPlatform.transform.localPosition;

        // Beginning with the target equal to the exact current position ensures
        // the platform remains completely stationary until the receiver activates.
        MP_Target =
            MP_Origin;
    }

    private void Update()
    {
        if (MovingPlatform == null)
        {
            return;
        }

        float step =
            Speed *
            Time.deltaTime;

        // Local movement keeps the platform positions relative to the LightPuzzle
        // parent so the entire puzzle can be moved around the level safely.
        MovingPlatform.transform.localPosition =
            Vector3.MoveTowards(
                MovingPlatform.transform.localPosition,
                MP_Target,
                step
            );
    }

    public void Activate()
    {
        if (MovingPlatform != null)
        {
            // The receiver changes the destination only when the laser actually
            // reaches it. Until this happens, the target remains at the origin.
            MP_Target =
                MP_EndGoal;
        }

        if (
            completesPuzzleOnActivate &&
            puzzleController != null
        )
        {
            puzzleController.SolvePuzzle();
        }
    }

    public void DeActivate()
    {
        // A permanently completed puzzle keeps the platform at its end position
        // after the temporary laser switches off.
        if (
            puzzleController != null &&
            puzzleController.IsSolved()
        )
        {
            MP_Target =
                MP_EndGoal;

            return;
        }

        if (MovingPlatform != null)
        {
            // An unsolved puzzle returns to the exact local position that was
            // recorded when gameplay began.
            MP_Target =
                MP_Origin;
        }
    }
}