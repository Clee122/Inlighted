using UnityEngine;

public class Laser : MonoBehaviour
{
    public LineRenderer Direction;

    public int reflections;
    public float MaxRayDistance;
    public LayerMask Layerdetection;
    public float rotationSpeed;

    private void Start()
    {
        Physics2D.queriesStartInColliders = false;
    }

    private void Update()
    {
        //transform.Rotate(rotationSpeed*Vector3.forward*Time.deltaTime);

        Direction.positionCount = 1;
        Direction.SetPosition(0,transform.position);

        RaycastHit2D hitInfo = Physics2D.Raycast(transform.position, transform.right, MaxRayDistance, Layerdetection);

        bool isMirror = false;
        Vector2 mirrorHitPoint = Vector2.zero;
        Vector2 mirrorHitNormal = Vector2.zero;


        for (int i = 0; i < reflections; i++)
        {
            Direction.positionCount += 1;

            if (hitInfo.collider != null)
            {
                Direction.SetPosition(Direction.positionCount - 1, hitInfo.point);

                isMirror = false;
                if (hitInfo.collider.CompareTag("Mirror"))
                {
                    mirrorHitPoint = (Vector2)hitInfo.point;
                    mirrorHitNormal = (Vector2)hitInfo.normal;
                    hitInfo = Physics2D.Raycast((Vector2)hitInfo.point, Vector2.Reflect(hitInfo.point, hitInfo.normal), MaxRayDistance, Layerdetection);
                    isMirror = true;
                }
                else
                    break;
            }
            else
            {
                if (isMirror)
                {
                    Direction.SetPosition(Direction.positionCount - 1, mirrorHitPoint + Vector2.Reflect(mirrorHitPoint, mirrorHitNormal) * MaxRayDistance);
                    break;
                }
                else
                {
                    Direction.SetPosition(Direction.positionCount - 1, transform.position + transform.right * MaxRayDistance);
                }
            }

        }






    }

}
