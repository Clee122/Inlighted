using UnityEngine;
using UnityEngine.InputSystem;

public class ToolTipAppear : MonoBehaviour
{

   
    public GameObject ChosenTip;
    public Vector3 location;
    private GameObject ToolTip;



    private void OnTriggerEnter2D(Collider2D other)
    {
        SpawnToolTip();
        Debug.Log("Collided");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        KillToolTip();

    }

    void SpawnToolTip()
    {
        ToolTip = Instantiate(ChosenTip,location,transform.rotation);

    }

    void KillToolTip()
    {
        Destroy(ToolTip);
    }

}

