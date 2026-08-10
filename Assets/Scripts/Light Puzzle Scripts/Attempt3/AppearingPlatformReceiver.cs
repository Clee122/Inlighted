using UnityEngine;

public class AppearingPlatformReceiver : MonoBehaviour
{

    public GameObject AppearingPlatform;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
