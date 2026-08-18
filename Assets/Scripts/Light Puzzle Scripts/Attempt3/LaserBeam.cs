using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeam
{
    Vector3 pos, dir;

    GameObject laserObj;
    public LineRenderer laser;
    public List<Vector3> laserIndices = new List<Vector3>();

    // LaserBeam is a normal C# class rather than a MonoBehaviour, so the
    // receiver reference must be provided when the beam is created.
    private AppearingPlatformReceiver APReceiver;
    private MovingPlatformReceiver MPReceiver;

    public LaserBeam(
        Vector3 pos,
        Vector3 dir,
        Material material,
        AppearingPlatformReceiver receiver,
        MovingPlatformReceiver Mreceiver)
    {
        // Store the receiver so the laser can tell it when the beam is or is
        // not hitting the correct light receiver.
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

        CastRay(pos, dir, laser);
    }

    public void CastRay(Vector3 pos, Vector3 dir, LineRenderer laser)
    {
        laserIndices.Add(pos);

        Ray ray = new Ray(pos, dir);
        RaycastHit hit;

        // The layer mask value 1 means the ray currently checks objects on the
        // Default layer only, preserving the original raycast behaviour.
        if (Physics.Raycast(ray, out hit, 30, 1))
        {
            CheckHit(hit, dir, laser);
        }
        else
        {
            laserIndices.Add(ray.GetPoint(30));
            UpdateLaser();

            // The platform should disappear when the beam is not hitting
            // anything within its maximum distance.
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
            MPReceiver.DeActivate();

            // The receiver is responsible for showing the platform when it is
            // reached by the reflected laser.
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
            APReceiver.DeActivate();

            // The receiver is responsible for moving the platform when it is
            // reached by the reflected laser.
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

            // Any non-mirror object other than the intended receiver stops the
            // beam and hides/moves the platform.
            if (APReceiver != null)
            {
                APReceiver.DeActivate();
            }
            else if (MPReceiver !=null)
            {
                MPReceiver.DeActivate();
            }
            else
            {
                Debug.LogError(
                    "LaserBeam does not have an AppearingPlatformReceiver or MovingPlatformReceiver reference."
                );
            }
        }
    }
}