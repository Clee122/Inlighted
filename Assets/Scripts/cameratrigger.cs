using UnityEngine;

public class cameratrigger : MonoBehaviour
{
    public cameramanage Cameramanager;
    public Transform cameraspace;

     private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Something entered trigger: " + collision.name);

        if (collision.CompareTag("Player"))
        {
            Debug.Log("PLAYER TRIGGERED CAMERA");

            Cameramanager.Movetocameraspace(cameraspace);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Debug.Log("Something exited trigger: " + collision.name);

        if (collision.CompareTag("Player"))
        {
            Debug.Log("PLAYER LEFT CAMERA TRIGGER");

            Cameramanager.Movecamback();
        }
    }
}
