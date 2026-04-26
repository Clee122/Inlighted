using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour
{
    public string sceneName;
    // used to type the scene in the editor so script is reusable

    public void GoToScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

        Application.Quit();
        Debug.Log("quit button pressed");
    }
}