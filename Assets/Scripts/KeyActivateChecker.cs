using UnityEngine;
using UnityEngine.InputSystem;

public class KeyActivateChecker : MonoBehaviour
{

    private InputAction playerInteract;

    public float radius;
    public Vector2 direction;
    public float distance;

    private Vector3 RotateTarget;

    private float timeCount;

    public float angle;
    public float angleIncrement;

    public GameObject Player;



    //needed: origin, radius, 



    private void Awake()
    {
        playerInteract = Player.GetComponent<PlayerInput>().actions["Interact1"];

        if (playerInteract != null )
        {
            Debug.Log("player interact not null");
        }
    }



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, direction, distance);

        if (hit.collider  != null )
        {
            if (hit.collider.CompareTag("Player") /*&& playerInteract.WasPressedThisFrame()*/)
            {
                Debug.Log("if 1 success");
                if (playerInteract.WasPressedThisFrame())
                {
                    angle = +90;
                    transform.Rotate(0, 0, angle);
                }
            }
        }
        
        {
            //angle =+ 90;
        }
        //transform.Rotate(0,0,angle);

    }

    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {
        
    }


}
