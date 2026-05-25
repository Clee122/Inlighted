using UnityEngine;

public class Laser : MonoBehaviour
{
    public LineRenderer Beam;

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

        Beam.positionCount = 1;
        Beam.SetPosition(0,transform.position);

        RaycastHit2D hitInfo = Physics2D.Raycast(transform.position, transform.right, MaxRayDistance, Layerdetection);

        Ray2D ray = new Ray2D(transform.position, transform.right);

        bool isMirror = false;
        Vector2 mirrorHitPoint = Vector2.zero;
        Vector2 mirrorHitNormal = Vector2.zero;


        for (int i = 0; i < reflections; i++)
        {
            Beam.positionCount += 1;

            if (hitInfo.collider != null)
            {
                Beam.SetPosition(Beam.positionCount - 1, hitInfo.point - ray.direction * -0.1f);

                isMirror = false;
                if (hitInfo.collider.CompareTag("Mirror"))
                {
                    mirrorHitPoint = (Vector2)hitInfo.point;
                    mirrorHitNormal = (Vector2)hitInfo.normal;
                    hitInfo = Physics2D.Raycast((Vector2)hitInfo.point - ray.direction * -0.1f, Vector2.Reflect(hitInfo.point - ray.direction * -0.1f, hitInfo.normal), MaxRayDistance, Layerdetection);
                    //Debug.Log(hitInfo.normal);
                    isMirror = true; 
                }
                else
                    break;
            }
            else
            {
                if (isMirror)
                {
                    Beam.SetPosition(Beam.positionCount - 1, mirrorHitPoint + Vector2.Reflect(mirrorHitPoint, mirrorHitNormal) * MaxRayDistance);
                    break;
                }
                else
                {
                    Beam.SetPosition(Beam.positionCount - 1, transform.position + transform.right * MaxRayDistance);
                }
            }

        }






    }

}
