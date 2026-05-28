using UnityEngine;
using System.Collections;

public class checkpoint : MonoBehaviour
{
    private PlayerRespawn respawn; // Use the functions store in PlayerRespawn script from the Player
    void Awake()
    {
        respawn = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerRespawn>(); // Find the object being tag Player and get the PlayerRespawn component.
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
       { if (collision.gameObject.tag == "Player") // Checks is the object Player when enter the collider
             respawn.SetCheckpoint(transform); // Active the SetCheckpoint in PlayerRespawn script to save the checkpoint position
       }
    }
}
