using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public GameObject PauseMenu;
    public GameObject SettingsMenu;

    private void Start()
    {
        // Both menus begin hidden so gameplay starts normally rather than
        // displaying pause-related UI as soon as the level loads.
        PauseMenu.SetActive(false);
        SettingsMenu.SetActive(false);
    }

    public void Pause()
    {
        // Freezing Unity's time keeps the current gameplay state exactly where
        // it is while the player uses the pause menu.
        Time.timeScale = 0f;

        PauseMenu.SetActive(true);
        SettingsMenu.SetActive(false);
    }

    public void OpenSettings()
    {
        // Switching panels rather than changing scenes preserves the player's
        // position, puzzle state and other runtime information.
        PauseMenu.SetActive(false);
        SettingsMenu.SetActive(true);
    }

    public void BackToPauseMenu()
    {
        // Returning from Settings keeps the game paused until the player
        // explicitly chooses Resume.
        SettingsMenu.SetActive(false);
        PauseMenu.SetActive(true);
    }

    public void UnPause()
    {
        // Restoring the time scale continues gameplay from exactly the point
        // where it was paused.
        Time.timeScale = 1f;

        PauseMenu.SetActive(false);
        SettingsMenu.SetActive(false);
    }
}