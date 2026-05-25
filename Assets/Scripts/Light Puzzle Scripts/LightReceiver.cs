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
    public ListColors ChooseColorInput;
    private string ColorInput;
    //make visual for color inputted

    [Header("Activates")]
    public GameObject activatee;
    private Door ActivateScript;
    //choose object for activation
    public enum ListActivations //add any more interactions for the light receiver
    {
        Door,
        Platform
    }

    public float Radius = 3f;
    public ListActivations ActivateObject;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ColorInput = ChooseColorInput.ToString();
        Debug.Log(ColorInput);

        if (activatee != null)
        {
            ActivateScript = activatee.GetComponent<Door>();
        }

        if (activatee == null) { Debug.LogError("Cannot find activatee object!"); }
        if (ActivateScript == null) { Debug.LogError("Cannot find activatee script!");}
    }

    void Update()
    {

        RaycastHit2D hit = Physics2D.CircleCast(transform.position, Radius, Vector2.left);
        

        if (hit.collider.CompareTag("BeamBlue"))
        {
            Debug.Log("specific color hit");
            //activate public gameobject script section
            ActivateScript.Activate();
        }
        else if (ColorInput == "BeamAny")
        {
            if (hit.collider.CompareTag("BeamBlue") || hit.collider.CompareTag("BeamYellow") /* add any more beam colours */ )
            {
                Debug.Log("any color hit");
                //activate public gameobject script section
                ActivateScript.Activate();

            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Radius);
    }


    /*
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("triggered");

        if (collision.CompareTag(ColorInput))
        {
            Debug.Log("specific color hit");
            //activate public gameobject script section
            ActivateScript.Activate();
        }
        else if (ColorInput == "BeamAny")
        {
            if  (collision.CompareTag("BeamBlue") || collision.CompareTag("BeamYellow")  add any more beam colours )
            {
                Debug.Log("any color hit");
                //activate public gameobject script section
                ActivateScript.Activate();
            }
        }
    
    }
    */
}
