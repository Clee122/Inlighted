using UnityEngine;

public class cameratrigger : MonoBehaviour
{
    public cameramanage Cameramanager;
    public Transform cameraspace;
    public float maxOrtho;
     private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Something entered trigger: " + collision.name);

        if (collision.CompareTag("Player"))
        {
            Debug.Log("Player trigger");

            Cameramanager.Movetocameraspace(cameraspace);
            Cameramanager.ZoomOut(maxOrtho);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Debug.Log("Something exited trigger: " + collision.name);

        if (collision.CompareTag("Player"))
        {
            Debug.Log("Player lift trigger");

            Cameramanager.Movecamback();
        }
    }
}
