using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DarknessZone : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject darknessVisual;

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

        // The red/greybox sprite was useful while blocking out the mechanic,
        // but now the VFX child should be the actual darkness the player sees.
        // Keeping the old sprite hidden avoids the debug-looking square coming back after reform.
        if (spriteRenderer != null && darknessVisual != null)
        {
            spriteRenderer.enabled = false;
        }

        // The collider may be disabled while darkness is dispelled, so I store its bounds early.
        // The stored bounds are later used to check whether an active light ability is still covering this area.
        if (darknessCollider != null)
        {
            lastKnownBounds = darknessCollider.bounds;
        }

        // The VFX starts active because the darkness should be visible unless it has been cleared by light.
        if (darknessVisual != null)
        {
            darknessVisual.SetActive(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If the darkness has been dispelled, it should not damage the player or count as active darkness.
        // This keeps the visual state and gameplay state consistent.
        if (isDispelled)
            return;

        PlayerDarknessTracker darknessTracker = other.GetComponent<PlayerDarknessTracker>();

        if (darknessTracker != null)
        {
            // A HashSet is used so the same player tracker is not added more than once.
            // This helps avoid messy damage counting if trigger events happen repeatedly.
            playersInside.Add(darknessTracker);
            darknessTracker.EnterDarkness();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerDarknessTracker darknessTracker = other.GetComponent<PlayerDarknessTracker>();

        if (darknessTracker != null && playersInside.Contains(darknessTracker))
        {
            // The tracker needs to be told when the player leaves so darkness damage stops correctly.
            // This is especially important now that multiple small darkness zones can overlap.
            playersInside.Remove(darknessTracker);
            darknessTracker.ExitDarkness();
        }
    }

    public void Dispel()
    {
        // If this zone is already gone, do not restart the reform timer every frame.
        // This prevents light burst/beam from constantly resetting the same darkness piece.
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

        // Store the collider bounds before disabling the collider.
        // Once disabled, we still need to know where the darkness used to be so we can check
        // whether light burst or light beam is still overlapping that cleared space.
        if (darknessCollider != null)
        {
            lastKnownBounds = darknessCollider.bounds;
        }

        // If the player was inside this darkness when it was cleared, they should stop taking damage from it.
        // This prevents invisible/dispelled darkness from continuing to hurt the player.
        foreach (PlayerDarknessTracker tracker in playersInside)
        {
            if (tracker != null)
            {
                tracker.ExitDarkness();
            }
        }

        playersInside.Clear();

        // The VFX is now the real visual darkness, so this is what should disappear when light clears it.
        if (darknessVisual != null)
        {
            darknessVisual.SetActive(false);
        }

        // Keep the old greybox sprite hidden. It now acts more like an editor/debug marker,
        // not something the player should see during gameplay.
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // The collider is disabled while dispelled so the player can move through the cleared darkness safely.
        if (darknessCollider != null)
        {
            darknessCollider.enabled = false;
        }

        Debug.Log(gameObject.name + " dispelled");

        // This first delay gives the player a short window after the darkness is cleared.
        yield return new WaitForSeconds(reformDelay);

        // Darkness should not reform while a light ability is still covering it.
        // Without this check, the darkness can flicker back for a moment and then disappear again.
        while (IsActiveLightBurstOverlapping() || IsActiveLightBeamOverlapping())
        {
            yield return new WaitForSeconds(reformCheckInterval);
        }

        // After the light is gone, give the player a final reaction window before the danger returns.
        // This makes the reform feel fairer instead of snapping back instantly.
        yield return new WaitForSeconds(reformDelay);

        Reform();
    }

    private bool IsActiveLightBurstOverlapping()
    {
        // The darkness checks all active bursts because the darkness itself controls when it reforms.
        // This keeps the reform behaviour consistent no matter which light ability cleared it.
        LightBurstController[] bursts = Object.FindObjectsByType<LightBurstController>(FindObjectsSortMode.None);

        foreach (LightBurstController burst in bursts)
        {
            if (burst == null || !burst.IsBurstActive())
                continue;

            float radius = burst.GetBurstDispelRadius();

            Vector2 burstPosition = burst.transform.position;
            Vector2 closestPoint = lastKnownBounds.ClosestPoint(burstPosition);

            float distance = Vector2.Distance(burstPosition, closestPoint);

            // If the burst radius still reaches this darkness area, the darkness should stay cleared.
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

        // Do not re-enable the red/greybox sprite here.
        // The child VFX is now responsible for showing that darkness has returned.
        if (spriteRenderer != null && darknessVisual != null)
        {
            spriteRenderer.enabled = false;
        }

        // The collider returns with the VFX so the visible danger and gameplay danger match again.
        if (darknessCollider != null)
        {
            darknessCollider.enabled = true;
        }

        if (darknessVisual != null)
        {
            darknessVisual.SetActive(true);
        }

        // If the player is still standing where the darkness reforms, the tracker needs to know immediately.
        // Otherwise the player could stand inside reformed darkness without taking damage.
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
                // This covers the case where darkness reforms on top of the player.
                // Trigger enter may not fire normally because the collider was disabled and re-enabled.
                playersInside.Add(darknessTracker);
                darknessTracker.EnterDarkness();
            }
        }
    }

    private bool IsActiveLightBeamOverlapping()
    {
        // The beam uses a rotated rectangular area rather than a simple circle,
        // so the overlap check is handled by the beam controller.
        LightBeamController[] beams = Object.FindObjectsByType<LightBeamController>(FindObjectsSortMode.None);

        foreach (LightBeamController beam in beams)
        {
            if (beam == null || !beam.IsBeamActive())
                continue;

            // If the active beam is still covering this darkness area, reform should wait.
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
        // This lets us test dispel/reform from the Inspector without needing to fire a light ability.
        // It is useful for checking whether the VFX and collider are both hiding/reappearing correctly.
        Dispel();
    }
}