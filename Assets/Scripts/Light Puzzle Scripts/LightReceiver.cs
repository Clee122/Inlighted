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
    private ListColors ChooseColorInput;
    private string ColorInput;
    //make visual for color inputted

    [Header("Activates")]
    public GameObject activatee;
    private Door ActivateScript;
    //choose object for activation


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string ColorInput = ChooseColorInput.ToString();

        if (activatee != null)
        {
            ActivateScript = activatee.GetComponent<Door>();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(ColorInput))
        {
            //activate public gameobject script section
            ActivateScript.Activate();
        }
        else if (ColorInput == "BeamAny")
        {
            if  (collision.CompareTag("BeamBlue") || collision.CompareTag("BeamYellow"))
            {
                //activate public gameobject script section
                ActivateScript.Activate();
            }
        }
    }
}
