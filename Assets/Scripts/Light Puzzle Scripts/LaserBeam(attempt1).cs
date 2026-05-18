using NUnit.Framework;
using UnityEngine;

public class LaserBeam
{
    Vector3 pos, dir;

    GameObject laserObj;
    LineRenderer laser;
    //List<Vector3> laserIndices = new List<Vector3>();

    public LaserBeam(Vector3 pos, Vector3 dir, Material material)
    {
        this.laser = new LineRenderer();
        this.laserObj = new GameObject();
        this.laserObj.name = "Laser Beam";
        this.pos = pos;
        this.dir = dir;

       // this.laser = this.laserObj.AddComponent(typeof(lineRenderer)) as LineRenderer;



    }
}
