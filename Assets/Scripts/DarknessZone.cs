using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DarknessZone : MonoBehaviour
{
    [Header("Dispel Settings")]
    [SerializeField] private float reformDelay = 3f;
    [SerializeField] private float reformCheckInterval = 0.1f;

    private Collider2D darknessCollider;
    private SpriteRenderer spriteRenderer;
    private Coroutine reformCoroutine;

    private bool isDispelled = false;

    private Bounds lastKnownBounds;

    private readonly HashSet<PlayerDarknessTracker> playersInside = new HashSet<PlayerDarknessTracker>();

    private void Awake()
    {
        darknessCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (darknessCollider != null)
        {
            lastKnownBounds = darknessCollider.bounds;
        }
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
        if (isDispelled)
            return;

        if (reformCoroutine != null)
        {
            StopCoroutine(reformCoroutine);
        }

        reformCoroutine = StartCoroutine(DispelRoutine());
    }

    private IEnumerator DispelRoutine()
    {
        isDispelled = true;

        if (darknessCollider != null)
        {
            lastKnownBounds = darknessCollider.bounds;
        }

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

        // First delay after being dispelled
        yield return new WaitForSeconds(reformDelay);

        // If light burst is still overlapping, stay dispelled
        while (IsActiveLightBurstOverlapping() || IsActiveLightBeamOverlapping())
        {
            yield return new WaitForSeconds(reformCheckInterval);
        }

        // Important: once the burst is gone, give the player time to react
        yield return new WaitForSeconds(reformDelay);

        Reform();
    }

    private bool IsActiveLightBurstOverlapping()
    {
        LightBurstController[] bursts = Object.FindObjectsByType<LightBurstController>(FindObjectsSortMode.None);

        foreach (LightBurstController burst in bursts)
        {
            if (burst == null || !burst.IsBurstActive())
                continue;

            float radius = burst.GetBurstDispelRadius();

            Vector2 burstPosition = burst.transform.position;
            Vector2 closestPoint = lastKnownBounds.ClosestPoint(burstPosition);

            float distance = Vector2.Distance(burstPosition, closestPoint);

            if (distance <= radius)
            {
                return true;
            }
        }

        return false;
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

    private bool IsActiveLightBeamOverlapping()
    {
        LightBeamController[] beams = Object.FindObjectsByType<LightBeamController>(FindObjectsSortMode.None);

        foreach (LightBeamController beam in beams)
        {
            if (beam == null || !beam.IsBeamActive())
                continue;

            if (beam.IsBoundsOverlappingActiveBeam(lastKnownBounds))
            {
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Test Dispel")]
    private void TestDispel()
    {
        Dispel();
    }
}