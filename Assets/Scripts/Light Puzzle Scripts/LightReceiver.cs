using UnityEngine;
using static AbilityUnlockObject;

public class LightReceiver : MonoBehaviour
{
    //public choose what color input: blue, yellow, any

    public enum ListColors //make sure the tags share the same exact name as these
    {
        BeamBlue,
        BeamYellow,
        BeamAny
    }

    [Header("Color Input")]

    // This is serialised so the required beam colour can be selected in the Inspector.
    [SerializeField] private ListColors ChooseColorInput;

    private string ColorInput;
    //make visual for color inputted

    [Header("Activates")]
    public GameObject activatee;
    private Door ActivateScript;
    //choose object for activation

    // Start is called once before the first execution of Update after the MonoBehaviour is created
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(ColorInput))
        {
            //activate public gameobject script section
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
                //activate public gameobject script section
                if (ActivateScript != null)
                {
                    ActivateScript.Activate();
                }
            }
        }
    }
}