using UnityEngine;
using UnityEngine.InputSystem;

public class platform_llever : MonoBehaviour
{
    public MovingPlatform platform;
    private bool playerInRange = false;
    private bool activated = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
         if (playerInRange && Keyboard.current.shiftKey.wasPressedThisFrame && !activated)
        {
            activated = true;
            platform.MoveDown();

        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}
