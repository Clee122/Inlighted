using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootLaser : MonoBehaviour
{
    public Material material;
    LaserBeam beam;

    public AppearingPlatformReceiver APReceiver;
    public MovingPlatformReceiver MPReceiver;

    /*
    // Update is called once per frame
    void Update()
    {
        Destroy(GameObject.Find("Laser Beam"));
        beam = new LaserBeam(
            gameObject.transform.position,
            gameObject.transform.right,
            material,
            APReceiver
        );
    }
    */

    private void Start()
    {
        // Pass the assigned receiver into LaserBeam because LaserBeam is not a
        // MonoBehaviour and cannot obtain the reference through Awake.
        beam = new LaserBeam(
            transform.position,
            transform.right,
            material,
            APReceiver,
            MPReceiver
        );

        //beam.tag = "BeamBlue";

        // Begin with the platform hidden until the beam reaches the receiver.
        if (APReceiver != null)
        {
            APReceiver.DeActivate();
        }
        else
        {
            Debug.LogError(
                "ShootLaser requires an AppearingPlatformReceiver to be assigned.",
                this
            );
        }

        // Begin with the platform at original location until the beam reaches the receiver.
        if (MPReceiver != null)
        {
            MPReceiver.DeActivate();
        }
        else
        {
            Debug.LogError(
                "ShootLaser requires a MovingPlatformReceiver to be assigned.",
                this
            );
        }

    }

    private void Update()
    {
        // Avoid another null-reference error if the beam could not be created.
        if (beam == null || beam.laser == null)
        {
            return;
        }

        beam.laser.positionCount = 0;
        beam.laserIndices.Clear();
        beam.CastRay(
            transform.position,
            transform.right,
            beam.laser
        );
    }
}