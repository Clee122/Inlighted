using UnityEngine;
using UnityEngine.InputSystem;

public class platform_llever : MonoBehaviour
{
    public MovingPlatform platform; // Select the platform object being want to be move
    private bool playerInRange = false; // Set the playerInRange in false
    private bool activate = false; // Set the activate in false


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
         if (playerInRange && Keyboard.current.shiftKey.wasPressedThisFrame && !activate) // When player are in range pressed the shift Key then the lever active
        {
            activate = true; // Activate became true  
            platform.MoveDown(); // Active the MoveDown() function in moving_platform script

        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // When the object being tag as Player enter the range
        {
            playerInRange = true; // Change the playerInRange into true
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // When the Player exit the range
        {
            playerInRange = false; // Set the playerInRange back into false
        }
    }
}
