using UnityEngine;

public class MovingPlatformReceiver : MonoBehaviour
{
    [Header("Puzzle")]
    // This receiver can complete a larger Light puzzle when illuminated.
    // Keeping the reference optional also allows the receiver to be reused
    // later for temporary platform puzzles that should not permanently solve.
    [SerializeField] private LightPuzzleController puzzleController;

    [SerializeField]
    private bool completesPuzzleOnActivate = true;

    [Header("Moving Platform")]
    public GameObject MovingPlatform;

    public Vector3 MP_Origin;
    public Vector3 MP_EndGoal;
    public float Speed;

    private Vector3 MP_Target;

    // Start is called once before the first execution of Update after the
    // MonoBehaviour is created.
    void Start()
    {
        // Report a clear setup error rather than allowing an unclear
        // null-reference error when the laser reaches the receiver.
        if (MovingPlatform == null)
        {
            Debug.LogError(
                "MovingPlatformReceiver requires a MovingPlatform to be assigned.",
                this
            );

            return;
        }

        // Starting at the origin target ensures the platform remains in its
        // intended resting state until the receiver is illuminated.
        MP_Target = MP_Origin;
    }

    // Update is called once per frame.
    void Update()
    {
        if (MovingPlatform == null)
        {
            return;
        }

        // The platform continuously approaches its current target so Activate
        // and DeActivate only need to decide which location is appropriate.
        float step =
            Speed *
            Time.deltaTime;

        MovingPlatform.transform.position =
            Vector3.MoveTowards(
                MovingPlatform.transform.position,
                MP_Target,
                step
            );
    }

    public void Activate()
    {
        // Only move the platform when its Inspector reference is valid.
        if (MovingPlatform != null)
        {
            MP_Target = MP_EndGoal;
        }

        // The introductory puzzle becomes permanently solved the first time
        // its intended receiver is successfully illuminated.
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
        // A permanently solved puzzle should never send its platform back to
        // the origin when the temporary laser eventually switches off.
        if (
            puzzleController != null &&
            puzzleController.IsSolved()
        )
        {
            MP_Target = MP_EndGoal;
            return;
        }

        // Temporary or unsolved puzzles still return towards their origin when
        // the receiver is no longer illuminated.
        if (MovingPlatform != null)
        {
            MP_Target = MP_Origin;
        }
    }
}