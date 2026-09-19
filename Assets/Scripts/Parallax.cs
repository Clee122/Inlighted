using UnityEngine;

public class Parallax : MonoBehaviour
{

    public float length;
    public float startpos;
    public GameObject cam;
    public float lockedYPosition;

    //closer objects have lower numbers
    public float parallaxEffect;

    public GameObject hitboxDisableParallax;
    public GameObject hitboxEnableParallax;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float temp = (cam.transform.position.x * (1 - parallaxEffect));

        float dist = (cam.transform.position.x * parallaxEffect);

        transform.position = new Vector3(startpos + dist, lockedYPosition, transform.position.z);

        if (temp > startpos + length)
        {
            startpos += length;
        }
        else if (temp < startpos - length)
        {
            startpos -= length;
        }

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        print("collided with something");

        if (collision == hitboxDisableParallax)
        {
            print("set disabled");
            gameObject.SetActive(false);
        }

        if (collision == hitboxEnableParallax)
        {
            print("set enabled");
            gameObject.SetActive(true);
        }
    }

}
