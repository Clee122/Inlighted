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

    [Header("shake")]
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeStrength = 10f; //pixel jitter

    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Coroutine ShakeRoutine;
    private void Awake()
    {
        healthImage = GetComponent<Image>();
        animator = GetComponent<Animator>();
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
    }

    public void SetHealthState(HealthState state) //using a switch state which will work for only having 3 hearts
    {
        switch (state)
        {
            case HealthState.Empty:
                animator.SetTrigger("FadeOut"); //animator handles a visualfade that can than hide image
                PlayShake();//shakes as it starts fading
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
    
    private void PlayShake()
    {
        //stops any shake running so it can't stack
        if (ShakeRoutine != null)
        {
            StopCoroutine(ShakeRoutine);
            rectTransform.anchoredPosition = originalPosition; //sets back to original position before starting a fresh shake
        }

        ShakeRoutine = StartCoroutine(shakeRoutine());
    }

    private IEnumerator shakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)

        {
            float offsetX = Random.Range(-shakeStrength, shakeStrength);
            float offsetY = Random.Range(-shakeStrength, shakeStrength);

            rectTransform.anchoredPosition = originalPosition + new Vector2(offsetX, offsetY);
            elapsed += Time.deltaTime;
            yield return null;
        }

        //always end back at the og position 
        rectTransform.anchoredPosition = originalPosition;
        ShakeRoutine = null;
    }    
}

public enum HealthState //this lets me reference the health states from sprite fullheart, halfheart, and emptyheart
{
    Empty = 0,
    Half = 1,
    Full = 2
}
