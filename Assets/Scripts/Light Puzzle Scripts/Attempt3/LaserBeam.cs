using System.Collections.Generic;
using UnityEngine;

public class LaserBeam
{
    Vector3 pos, dir;

    GameObject laserObj;
    public LineRenderer laser;
    public List<Vector3> laserIndices = new List<Vector3>();

    // LaserBeam is a normal C# class rather than a MonoBehaviour, so receiver
    // references and renderer settings must be supplied when it is created.
    private AppearingPlatformReceiver APReceiver;
    private MovingPlatformReceiver MPReceiver;

    public LaserBeam(
        Vector3 pos,
        Vector3 dir,
        Material material,
        AppearingPlatformReceiver receiver,
        MovingPlatformReceiver Mreceiver,
        int sortingOrder)
    {
        this.APReceiver = receiver;
        this.MPReceiver = Mreceiver;

        this.laserObj = new GameObject();
        this.laserObj.name = "Laser Beam";
        this.pos = pos;
        this.dir = dir;

        this.laser =
            this.laserObj.AddComponent(typeof(LineRenderer)) as LineRenderer;

        this.laser.startWidth = 0.2f;
        this.laser.endWidth = 0.2f;
        this.laser.material = material;
        this.laser.startColor = Color.cyan;
        this.laser.endColor = Color.cyan;

        // The LineRenderer is created as a separate runtime object and therefore
        // does not inherit the LaserPointer's Order in Layer. The value supplied
        // by ShootLaser keeps the beam visible over environment artwork.
        this.laser.sortingOrder = sortingOrder;

        CastRay(pos, dir, laser);
    }

    public void CastRay(Vector3 pos, Vector3 dir, LineRenderer laser)
    {
        laserIndices.Add(pos);

        Ray ray = new Ray(pos, dir);
        RaycastHit hit;

        // The mask value 1 preserves Jayden's original behaviour of checking
        // objects on the Default layer.
        if (Physics.Raycast(ray, out hit, 30, 1))
        {
            CheckHit(hit, dir, laser);
        }
        else
        {
            laserIndices.Add(ray.GetPoint(30));
            UpdateLaser();

            if (APReceiver != null)
            {
                APReceiver.DeActivate();
            }

            if (MPReceiver != null)
            {
                MPReceiver.DeActivate();
            }
        }
    }

    void UpdateLaser()
    {
        int count = 0;
        laser.positionCount = laserIndices.Count;

        foreach (Vector3 idx in laserIndices)
        {
            laser.SetPosition(count, idx);
            count++;
        }
    }

    void CheckHit(
        RaycastHit hitInfo,
        Vector3 direction,
        LineRenderer laser)
    {
        if (hitInfo.collider.CompareTag("Mirror"))
        {
            Vector3 pos = hitInfo.point;
            Vector3 dir = Vector3.Reflect(direction, hitInfo.normal);

            CastRay(pos, dir, laser);
        }
        else if (
            hitInfo.collider.CompareTag(
                "AppearingPlatformLightReceiver"))
        {
            laserIndices.Add(hitInfo.point);
            UpdateLaser();

            if (MPReceiver != null)
            {
                MPReceiver.DeActivate();
            }

            if (APReceiver != null)
            {
                APReceiver.Activate();
            }
            else
            {
                Debug.LogError(
                    "LaserBeam does not have an AppearingPlatformReceiver reference."
                );
            }
        }
        else if (
            hitInfo.collider.CompareTag(
                "MovingPlatformLightReceiver"))
        {
            laserIndices.Add(hitInfo.point);
            UpdateLaser();

            if (APReceiver != null)
            {
                APReceiver.DeActivate();
            }

            if (MPReceiver != null)
            {
                MPReceiver.Activate();
            }
            else
            {
                Debug.LogError(
                    "LaserBeam does not have a MovingPlatformReceiver reference."
                );
            }
        }
        else
        {
            laserIndices.Add(hitInfo.point);
            UpdateLaser();

            if (APReceiver != null)
            {
                APReceiver.DeActivate();
            }

            if (MPReceiver != null)
            {
                MPReceiver.DeActivate();
            }

            if (
                APReceiver == null &&
                MPReceiver == null
            )
            {
                Debug.LogError(
                    "LaserBeam does not have an AppearingPlatformReceiver or MovingPlatformReceiver reference."
                );
            }
        }
    }
}