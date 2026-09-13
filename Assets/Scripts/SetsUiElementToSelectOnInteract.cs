using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SetsUiElementToSelectOnInteract : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private Selectable objectToSelect;

    [Header("Visualisation")]
    [SerializeField] private bool showVisualisation;
    [SerializeField] private Color navigationColour = Color.cyan;

    // The pause menu is disabled during gameplay and enabled when pausing.
    // Selecting the first button here ensures controller navigation has a valid starting point
    // every time the menu becomes visible.
    private void OnEnable()
    {
        JumpToElement();
    }

    private void OnDrawGizmos()
    {
        if (!showVisualisation) return;
        if (objectToSelect == null) return;

        Gizmos.color = navigationColour;
        Gizmos.DrawLine(gameObject.transform.position, objectToSelect.transform.position);
    }

    private void Reset()
    {
        eventSystem = FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
            Debug.LogError("No EventSystem found in scene!", this);
    }

    public void JumpToElement()
    {
        if (eventSystem == null)
        {
            Debug.LogError("No EventSystem found in scene!", this);
            return;
        }

        if (objectToSelect == null)
        {
            Debug.LogWarning("No object to select", this);
            return;
        }

        // Clear the previous selection first so Unity reliably refreshes
        // controller navigation when the pause menu opens.
        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(objectToSelect.gameObject);
    }
}