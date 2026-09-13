using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ActEndTrigger : MonoBehaviour
{
    [SerializeField] private string actCompleteMessage = "End of Act 1";
    [SerializeField] private GameObject promptUI; // Assign text/panel.
    [SerializeField] private GameObject endPanel; // Demo complete panel with restart, menu and quit.
    [SerializeField] private CanvasGroup endPanelCanvas;
    [SerializeField] private float fadeDuration = 1.0f;

    [Header("Input")]
    // Using an Input Action Reference allows the end trigger to respond to the
    // same keyboard and controller interaction binding used elsewhere in the game.
    [SerializeField] private InputActionReference interactAction;

    public UnityEvent onActComplete; // Hook up UI in Inspector.

    private bool playerInRange = false;
    private bool hasTriggered = false;

    private PauseManager pauseManager;

    private void Awake()
    {
        pauseManager = FindFirstObjectByType<PauseManager>();
    }

    private void OnEnable()
    {
        // The action needs to be enabled so this trigger can receive controller input
        // even though it is not directly handled by the PlayerInput component.
        if (interactAction != null)
        {
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        // Disabling the action prevents this object from continuing to listen for
        // interaction input if the trigger is disabled or the scene changes.
        if (interactAction != null)
        {
            interactAction.action.Disable();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (promptUI != null)
            {
                promptUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // The Input Action handles controller/keyboard bindings, while C remains
        // as a fallback so the original keyboard behaviour is preserved.
        bool interactPressed =
            interactAction != null &&
            interactAction.action.WasPressedThisFrame();

        bool keyboardFallback =
            Keyboard.current != null &&
            Keyboard.current.cKey.wasPressedThisFrame;

        if (playerInRange && !hasTriggered && (interactPressed || keyboardFallback))
        {
            hasTriggered = true;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }

            CompleteAct();
        }
    }

    private void CompleteAct()
    {
        Debug.Log(actCompleteMessage);
        Time.timeScale = 0.0f;

        if (pauseManager != null)
        {
            pauseManager.LockPause();
        }

        onActComplete.Invoke();

        endPanel.SetActive(true);
        StartCoroutine(FadeInPanel());
    }

    private IEnumerator FadeInPanel()
    {
        float elapsed = 0f;
        endPanelCanvas.alpha = 0.0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Ignores time scale so fading continues while paused.
            endPanelCanvas.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        endPanelCanvas.alpha = 1.0f;
    }
}
