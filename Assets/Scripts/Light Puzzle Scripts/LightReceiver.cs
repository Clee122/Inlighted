using UnityEngine;
using static AbilityUnlockObject;

public class LightReceiver : MonoBehaviour
{
    // Public choose what colour input: blue, yellow, any.
    public enum ListColors // Make sure the tags share the same exact name as these.
    {
        BeamBlue,
        BeamYellow,
        BeamAny
    }

    [Header("Color Input")]

    // This is serialised so the required beam colour can be selected in the Inspector.
    [SerializeField] private ListColors ChooseColorInput;

    private string ColorInput;

    // Make visual for colour inputted.

    [Header("Activates")]
    public GameObject activatee;
    private Door ActivateScript;

    // Choose object for activation.
    public enum ListActivations // Add any more interactions for the light receiver.
    {
        Door,
        Platform
    }

    public ListActivations ActivateObject;

    // Store the configured colour and find the script on the object this
    // receiver is expected to activate before any beam interaction occurs.
    void Start()
    {
        // Store the selected enum value in the class variable so it remains
        // available when a collider later enters the receiver trigger.
        ColorInput = ChooseColorInput.ToString();

        if (activatee != null)
        {
            ActivateScript = activatee.GetComponent<Door>();

            if (ActivateScript == null)
            {
                Debug.LogError(
                    "The assigned activation object does not contain a Door component.",
                    this
                );
            }
        }
        else
        {
            Debug.LogError(
                "LightReceiver requires an activation object to be assigned.",
                this
            );
        }
    }

    // The receiver now responds to the beam collider entering its trigger instead
    // of searching for the visual beam with a CircleCast every frame.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(ColorInput))
        {
            Debug.Log("specific color hit");

            // Only attempt activation when a valid Door component was found,
            // preventing a missing reference from stopping the interaction.
            if (ActivateScript != null)
            {
                ActivateScript.Activate();
            }
        }
        else if (ColorInput == "BeamAny")
        {
            if (collision.CompareTag("BeamBlue") ||
                collision.CompareTag("BeamYellow"))
            {
                Debug.Log("any color hit");

                // BeamAny accepts any supported beam colour while still requiring
                // a valid activation target before calling its activation method.
                if (ActivateScript != null)
                {
                    ActivateScript.Activate();
                }
            }
        }
    }
}