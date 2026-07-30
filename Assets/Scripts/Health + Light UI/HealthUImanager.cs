using UnityEngine;

public class HealthUImanager : MonoBehaviour
{
    [SerializeField] private HealthHUD[] icons;
    
    private int previousLives = -1; // to indicate no event has fired yet
    private void OnEnable()
    {
        PlayerLifeSystem.OnLivesChanged += HandleLivesChanged;
    }

    private void OnDisable()
    {
        PlayerLifeSystem.OnLivesChanged -= HandleLivesChanged;
    }

    private void HandleLivesChanged(int currentLives, int maxLives)
    {
        for(int i = 0; i < icons.Length; i++)
        {
            bool wasFull = previousLives < 0 ? true : i < previousLives;  // true on the first event firing,otherwise checks for oldlives count 
            bool isFull = i < currentLives; // checks if health icon should be full or not

            if (wasFull != isFull) // only updates the icon whose state is actually flipped to stop the animation fadein retriggering 
            {

                icons[i].SetHealthState(i < currentLives ? HealthState.Full : HealthState.Empty);

            }
        }
        previousLives = currentLives;
    }
}
