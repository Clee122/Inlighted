using UnityEngine;

public class BloomReceiver : MonoBehaviour
{
    [Header("Assigned Bloom Platforms")]

    // Each receiver owns only the flower platforms assigned here.
    // This allows separate receivers throughout the level to control different
    // traversal routes without activating every BloomPlatform in the scene.
    [SerializeField] private BloomPlatform[] bloomPlatforms;

    [Header("Receiver Visual Feedback")]

    // The receiver changes colour while at least one of its assigned flower
    // platforms is in its active bloom cycle. This gives the player a direct
    // visual connection between hitting the receiver and opening the flowers.
    [SerializeField] private SpriteRenderer receiverRenderer;

    // The active colour is intentionally brighter than the receiver's normal
    // appearance so activation can be read quickly during timed platforming.
    // It remains editable so the final value can be tuned around future artwork.
    [SerializeField]
    private Color activeColour =
        new Color32(
            255,
            153,
            255,
            255
        );

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // The receiver's starting colour is cached rather than hard-coded because
    // different receiver artwork can later have its own normal appearance.
    private Color inactiveColour = Color.white;

    private bool isShowingActiveColour;

    private void Awake()
    {
        if (receiverRenderer == null)
        {
            // Most current Bloom Receivers keep their SpriteRenderer on the
            // same GameObject, while this fallback also supports child artwork.
            receiverRenderer =
                GetComponent<SpriteRenderer>();

            if (receiverRenderer == null)
            {
                receiverRenderer =
                    GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (receiverRenderer != null)
        {
            // Remembering the actual starting colour means the receiver can
            // return exactly to its original appearance after the bloom ends.
            inactiveColour =
                receiverRenderer.color;

            receiverRenderer.color =
                inactiveColour;
        }
        else
        {
            Debug.LogWarning(
                "BloomReceiver could not find a SpriteRenderer for visual feedback.",
                this
            );
        }
    }

    private void Update()
    {
        /*
         * The receiver should remain visually active for the complete lifetime
         * of its flower platforms rather than merely for the brief Beam hit.
         *
         * Checking the platform state also avoids duplicating the BloomPlatform
         * timer inside this script. If the bloom duration changes later, the
         * receiver will still automatically stay synchronised.
         */
        bool anyAssignedPlatformActive =
            IsAnyAssignedPlatformActive();

        if (
            anyAssignedPlatformActive !=
            isShowingActiveColour
        )
        {
            SetReceiverVisualState(
                anyAssignedPlatformActive
            );
        }
    }

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

        if (activatedAnyPlatform)
        {
            // Change colour immediately when a new bloom is successfully
            // triggered instead of waiting until the following Update frame.
            SetReceiverVisualState(
                true
            );

            if (showDebugLogs)
            {
                Debug.Log(
                    gameObject.name +
                    " activated its assigned Bloom Platforms."
                );
            }
        }
    }

    private bool IsAnyAssignedPlatformActive()
    {
        foreach (BloomPlatform bloomPlatform in bloomPlatforms)
        {
            if (bloomPlatform == null)
            {
                continue;
            }

            /*
             * As long as at least one assigned flower is blooming, open or
             * finishing its bloom cycle, this receiver still represents an
             * active temporary traversal route.
             */
            if (bloomPlatform.IsBloomedOrBlooming())
            {
                return true;
            }
        }

        return false;
    }

    private void SetReceiverVisualState(
        bool isActive
    )
    {
        isShowingActiveColour =
            isActive;

        if (receiverRenderer == null)
        {
            return;
        }

        if (isActive)
        {
            // Preserve the receiver's existing transparency while changing only
            // its RGB feedback so translucent future artwork remains intact.
            Color displayedActiveColour =
                activeColour;

            displayedActiveColour.a =
                inactiveColour.a;

            receiverRenderer.color =
                displayedActiveColour;

            return;
        }

        // Once all assigned flowers have completely finished their bloom cycle,
        // the receiver returns to exactly the colour it had before activation.
        receiverRenderer.color =
            inactiveColour;

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " returned to its inactive colour because its Bloom Platforms finished."
            );
        }
    }

    private void OnDisable()
    {
        // Resetting the visual prevents an active receiver colour remaining
        // behind if this object is disabled while its flowers are still open.
        isShowingActiveColour =
            false;

        if (receiverRenderer != null)
        {
            receiverRenderer.color =
                inactiveColour;
        }
    }
}