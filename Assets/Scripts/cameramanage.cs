using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class cameramanage : MonoBehaviour
{
    public float speed = 10f;

    private Transform target;
    private Transform player;

    public float zoomSpeed = 5f;
    public float targetOrtho;
    public float normalOrtho = 4;
    public float maxOrtho = 17.0f;
    public CinemachineVirtualCamera vcam;

    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        target = player;
        vcam = GetComponent<CinemachineVirtualCamera>();
        
        if (vcam == null)
        {
            enabled = false;
            return;
        }
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
        Vector3 newPos = new Vector3(target.position.x, target.position.y +2, -10);

        transform.position = Vector3.MoveTowards(transform.position, newPos, speed * Time.deltaTime);
    }
        public void ZoomCam()
    {
        vcam.m_Lens.OrthographicSize = Mathf.MoveTowards(vcam.m_Lens.OrthographicSize, targetOrtho, zoomSpeed * Time.deltaTime);
    }

    public void Movetocameraspace(Transform cameraspace)
    {
        target = cameraspace;
        targetOrtho = maxOrtho;
    }

    public void Movecamback()
    {
        target = player;
        targetOrtho = normalOrtho;

        Vector3 TpPos = new Vector3(player.position.x, player.position.y + 2, -10);

        transform.position = TpPos;
        vcam.ForceCameraPosition(TpPos, transform.rotation);
        vcam.PreviousStateIsValid = false;
        vcam.m_Lens.OrthographicSize = normalOrtho;
    }
}
