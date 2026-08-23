using NUnit.Framework.Constraints;
using UnityEngine;

public class SettingsManager : MonoBehaviour

{
    //singleton to call without a reference 
   public static SettingsManager Instance;

    private const string Fullscreen_KEY = "Fullscreen";

    private void Awake()
    {
        //make sure only one of these exist at one time 
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAndApplySettings();
    }
     
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(Fullscreen_KEY, isFullscreen ? 1 : 0); //saves a 1 or a 0 to the disk under the name "fullscreen" to tell which is toggled 
        PlayerPrefs.Save();
    }

    public bool GetFullscreen() => PlayerPrefs.GetInt(Fullscreen_KEY,1) == 1;

    //runs on startup and applys what was set from last time
    private void LoadAndApplySettings()
    {
        Screen.fullScreen = GetFullscreen();
    }
}

