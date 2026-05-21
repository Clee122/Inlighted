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
    [SerializeField] private ListColors ChooseColorInput;
    private string ColorInput;
    //make visual for color inputted


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string ColorInput = ChooseColorInput.ToString();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(ColorInput))
        {
            //activate public gameobject script section
        }
    }
}
