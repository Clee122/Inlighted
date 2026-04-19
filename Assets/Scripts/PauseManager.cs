using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public GameObject PauseMenu;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //disable pause menu at beginning of scene
        PauseMenu.SetActive(false);
    }

    public void Pause()
    {
        //pause game by setting timescale to 0
        Time.timeScale = 0;
        //display pause menu
        PauseMenu.SetActive(true);
    }

    public void UnPause()
    {
        //unpause game by setting timescale to 1
        Time.timeScale = 1;
        //hide pause menu
        PauseMenu.SetActive(false);
    }
}
