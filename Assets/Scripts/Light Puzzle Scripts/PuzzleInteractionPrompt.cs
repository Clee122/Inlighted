using UnityEngine;

public class PuzzleInteractionPrompt : MonoBehaviour
{
    [Header("Prompt")]

    // The prompt itself lives in the main Screen Space Overlay Canvas so it stays
    // upright and readable instead of inheriting a mirror's rotation or scale.
    [SerializeField] private GameObject promptObject;

    [Header("Prompt Position")]

    // This transform represents the puzzle object that the prompt should appear
    // above. If it is not assigned manually, the parent of this trigger is used.
    [SerializeField] private Transform promptTarget;

    // This world-space offset controls how far above the puzzle object the C
    // appears before its position is converted into screen coordinates.
    [SerializeField]
    private Vector3 worldOffset =
        new Vector3(
            0f,
            1f,
            0f
        );

    private Camera mainCamera;
    private bool playerIsInside;

    private void Awake()
    {
        mainCamera =
            Camera.main;

        if (promptTarget == null)
        {
            // The trigger is normally a child of the Mirror or LaserPointer, so
            // using its parent avoids needing another reference on every instance.
            promptTarget =
                transform.parent;
        }

        if (mainCamera == null)
        {
            Debug.LogWarning(
                "PuzzleInteractionPrompt could not find the Main Camera.",
                this
            );
        }
    }

    private void Start()
    {
        // The interaction hint remains hidden until the player enters the range.
        SetPromptVisible(
            false
        );
    }

    private void Update()
    {
        if (!playerIsInside)
        {
            return;
        }

        UpdatePromptPosition();
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerIsInside =
            true;

        // Position the prompt immediately before revealing it so there is no
        // single frame where the C appears at its previous Canvas position.
        UpdatePromptPosition();

        SetPromptVisible(
            true
        );
    }

    private void OnTriggerExit2D(
        Collider2D other
    )
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerIsInside =
            false;

        SetPromptVisible(
            false
        );
    }

    private void UpdatePromptPosition()
    {
        if (
            promptObject == null ||
            promptTarget == null ||
            mainCamera == null
        )
        {
            return;
        }

        // Screen Space Overlay UI uses screen coordinates, so converting the
        // puzzle object's world position lets the Canvas C visually follow it.
        Vector3 worldPosition =
            promptTarget.position +
            worldOffset;

        Vector3 screenPosition =
            mainCamera.WorldToScreenPoint(
                worldPosition
            );

        promptObject.transform.position =
            screenPosition;
    }

    public void HidePrompt()
    {
        SetPromptVisible(
            false
        );
    }

    private void SetPromptVisible(
        bool shouldShow
    )
    {
        if (promptObject == null)
        {
            return;
        }

        promptObject.SetActive(
            shouldShow
        );
    }

    private void OnDisable()
    {
        playerIsInside =
            false;

        SetPromptVisible(
            false
        );
    }
}
