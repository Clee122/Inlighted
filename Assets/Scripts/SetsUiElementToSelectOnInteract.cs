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

        if (eventSystem == null) Debug.LogError("No EventSystem found in scene!", this);
    }

    public void JumpToElement()
    {
        if (eventSystem == null) Debug.LogError("No EventSystem found in scene!", this);
        if (objectToSelect == null) Debug.LogWarning("No object to select", this);

        eventSystem.SetSelectedGameObject(objectToSelect.gameObject);
    }
}
