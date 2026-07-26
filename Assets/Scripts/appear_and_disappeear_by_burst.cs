using UnityEngine;
using System.Collections;

public class appear_and_disappeear_by_burst : MonoBehaviour
{
    private SpriteRenderer[] spriteRenderers;
    private Collider2D[] platformColliders;
    private float disappeearing = 0.3f;
    private Coroutine disappeearingCoroutine;
    
    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        platformColliders = GetComponentsInChildren<Collider2D>();
    }

    private void Start()
    {
        HidePlatform();
    }

    
    public void ShowPlatform()
    {
        foreach (SpriteRenderer sprite in spriteRenderers)
        {
            sprite.enabled = true;
        }

        foreach (Collider2D platformCollider in platformColliders)
        {
            platformCollider.enabled = true;
        }
        
        if (disappeearingCoroutine != null)
        {
            StopCoroutine(disappeearingCoroutine);
        }

            disappeearingCoroutine = StartCoroutine(becomehide());
    }

    private IEnumerator becomehide()
    {
        yield return new WaitForSeconds(disappeearing);

        HidePlatform();
        disappeearingCoroutine = null;
    }

    private void HidePlatform()
    {
        
        foreach (SpriteRenderer sprite in spriteRenderers)
        {
            sprite.enabled = false;
        }

    }
}
