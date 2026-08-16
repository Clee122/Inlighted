using System.Runtime.CompilerServices;
using UnityEngine;

public class LightUImanager : MonoBehaviour
{
    [SerializeField] private LightHUD lightHUD;
    [SerializeField] private PlayerLightResource lightResource;

    private void OnEnable()
    {
        lightResource.OnLightChanged += HandleLightChanged;
    }

    private void OnDisable()
    {
        lightResource.OnLightChanged -= HandleLightChanged;
    }

    private void Start()
    {
        HandleLightChanged(lightResource.GetCurrentLight(), lightResource.GetMaximumLight());
    }

    private void HandleLightChanged(float current, float max)
    {
        lightHUD.Light(current, max);
    }
}


