using UnityEngine;

public class BloomReceiver : MonoBehaviour
{
    [Header("Assigned Bloom Platforms")]

    // Each receiver owns only the flower platforms assigned here.
    // This allows separate receivers throughout the level to control different
    // traversal routes without activating every BloomPlatform in the scene.
    [SerializeField] private BloomPlatform[] bloomPlatforms;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public void ActivateReceiver()
    {
        bool activatedAnyPlatform = false;

        foreach (BloomPlatform bloomPlatform in bloomPlatforms)
        {
            // Empty Inspector entries are skipped so one missing platform
            // reference does not prevent the remaining assigned flowers working.
            if (bloomPlatform == null)
            {
                continue;
            }

            // Already blooming/open flowers deliberately ignore further Beam
            // hits. Their existing bloom timer continues instead of restarting.
            if (bloomPlatform.IsBloomedOrBlooming())
            {
                continue;
            }

            bloomPlatform.ActivateBloom();

            activatedAnyPlatform = true;
        }

        if (
            activatedAnyPlatform &&
            showDebugLogs
        )
        {
            Debug.Log(
                gameObject.name +
                " activated its assigned Bloom Platforms."
            );
        }
    }
}