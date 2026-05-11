using UnityEngine;
using System.Collections;

public class MovingPlatform : MonoBehaviour
{
    public float moveDistance;
    public float moveSpeed = 2f;

    public Vector3 startPosition;
    public Vector3 targetPosition;
    private bool shouldMove = false;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.down * moveDistance;
    }

    void Update()
    {
        if (shouldMove)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
    }

    public void MoveDown()
    {
        shouldMove = true;
    }
}
