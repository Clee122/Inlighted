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

    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        vcam = GetComponent<CinemachineVirtualCamera>();

        if (vcam == null)
        {
            enabled = false;
            return;
        }

        target = player;
        GameObject followObject = new GameObject("CameraFollowPoint");
        camerapplace = followObject.transform;
        camerapplace.position = player.position;
        vcam.Follow = camerapplace;
        normalOrtho = vcam.m_Lens.OrthographicSize;
        targetOrtho = normalOrtho;
    }

    void LateUpdate()
    {
        MoveCam();
        ZoomCam();
    }

    public void MoveCam()
{
    if (target == null || player == null)
        return;

    if (!movingToCameraSpace && !movingBack)
    {
        camerapplace.position = player.position;
        return;
    }

    if (movingToCameraSpace)
    {
        camerapplace.position = Vector3.MoveTowards(camerapplace.position, target.position, speed * Time.deltaTime);
        return;
    }

    if (movingBack)
    {
        camerapplace.position = Vector3.MoveTowards(camerapplace.position, player.position, speed * Time.deltaTime);
         if (Vector3.Distance(camerapplace.position, player.position) < 0.1f)
        {
            camerapplace.position = player.position;
            movingBack = false;
        }
    }
}

    public void ZoomCam()
    {
        vcam.m_Lens.OrthographicSize = Mathf.MoveTowards(vcam.m_Lens.OrthographicSize, targetOrtho, zoomSpeed * Time.deltaTime);
    }

    public void Movetocameraspace(Transform cameraspace)
    {
    target = cameraspace;

    movingToCameraSpace = true;
    movingBack = false;
    }

    public void ZoomOut(float zoomSize)
    {
        targetOrtho = zoomSize;
    }

    public void Movecamback()
    {
        target = player;
        movingToCameraSpace = false;
        movingBack = true;
        targetOrtho = normalOrtho;
    }

    public void Movecambackdie()
    {
        target = player;
        movingToCameraSpace = false;
        movingBack = false;
        camerapplace.position = player.position;
        targetOrtho = normalOrtho;
        vcam.m_Lens.OrthographicSize = normalOrtho;
        vcam.PreviousStateIsValid = false;
    }
}
