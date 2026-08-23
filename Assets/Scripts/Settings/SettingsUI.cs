using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private Toggle FullscreenToggle;

    private void Start()
    {
        if (SettingsManager.Instance == null)
        {
            Debug.Log("no settings manager found in scene");
            return;
        }

        FullscreenToggle.SetIsOnWithoutNotify(SettingsManager.Instance.GetFullscreen()); //sets toggle to match save state without firing the onValueChanged
        FullscreenToggle.onValueChanged.AddListener(SettingsManager.Instance.SetFullscreen); //hooks the toggle clicks up to actually apply the save setting
    }

    private void OnDestroy()
    {
        if (SettingsManager.Instance == null) return;

        FullscreenToggle.onValueChanged.RemoveListener(SettingsManager.Instance.SetFullscreen);//unsubscribes from onValueChanged since this UI object dies when the scene unloads but SettingsManager doesn't
    }
}
