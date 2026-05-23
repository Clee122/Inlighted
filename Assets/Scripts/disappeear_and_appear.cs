using UnityEngine;
using System.Collections;

public class disappeear_and_appear : MonoBehaviour
{
    [Header("fade Settings")]
    public float disappeear = 1f;
    public float unfade = 5f;
    public float appear = 1f;

    private SpriteRenderer[] spriterenderer;
    private Collider2D[] fadingcollider;

    private void Awake()
    {
        spriterenderer = GetComponents<SpriteRenderer>();
        fadingcollider = GetComponents<Collider2D>();
    }

    private void Start()
    {
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        changealpha(0f);
        enablecollider(false);

        yield return Fadeinandout(1f, appear);

        enablecollider(true);

        yield return new WaitForSeconds(unfade);

        enablecollider(false);

        yield return Fadeinandout(0f, disappeear);

        Destroy(gameObject);
    }

    private IEnumerator Fadeinandout(float fadeAlpha, float Fadetime)
    {
        float startAlpha = spriterenderer.Length > 0 ? spriterenderer[0].color.a : 1f;
        float timer = 0f;

        while (timer < Fadetime)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, fadeAlpha, timer / Fadetime);
            changealpha(alpha);
            yield return null;
        }

        changealpha(fadeAlpha);
    }

    private void changealpha(float alpha)
    {
        foreach (SpriteRenderer sprite in spriterenderer)
        {
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }
    }

    private void enablecollider(bool enable)
    {
        foreach (Collider2D collider in fadingcollider)
        {
            collider.enabled = enable;
        }
    }
}