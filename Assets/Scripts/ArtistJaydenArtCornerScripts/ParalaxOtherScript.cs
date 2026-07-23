using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    public enum MovementDirection { Normal, Inverted }

    [Header("Camera Link")]
    [SerializeField] private Transform cameraTransform;

    [Header("Direction Fix")]
    [Tooltip("If the background moves the wrong way, switch this to Inverted.")]
    [SerializeField] private MovementDirection directionMode = MovementDirection.Normal;

    [Header("Speed Multipliers")]
    [Range(0f, 1f)][SerializeField] private float parallaxSpeedX = 0.5f;
    [Range(0f, 1f)][SerializeField] private float parallaxSpeedY = 0.1f;

    private Vector3 startBackgroundPos;
    private Vector3 startCameraPos;

    private void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Freeze the starting positions
        startBackgroundPos = transform.position;
        startCameraPos = cameraTransform.position;
    }

    private void LateUpdate()
    {
        // Calculate overall camera travel distance
        Vector3 totalDistanceMoved = cameraTransform.position - startCameraPos;

        // Switch sign based on drop-down selection
        float directionModifier = (directionMode == MovementDirection.Inverted) ? -1f : 1f;

        // Apply new coordinate math
        float targetX = startBackgroundPos.x + (totalDistanceMoved.x * parallaxSpeedX * directionModifier);
        float targetY = startBackgroundPos.y + (totalDistanceMoved.y * parallaxSpeedY * directionModifier);

        transform.position = new Vector3(targetX, targetY, transform.position.z);
    }
}