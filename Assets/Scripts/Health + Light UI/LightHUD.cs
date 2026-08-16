using NUnit.Framework.Constraints;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class LightHUD : MonoBehaviour
{
    [System.Serializable]
    public class StarSlot //using a nested class why are these fields private by default 
    {

        public Image lightEmpty;
        public Image lightFull; //filled image 
        public Animator animator;
        public LightState currentState = LightState.Full;
    }

    public List<StarSlot> stars;
    public float fillSpeed = 5f;

    //makes it drain smooth and lets it sit at any percent instead of going between sprites 
    private float[] lightFill;
    private float[] displayFill;

    private void Awake()
    {
        lightFill = new float[stars.Count];
        displayFill = new float[stars.Count];
    }

    public void Light(float current, float max)
    {
        //calls how many stars worth of light so the drain is spread across multiple stars
        float proportion = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        float litUnits = proportion * stars.Count;

        for (int i = 0; i < stars.Count; i++)
        {
            lightFill[i] = Mathf.Clamp01(litUnits - i); //so stars dain in order 
        }
    }

    private void Update()
    {
        for (int i = 0; i < stars.Count; i++)
        {
            //mathF.MoveTowards is used to nudge the displayed value closer to the target, making the drain effect look smooth and uses deltaTime to have it drain at the same speed of the framerate
            displayFill[i] = Mathf.MoveTowards(displayFill[i], lightFill[i], fillSpeed * Time.deltaTime);
            stars[i].lightFull.fillAmount = displayFill[i];

            LightState newState = GetStateFromFill(displayFill[i]);

            //fires only when the state actually changes 
            if (newState != stars[i].currentState)
            {
                stars[i].currentState = newState;
                SetLightState(stars[i], newState);
            }
        }
    }


    private LightState GetStateFromFill(float fill)
    {
        //turns the smooth 0-1 fill value into the 3 states for the animator
        if (fill < 0f) return LightState.Empty;
        if (fill > 1f) return LightState.Full;
        return LightState.Partial;
    }

    public void SetLightState(StarSlot star, LightState state)
    {
        if (star.animator == null)
            return;

        switch (state)
        {
            case LightState.Empty:
                star.animator.SetTrigger("lightEmpty");
                break;
            case LightState.Partial:
                star.animator.SetTrigger("lightPartial");
                break;
            case LightState.Full:
                star.animator.SetTrigger("lightFull");
                break;
        }
    }
}
public enum LightState
{
    Empty = 0,
    Partial = 1,
    Full = 2,
}
