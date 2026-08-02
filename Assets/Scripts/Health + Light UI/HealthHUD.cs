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
    private Coroutine shakeRoutine;

    [Header("Health Regen Juice Overshoot")]
    [SerializeField] private float overShotScale = 0.2f; //how big the health bumps up to before going back to og size when regening by %
    [SerializeField] private float overShotDuration = 0.3f; //how long it takes for it to bump up and settle

    private Vector3 originalScale;
    private Coroutine overShootRoutine;
    private void Awake()
    {
        healthImage = GetComponent<Image>();
        animator = GetComponent<Animator>();
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;
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
                PlayOverShoot(); //punches health up in scale then settles as it fades in
                break;
        }
    }
    
    private void PlayShake()
    {
        //stops any shake running so it can't stack
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            rectTransform.anchoredPosition = originalPosition; //sets back to original position before starting a fresh shake
        }

        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
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
        shakeRoutine = null;
    }

    private void PlayOverShoot()
    {
        if (overShootRoutine != null) //stops stacking of the overshoot
        {
            StopCoroutine(overShootRoutine);
            rectTransform.localScale = originalScale; //resets scale before starting a fresh overshoot 
        }

        overShootRoutine = StartCoroutine(OverShootRoutine());
    }

    private IEnumerator OverShootRoutine()
    {
        float elapsed = 0f;
        while (elapsed < overShotDuration)
        {
            //0 - 1 over the duration  
            float t = elapsed / overShotDuration; // t holds a fraction of 0 to 1 and is not mesured in seconds 
            float scaleAmount = Mathf.Sin(t * Mathf.PI) * (overShotScale); //quickly bumps upthen eases back down to original size using a sin wave 
            rectTransform.localScale = originalScale * (1f + scaleAmount);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rectTransform.localScale = originalScale; // ends back at original scale
        overShootRoutine = null;
    }
}

public enum HealthState //this lets me reference the health states from sprite fullheart, halfheart, and emptyheart
{
    Empty = 0,
    Half = 1,
    Full = 2
}
