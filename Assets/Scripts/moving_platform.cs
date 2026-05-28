using UnityEngine;
using System.Collections;

public class MovingPlatform : MonoBehaviour
{
    public float moveDistance; // Set the moving distance
    public float moveSpeed = 2f; // Set the moving speed for the platform as 2

    public Vector3 startPosition; // Set the start position for the object
    public Vector3 targetPosition; //Set the position where it stop for the object

    private bool startMove = false; // Set the startMove in false

    void Start()
    {
        startPosition = transform.position; // Save the platform starting position as current position 
        targetPosition = startPosition + Vector3.down * moveDistance; // Calculates where the targetPosition should be 
    }

    void Update()
    {
        if (startMove) // Only activate if the the startMove is turn into true 
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime); // Move the platform from its current position toward the target position
        }
    }

    public void MoveDown()
    {
        startMove = true; // Change the startMove into true
    }
}
