using UnityEngine;
using System.Collections;

public class appear_and_disappeear_by_burst : MonoBehaviour
{
    [Header("Platform Collider")]
    [SerializeField] private Collider2D platformCollider;

    [Header("Disable")]
    [SerializeField] private float disable = 0.5f;

    private Coroutine disableCoroutine;
    private void Awake()
    {
        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }

         enableCollider(false);

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite != null)
            {
                sprite.enabled = true;
                sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }
    }


    public void ActivatePlatform()
    {
        enableCollider(true);

        if (disableCoroutine != null)
        {
            StopCoroutine(disableCoroutine);
        }

        disableCoroutine = StartCoroutine(TurnoffCollider());
    }

    private IEnumerator TurnoffCollider()
    {
        yield return new WaitForSeconds(disable);

        enableCollider(false);
        disableCoroutine = null;
    }

        private void enableCollider(bool enable)
    {
        if (platformCollider != null)
        {
            platformCollider.enabled = enable;
        }
    }
}