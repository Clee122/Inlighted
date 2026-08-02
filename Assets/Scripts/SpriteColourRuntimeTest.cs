using UnityEngine;

public class SpriteColourRuntimeTest : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetSpriteRenderer;

    private void Awake()
    {
        // The test finds the renderer automatically when attached directly to the
        // CatMoth Visual object, reducing the chance of testing the wrong renderer.
        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        if (targetSpriteRenderer == null)
        {
            return;
        }

        // Pure red is used because it gives an unmistakable result and confirms
        // whether runtime RGB tinting reaches the visible CatMoth renderer.
        targetSpriteRenderer.color = Color.red;
    }
}