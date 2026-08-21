using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour, IPointerEnterHandler
{
    [Header("Scene")]
    // The scene name remains editable so the same script can continue being
    // reused by Play, Settings, Credits and other scene-loading buttons.
    public string sceneName;

    [Header("Audio")]
    // Hover audio gives immediate feedback when the pointer reaches a button.
    // It is separate from click audio because hovering does not perform an action.
    [SerializeField] private AudioClip hoverSound;

    // Click audio confirms that the player has actually selected the button.
    [SerializeField] private AudioClip clickSound;

    // Scene-changing buttons wait briefly before loading because destroying the
    // current scene immediately could cut off its AudioManager and click sound.
    [SerializeField] private float clickSoundDelay = 0.1f;

    private bool isProcessingAction;

    public void OnPointerEnter(
        PointerEventData eventData
    )
    {
        // Hover audio does not need any delay because entering the button does
        // not destroy the scene or perform another menu action.
        if (
            hoverSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.PlaySFX(
                hoverSound
            );
        }
    }

    public void GoToScene()
    {
        // Prevent repeated clicks from starting multiple scene loads while the
        // short click-audio delay is still running.
        if (isProcessingAction)
        {
            return;
        }

        isProcessingAction = true;

        StartCoroutine(
            LoadSceneRoutine()
        );
    }

    private IEnumerator LoadSceneRoutine()
    {
        Time.timeScale = 1f;

        // Click audio plays only when the button actually begins its scene action,
        // keeping hover and confirmation feedback clearly separated.
        PlayClickSound();

        if (clickSoundDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                clickSoundDelay
            );
        }

        SceneManager.LoadScene(
            sceneName
        );
    }

    public void QuitGame()
    {
        // The same protection prevents repeated Quit clicks while the click
        // feedback is being allowed to play.
        if (isProcessingAction)
        {
            return;
        }

        isProcessingAction = true;

        StartCoroutine(
            QuitGameRoutine()
        );
    }

    private IEnumerator QuitGameRoutine()
    {
        Time.timeScale = 1f;

        PlayClickSound();

        if (clickSoundDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                clickSoundDelay
            );
        }

        Application.Quit();

        Debug.Log(
            "Quit button pressed."
        );
    }

    private void PlayClickSound()
    {
        // Keeping click playback in one helper ensures scene-loading and Quit
        // buttons use exactly the same audio behaviour.
        if (
            clickSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.PlaySFX(
                clickSound
            );
        }
    }
}