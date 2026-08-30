using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public GameObject PauseMenu;
    public GameObject SettingsMenu;

    // The pause system is locked once the end-game sequence begins so the
    // player cannot open or close pause menus while the ending is being shown.
    private bool isLocked = false;

    private void Start()
    {
        // Both menus begin hidden so gameplay starts normally rather than
        // displaying pause-related UI as soon as the level loads.
        PauseMenu.SetActive(false);
        SettingsMenu.SetActive(false);
    }

    public void Pause()
    {
        // Once the end-game sequence has started, pause input is ignored so it
        // cannot interfere with the end-game screen or its transition.
        if (isLocked)
            return;

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
        // Once the end-game sequence has started, unpause input is ignored so
        // gameplay cannot resume underneath the end-game screen.
        if (isLocked)
            return;

        // Restoring the time scale continues gameplay from exactly the point
        // where it was paused.
        Time.timeScale = 1f;

        PauseMenu.SetActive(false);
        SettingsMenu.SetActive(false);
    }

    public void LockPause()
    {
        // The end-game trigger calls this once the ending begins. Hiding both
        // menus prevents an already-open pause/settings panel from remaining
        // visible over the end-game screen.
        isLocked = true;
        PauseMenu.SetActive(false);
        SettingsMenu.SetActive(false);
    }
}