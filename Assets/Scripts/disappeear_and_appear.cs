using UnityEngine;
using System.Collections;

public class disappeear_and_appear : MonoBehaviour
{
    [Header("fade Settings")]
    public float disappeear = 1f;  // set the fade out time as 1f
    public float unfade = 5f; // set the time when the platform stays visible
    public float appear = 1f; // set the fade in time as 1f

    private SpriteRenderer[] spriterenderer; // Stores all spriterenderer  
    private Collider2D[] fadingcollider;  // Stores all collider  

    private void Awake()
    {
        spriterenderer = GetComponents<SpriteRenderer>(); // Gets all SpriteRenderer components attach to this object
        fadingcollider = GetComponents<Collider2D>(); // Gets all Collider2D components attach to this object
    }

    private void Start()
    {
        StartCoroutine(FadeRoutine()); // start running the FadeRoutine
    }

    private IEnumerator FadeRoutine()
    {
        changealpha(0f); // Make the platform invisible 
        enablecollider(false); // Disable collider so player cannot stand on it

        yield return Fadeinandout(1f, appear); // slowly Fade in to fully visible

        enablecollider(true); // Enable collider so player can stand on it 

        yield return new WaitForSeconds(unfade); // make the platform stay for 5 seconds before fading out

        enablecollider(false); // Disable collider again

        yield return Fadeinandout(0f, disappeear); // make the platform fade out to invisible

        Destroy(gameObject); // Delete the platform
    }

    private IEnumerator Fadeinandout(float fadeAlpha, float Fadetime)
    {
        float startAlpha = spriterenderer.Length > 0 ? spriterenderer[0].color.a : 1f; // Get the starting alpha
        float timer = 0f; //  set the timer from 0

        while (timer < Fadetime)
        {
            timer += Time.deltaTime; //increases the timer every frame
            float alpha = Mathf.Lerp(startAlpha, fadeAlpha, timer / Fadetime);  // slowly change alpha from startAlpha to fadeAlpha over time
            changealpha(alpha);
            yield return null;
        }

        changealpha(fadeAlpha); // Make sure alpha reaches the final value
    }

    private void changealpha(float alpha)
    {
        foreach (SpriteRenderer sprite in spriterenderer) // this changes the transparency of every SpriteRenderer on the platform.
        {
            Color color = sprite.color; // Gets the current color.
            color.a = alpha; // Changes the alpha
            sprite.color = color; // applies the new alpha back to the sprite
        }
    }

    private void enablecollider(bool enable)
    {
        foreach (Collider2D collider in fadingcollider)
        {
            collider.enabled = enable; // this control all to collider on or off.
        }
    }
}