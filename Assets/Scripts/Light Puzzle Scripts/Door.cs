using UnityEngine;

public class Door : MonoBehaviour
{
   

    //public direction for opening
    public enum OpenDirection
    {
        up,
        down,
        left,
        right
    }
    public OpenDirection Direction;

    public float OpenDistance;
    //distance travelled when open


    //public requirements for opening, do a list so multiple can be used

    private Vector3 StartPos;
    private Vector3 EndPos;
    public Vector3 TargetPos;
    public float speed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartPos = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        EndPos = StartPos;

    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, EndPos, speed * Time.deltaTime);
    }

    public void Activate()
    {
        //activate in direction for distance specified
        EndPos = TargetPos;
        Debug.Log("activate void reached");
    }


}
