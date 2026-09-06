using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ActEndTrigger : MonoBehaviour
{
    [SerializeField] private string actCompleteMessage = "End of Act 1";
    [SerializeField] private GameObject promptUI;// assign text/panel 
    [SerializeField] private GameObject endPanel; //demo complete panel with a restart and menu and quit
    [SerializeField] private CanvasGroup endPanelCanvas;
    [SerializeField] private float fadeDuration = 1.0f;

    public UnityEvent onActComplete; //hook up ui in inspector

    private bool playerInRange = false;
    private bool hasTriggered = false;

    private PauseManager pauseManager;

    private void Awake()
    {
        pauseManager = FindFirstObjectByType<PauseManager>();
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
        if (playerInRange &&  !hasTriggered && Keyboard.current.cKey.wasPressedThisFrame)
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
            elapsed += Time.unscaledDeltaTime; //ignores timescale keeps a fading while pausing
            endPanelCanvas.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        endPanelCanvas.alpha = 1.0f;
    }
}
