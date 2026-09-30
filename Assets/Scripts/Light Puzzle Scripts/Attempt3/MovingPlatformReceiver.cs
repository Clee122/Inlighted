using System.Collections;
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

    public float Speed = 0.5f;

    public cameramanage CameraManager;
    public float maxOrtho;
    public Transform CameraSpace;

    private bool cameraShown = false;
    private Vector3 MP_Target;

    [Header("SFX")]
    public AudioSource DoorMovingSFX;

    [Header("VFX")]
    // This stationary holder contains the door movement VFX. It remains separate
    // from MovingPlatform so the smoke stays at the chosen doorway position
    // instead of following the door as it moves towards its destination.
    [SerializeField] private GameObject DoorVFXPosition;

    private bool doorVFXActive = false;

    [Header("Solved Visuals")]
    // The Off and On artwork are separate objects so the artist's original
    // visuals can be preserved. Solving the puzzle permanently switches which
    // version is visible without modifying either SpriteRenderer at runtime.
    [SerializeField] private GameObject receiverOffVisual;
    [SerializeField] private GameObject receiverOnVisual;

    [SerializeField] private GameObject doorOffVisual;
    [SerializeField] private GameObject doorOnVisual;

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

        // The VFX begins hidden and is only enabled when this receiver actually
        // tells the door to start moving towards its solved destination.
        if (DoorVFXPosition != null)
        {
            DoorVFXPosition.SetActive(false);
        }

        // The puzzle always begins with its unpowered artwork visible. Setting
        // this here also prevents an On object accidentally left enabled in the
        // hierarchy from appearing before the puzzle has been solved.
        SetSolvedVisuals(false);
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

        // The VFX holder is disabled once the door reaches its destination.
        // A small tolerance avoids relying on exact floating-point equality.
        bool reachedEnd =
            Vector3.Distance(
                MovingPlatform.transform.localPosition,
                MP_EndGoal
            ) <= 0.001f;

        if (
            doorVFXActive &&
            reachedEnd
        )
        {
            if (DoorVFXPosition != null)
            {
                DoorVFXPosition.SetActive(false);
            }

            doorVFXActive = false;
        }
    }

    public void Activate()
    {
        // The puzzle is solved immediately so puzzle completion does not depend
        // on the camera reaching CameraSpace. This preserves the fix where a
        // missing or incorrect camera reference could prevent the solved state.
        if (
            completesPuzzleOnActivate &&
            puzzleController != null
        )
        {
            puzzleController.SolvePuzzle();

            // The receiver and door permanently change to their powered artwork
            // as soon as the puzzle enters its solved state.
            SetSolvedVisuals(true);
        }

        // The door must wait for this puzzle's camera sequence before opening.
        // cameraShown only prevents this particular camera pan from repeating;
        // it does not affect camera sequences belonging to other puzzles.
        if (
            !cameraShown &&
            CameraManager != null &&
            CameraSpace != null
        )
        {
            cameraShown = true;

            CameraManager.Movetocameraspace(
                CameraSpace
            );

            StartCoroutine(
                WaitForCamera()
            );

            return;
        }

        // Some receivers may not use a camera sequence. In that case the door
        // is allowed to move immediately so a missing optional camera setup
        // cannot leave the puzzle permanently stuck.
        if (
            CameraManager == null ||
            CameraSpace == null
        )
        {
            StartDoorMovement();
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

    private IEnumerator WaitForCamera()
    {
        // Door movement is deliberately delayed until the camera reaches the
        // puzzle's CameraSpace so the player sees the door before it begins opening.
        yield return new WaitUntil(() =>
            CameraManager != null &&
            CameraSpace != null &&
            CameraManager.IsCameraAt(
                CameraSpace
            )
        );

        if (MovingPlatform != null)
        {
            StartDoorMovement();
        }
    }

    private void StartDoorMovement()
    {
        // The door receives its destination only after the camera has reached
        // the intended view. Keeping movement, VFX and audio together ensures
        // all three begin at the same point in the puzzle sequence.
        MP_Target =
            MP_EndGoal;

        if (DoorMovingSFX != null)
        {
            DoorMovingSFX.Play();
        }

        if (DoorVFXPosition != null)
        {
            DoorVFXPosition.SetActive(true);
            doorVFXActive = true;
        }
    }

    private void SetSolvedVisuals(bool solved)
    {
        // Switching the complete visual objects keeps the artist's Off and On
        // states independent and makes the current puzzle state explicit.
        if (receiverOffVisual != null)
        {
            receiverOffVisual.SetActive(!solved);
        }

        if (receiverOnVisual != null)
        {
            receiverOnVisual.SetActive(solved);
        }

        if (doorOffVisual != null)
        {
            doorOffVisual.SetActive(!solved);
        }

        if (doorOnVisual != null)
        {
            doorOnVisual.SetActive(solved);
        }
    }
}