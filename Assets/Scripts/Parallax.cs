using UnityEngine;

public class Parallax : MonoBehaviour
{

    public float length;
    public float startpos;
    public GameObject cam;

    //closer objects have lower numbers
    public float parallaxEffect;

    //still to do: endpoint where they hide/disappear, startpoint where they appear


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float temp = (cam.transform.position.x * (1 - parallaxEffect));

        float dist = (cam.transform.position.x * parallaxEffect);

        transform.position = new Vector3(startpos + dist, transform.position.y, transform.position.z);

        if (temp > startpos + length)
        {
            startpos += length;
        }
        else if (temp < startpos - length)
        {
            startpos -= length;
        }
    }
}
