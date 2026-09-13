using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class cameramanage : MonoBehaviour
{
    public float speed = 10f;

    private Transform target;
    private Transform player;
    private Transform camerapplace;

    public float zoomSpeed = 5f;
    public float targetOrtho;
    public float normalOrtho = 4f;
    public float maxOrtho = 17f;

    public CinemachineVirtualCamera vcam;

    private bool movingToCameraSpace = false;
    private bool movingBack = false;

    // The intro keeps the camera anchored to a designed opening composition
    // while CatMoth falls into view instead of following the player immediately.
    private bool holdingIntroPosition = false;

    // The intro return uses its own movement state so the camera can smoothly
    // transition from the fixed opening shot back to CatMoth after landing.
    private bool blendingBackFromIntro = false;

    [Header("Intro Camera Transition")]

    // This controls how quickly the camera catches up to CatMoth after the
    // introductory landing. A higher value makes the transition shorter while
    // still avoiding the visible one-frame snap of an instant follow switch.
    [SerializeField] private float introFollowTransitionSpeed = 15f;

    void Awake()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogError(
                "cameramanage could not find an object tagged Player.",
                this
            );

            enabled = false;
            return;
        }

        player =
            playerObject.transform;

        vcam =
            GetComponent<CinemachineVirtualCamera>();

        if (vcam == null)
        {
            Debug.LogError(
                "cameramanage requires a CinemachineVirtualCamera on the same GameObject.",
                this
            );

            enabled = false;
            return;
        }

        target =
            player;

        // The virtual camera follows an intermediary point rather than CatMoth
        // directly so puzzle transitions and the intro can temporarily control
        // camera framing without replacing the existing Cinemachine setup.
        GameObject followObject =
            new GameObject(
                "CameraFollowPoint"
            );

        camerapplace =
            followObject.transform;

        camerapplace.position =
            player.position;

        vcam.Follow =
            camerapplace;

        normalOrtho =
            vcam.m_Lens.OrthographicSize;

        targetOrtho =
            normalOrtho;

        introFollowTransitionSpeed =
            Mathf.Max(
                0.01f,
                introFollowTransitionSpeed
            );
    }

    void LateUpdate()
    {
        MoveCam();
        ZoomCam();
    }

    public void MoveCam()
    {
        if (
            target == null ||
            player == null ||
            camerapplace == null
        )
        {
            return;
        }

        // During the falling portion of the intro the camera remains fixed so
        // CatMoth visibly enters the established opening composition.
        if (holdingIntroPosition)
        {
            camerapplace.position =
                target.position;

            return;
        }

        // Once CatMoth lands, the follow point travels towards the player rather
        // than teleporting there. This hides the hard visual cut between the
        // fixed opening shot and normal gameplay camera following.
        if (blendingBackFromIntro)
        {
            camerapplace.position =
                Vector3.MoveTowards(
                    camerapplace.position,
                    player.position,
                    introFollowTransitionSpeed *
                    Time.deltaTime
                );

            if (
                Vector3.Distance(
                    camerapplace.position,
                    player.position
                ) < 0.05f
            )
            {
                camerapplace.position =
                    player.position;

                blendingBackFromIntro =
                    false;
            }

            return;
        }

        if (
            !movingToCameraSpace &&
            !movingBack
        )
        {
            camerapplace.position =
                player.position;

            return;
        }

        if (movingToCameraSpace)
        {
            camerapplace.position =
                Vector3.MoveTowards(
                    camerapplace.position,
                    target.position,
                    speed * Time.deltaTime
                );

            return;
        }

        if (movingBack)
        {
            camerapplace.position =
                Vector3.MoveTowards(
                    camerapplace.position,
                    player.position,
                    speed * Time.deltaTime
                );

            if (
                Vector3.Distance(
                    camerapplace.position,
                    player.position
                ) < 0.1f
            )
            {
                camerapplace.position =
                    player.position;

                movingBack =
                    false;
            }
        }
    }

    public void ZoomCam()
    {
        if (vcam == null)
        {
            return;
        }

        vcam.m_Lens.OrthographicSize =
            Mathf.MoveTowards(
                vcam.m_Lens.OrthographicSize,
                targetOrtho,
                zoomSpeed *
                Time.deltaTime
            );
    }

    public void Movetocameraspace(
        Transform cameraspace
    )
    {
        if (cameraspace == null)
        {
            return;
        }

        target =
            cameraspace;

        holdingIntroPosition =
            false;

        blendingBackFromIntro =
            false;

        movingToCameraSpace =
            true;

        movingBack =
            false;
    }

    public void ZoomOut(
        float zoomSize
    )
    {
        targetOrtho =
            zoomSize;
    }

    public void Movecamback()
    {
        target =
            player;

        holdingIntroPosition =
            false;

        blendingBackFromIntro =
            false;

        movingToCameraSpace =
            false;

        movingBack =
            true;

        targetOrtho =
            normalOrtho;
    }

    public void Movecambackdie()
    {
        target =
            player;

        holdingIntroPosition =
            false;

        blendingBackFromIntro =
            false;

        movingToCameraSpace =
            false;

        movingBack =
            false;

        camerapplace.position =
            player.position;

        targetOrtho =
            normalOrtho;

        vcam.m_Lens.OrthographicSize =
            normalOrtho;

        vcam.PreviousStateIsValid =
            false;
    }

    public void HoldIntroCamera(
        Transform introCameraPosition
    )
    {
        if (
            introCameraPosition == null ||
            camerapplace == null ||
            vcam == null
        )
        {
            Debug.LogWarning(
                "The intro camera could not start because its camera position is missing.",
                this
            );

            return;
        }

        // The intro camera begins already framed at the chosen position so the
        // opening does not spend time travelling before CatMoth starts falling.
        target =
            introCameraPosition;

        holdingIntroPosition =
            true;

        blendingBackFromIntro =
            false;

        movingToCameraSpace =
            false;

        movingBack =
            false;

        camerapplace.position =
            introCameraPosition.position;

        vcam.PreviousStateIsValid =
            false;
    }

    public void ResumePlayerFollowAfterIntro()
    {
        if (
            player == null ||
            camerapplace == null
        )
        {
            return;
        }

        // The camera stops being locked to the intro point after CatMoth lands,
        // but the follow point is not teleported directly to the player. Instead
        // it blends across the short remaining distance to make the handoff smooth.
        target =
            player;

        holdingIntroPosition =
            false;

        blendingBackFromIntro =
            true;

        movingToCameraSpace =
            false;

        movingBack =
            false;

        targetOrtho =
            normalOrtho;
    }
}