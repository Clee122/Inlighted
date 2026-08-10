using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class ShootLaser : MonoBehaviour
{
    public Material material;
    LaserBeam beam;

    public AppearingPlatformReceiver APReceiver;
    public GameObject AppearingPlatform;

    /*
    // Update is called once per frame
    void Update()
    {
        Destroy(GameObject.Find("Laser Beam"));
        beam = new LaserBeam(gameObject.transform.position, gameObject.transform.right, material);
    }
    */

    private void Start()
    {
        beam = new LaserBeam(transform.position, transform.right, material);
        //beam.tag = "BeamBlue";
        AppearingPlatform.SetActive(false);
    }

    private void Update()
    {
        beam.laser.positionCount = 0;
        beam.laserIndices.Clear();
        beam.CastRay(transform.position, transform.right, beam.laser);
    }

    
    public void Activate()
    {
        AppearingPlatform.SetActive(true);
    }

    public void DeActivate()
    {
        AppearingPlatform.SetActive(false);
    }
    

}
