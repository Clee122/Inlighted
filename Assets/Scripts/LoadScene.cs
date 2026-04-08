using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour
{
    public string sceneName;
    //used to type the scene in the editor so script is reusable

    public void GoToScene()
    //when button pressed, loads scene with name matching the string
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    //when quit button pressed
    {
        Application.Quit();
        //quits game, only works in built so debug is used to check working
        Debug.Log("quit button pressed");
    }
}
