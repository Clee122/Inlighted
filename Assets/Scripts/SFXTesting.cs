using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SFXTesting : MonoBehaviour
{
    private Boolean PlayerWithinRange;
    private Boolean PlayedAlready;
    private InputAction Interacted;
    public AudioSource audioSource;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Interact()
    {
        print("interacted");
        if (PlayerWithinRange && PlayedAlready == false)
        {
            audioSource.Play();
            PlayedAlready = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        print("collision began");
        if (collision.CompareTag("Player"))
        {
            PlayerWithinRange = true;
            PlayedAlready=false;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        print("collision ended");
        if (collision.CompareTag("Player"))
        {
            PlayerWithinRange = false;
        }
    }
}
