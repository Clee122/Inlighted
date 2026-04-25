using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DarknessZone : MonoBehaviour
{
    [Header("Dispel Settings")]
    [SerializeField] private float reformDelay = 3f;

    private Collider2D darknessCollider;
    private SpriteRenderer spriteRenderer;
    private Coroutine reformCoroutine;

    private bool isDispelled = false;

    private readonly HashSet<PlayerDarknessTracker> playersInside = new HashSet<PlayerDarknessTracker>();

    private void Awake()
    {
        darknessCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDispelled)
            return;

        PlayerDarknessTracker darknessTracker = other.GetComponent<PlayerDarknessTracker>();

        if (darknessTracker != null)
        {
            playersInside.Add(darknessTracker);
            darknessTracker.EnterDarkness();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDarknessTracker darknessTracker = other.GetComponent<PlayerDarknessTracker>();

        if (darknessTracker != null && playersInside.Contains(darknessTracker))
        {
            playersInside.Remove(darknessTracker);
            darknessTracker.ExitDarkness();
        }
    }

    public void Dispel()
    {
        if (reformCoroutine != null)
        {
            StopCoroutine(reformCoroutine);
        }

        reformCoroutine = StartCoroutine(DispelRoutine());
    }

    private IEnumerator DispelRoutine()
    {
        isDispelled = true;

        foreach (PlayerDarknessTracker tracker in playersInside)
        {
            if (tracker != null)
            {
                tracker.ExitDarkness();
            }
        }

        playersInside.Clear();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (darknessCollider != null)
        {
            darknessCollider.enabled = false;
        }

        Debug.Log(gameObject.name + " dispelled");

        yield return new WaitForSeconds(reformDelay);

        Reform();
    }

    private void Reform()
    {
        isDispelled = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (darknessCollider != null)
        {
            darknessCollider.enabled = true;
        }

        CheckForPlayerAfterReform();

        reformCoroutine = null;

        Debug.Log(gameObject.name + " reformed");
    }

    private void CheckForPlayerAfterReform()
    {
        if (darknessCollider == null)
            return;

        Bounds bounds = darknessCollider.bounds;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            bounds.center,
            bounds.size,
            transform.eulerAngles.z
        );

        foreach (Collider2D hit in hits)
        {
            PlayerDarknessTracker darknessTracker = hit.GetComponent<PlayerDarknessTracker>();

            if (darknessTracker != null && !playersInside.Contains(darknessTracker))
            {
                playersInside.Add(darknessTracker);
                darknessTracker.EnterDarkness();
            }
        }
    }

    [ContextMenu("Test Dispel")]
    private void TestDispel()
    {
        Dispel();
    }
}