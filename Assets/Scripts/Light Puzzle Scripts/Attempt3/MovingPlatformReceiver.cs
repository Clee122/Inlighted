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
        // The puzzle is solved immediately when the receiver is activated.
        // Puzzle completion no longer depends on the camera successfully
        // reaching CameraSpace.
        if (
            completesPuzzleOnActivate &&
            puzzleController != null
        )
        {
            puzzleController.SolvePuzzle();

            // The receiver and door permanently change to their powered artwork
            // at the same moment the puzzle enters its solved state.
            SetSolvedVisuals(true);

            // Audio feedback is optional, so a missing AudioSource should never
            // interrupt puzzle completion or prevent the platform from moving.
            if (DoorMovingSFX != null)
            {
                DoorMovingSFX.Play();
            }
        }

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
        }
        else
        {
            // If there is no camera sequence, or the camera has already been
            // shown, start the door and its stationary movement VFX immediately.
            if (MovingPlatform != null)
            {
                StartDoorMovement();
            }
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
        // Camera movement now only controls when the platform begins moving.
        // It no longer controls whether the puzzle becomes solved.
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
        // The VFX holder is enabled at the same moment the door receives its
        // destination. Because the holder is stationary, only the door moves.
        MP_Target =
            MP_EndGoal;

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