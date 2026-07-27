using UnityEngine;

public class AppearingPlatformReceiver : MonoBehaviour
{
    public GameObject AppearingPlatform;

    // Start is called once before the first execution of Update after the
    // MonoBehaviour is created.
    void Start()
    {
        // Report a clear setup error rather than allowing an unclear
        // null-reference error when the laser reaches the receiver.
        if (AppearingPlatform == null)
        {
            Debug.LogError(
                "AppearingPlatformReceiver requires an AppearingPlatform to be assigned.",
                this
            );
        }
    }

    // Update is called once per frame.
    void Update()
    {

    }

    public void Activate()
    {
        // Only show the platform when its Inspector reference is valid.
        if (AppearingPlatform != null)
        {
            AppearingPlatform.SetActive(true);
        }
    }

    public void DeActivate()
    {
        // Only hide the platform when its Inspector reference is valid.
        if (AppearingPlatform != null)
        {
            AppearingPlatform.SetActive(false);
        }
    }
}