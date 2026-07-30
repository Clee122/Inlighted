using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
public class HealthHUD : MonoBehaviour
{
    public Sprite fullHealth, halfHealth, noHealth;
    Image healthImage;
    Animator animator;
    HealthState currentState = HealthState.Full;

    private void Awake()
    {
        healthImage = GetComponent<Image>();
        animator = GetComponent<Animator>();
    }

    public void SetHealthState(HealthState state) //using a switch state which will work for only having 3 hearts
    {
        switch (state)
        {
            case HealthState.Empty:
                animator.SetTrigger("FadeOut"); //animator handles a visualfade that can than hide image
                break;
            case HealthState.Half:
                healthImage.sprite = halfHealth;
                break;
            case HealthState.Full:
                animator.SetTrigger("FadeIn");// can fade health back in 
                healthImage.sprite = fullHealth;
                break;
        }
    }
}

public enum HealthState //this lets me reference the health states from sprite fullheart, halfheart, and emptyheart
{
    Empty = 0,
    Half = 1,
    Full = 2
}
