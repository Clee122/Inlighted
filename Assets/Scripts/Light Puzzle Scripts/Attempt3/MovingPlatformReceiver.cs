using UnityEngine;

public class MovingPlatformReceiver : MonoBehaviour
{
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
        }
    }

    // Update is called once per frame.
    void Update()
    {
        //vector 3 needs to live here
        var step = Speed * Time.deltaTime;
        MovingPlatform.transform.position = Vector3.MoveTowards(MovingPlatform.transform.position, MP_Target, step);
    }

    public void Activate()
    {
        // Only show the platform when its Inspector reference is valid.
        if (MovingPlatform != null)
        {
            MP_Target = MP_EndGoal;
        }
    }

    public void DeActivate()
    {
        // Only hide the platform when its Inspector reference is valid.
        if (MovingPlatform != null)
        {
            MP_Target = MP_Origin;
        }
    }
}