using System;
using UnityEngine;

public class ParallaxChange : MonoBehaviour
{
    public Boolean EnableParallax;
    public Boolean DisableParallax;

    public GameObject ParallaxToEnable;
    public GameObject ParallaxToDisable;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (DisableParallax)
            {
                ParallaxToDisable.SetActive(false);
            }
            if (EnableParallax)
            {
                ParallaxToEnable.SetActive(true);
            }
        }
    }
}
