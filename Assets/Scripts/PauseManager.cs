using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class PauseManager : MonoBehaviour
{
    private Boolean CurrentPause = false;

    public GameObject PauseMenu;

    KeyCode PauseKey = KeyCode.Escape;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //disable pause menu at beginning of scene
        PauseMenu.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(PauseKey)) 
        {
            Debug.Log("pressed escape");

            if (Input.GetKeyDown(PauseKey) && CurrentPause == false)
            {
                //detect pause key pressed
                Debug.Log("Pause Key Pressed");
                //start pause
                Pause();
            }
            else if (Input.GetKeyDown(PauseKey) && CurrentPause == true)
            {
                //detect pause key pressed while paused
                Debug.Log("Pause Key Pressed for unpause");
                //start unpause
                UnPause();
            }
        }

    }

    private void Pause()
    {
        //pause game by setting timescale to 0
        Time.timeScale = 0;
        //set bool true to allow pause key to unpause next press
        CurrentPause = true;
        //display pause menu
        PauseMenu.SetActive(true);
    }

    public void UnPause()
    {
        //unpause game by setting timescale to 1
        Time.timeScale = 1;
        //set bool false to allow pause key to pause next press
        CurrentPause = false;
        //hide pause menu
        PauseMenu.SetActive(false);
    }
}
