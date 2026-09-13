using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class cameramanage : MonoBehaviour
{
    public float speed = 10f;
    private Transform target;
    private Transform player;
    private Transform cameraplace;

    public float zoomSpeed = 5f;
    public float targetOrtho;
    public float normalOrtho = 4f;
    public float maxOrtho;
    public CinemachineVirtualCamera vcam;
   
    [Header("Camera Sequence")]
    public float cameraTime = 5f;
    private bool movingtoCameraspace = false;
    private bool movingBack = false;
    private bool pressC = false;
    private bool camerasequenceon = false;
    private Transform activeCameraSpace;
    private Coroutine cameraRoutine;
    private float Zoomroomsize;

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

        cameraplace = followObject.transform;
        cameraplace.position = player.position;
        vcam.Follow = cameraplace;
        normalOrtho = vcam.m_Lens.OrthographicSize;
        targetOrtho = normalOrtho;
    }


    void Update()
    {
        if (
            pressC && !camerasequenceon && activeCameraSpace != null && Keyboard.current.cKey.wasPressedThisFrame
        )
        {
            cameraRoutine = StartCoroutine(CameraSequence());
        }
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

        if (!movingtoCameraspace && !movingBack)
        {
            cameraplace.position = player.position;
            return;
        }

        if (movingtoCameraspace)
        {
            cameraplace.position = Vector3.MoveTowards(cameraplace.position, target.position, speed * Time.deltaTime);
            return;
        }

        if (movingBack)
        {
            cameraplace.position = Vector3.MoveTowards(cameraplace.position, player.position, speed * Time.deltaTime);

            if (Vector3.Distance(cameraplace.position, player.position) < 0.1f)
            {
                cameraplace.position = player.position;

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

        movingtoCameraspace = true;
        movingBack = false;
    }

    public void SetCZoomout(Transform cameraSpace, float zoomSize)
    {
        pressC = true;
        Zoomroomsize = zoomSize;
        activeCameraSpace = cameraSpace;
    }


    public void DisableCZoomout()
    {
        pressC = false;

        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
            cameraRoutine = null;
        }

        camerasequenceon = false;
        activeCameraSpace = null;

        Movecamback();
    }


    public void ZoomOut(float zoomSize)
    {
        targetOrtho = zoomSize;
    }


    public void Movecamback()
    {
        target = player;

        movingtoCameraspace = false;
        movingBack = true;

        targetOrtho = normalOrtho;
    }


    private IEnumerator CameraSequence()
    {
        camerasequenceon = true;

        Movetocameraspace(activeCameraSpace);

        targetOrtho = Zoomroomsize;

        yield return new WaitUntil(() =>
        Vector3.Distance(cameraplace.position, activeCameraSpace.position) < 0.1f && Mathf.Abs(vcam.m_Lens.OrthographicSize - maxOrtho) < 0.05f);

        yield return new WaitForSeconds(cameraTime);
        Movecamback();

        yield return new WaitUntil(() => !movingBack);

        camerasequenceon = false;
        cameraRoutine = null;
    }
}