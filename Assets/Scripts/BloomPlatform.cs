using System.Collections;
using UnityEngine;

public class BloomPlatform : MonoBehaviour
{
    [Header("References")]

    // The Animator controls the visual state of the flower.
    // Keeping animation logic separate from platform collision makes it easier
    // to tune the visuals without affecting how the player stands on the platform.
    [SerializeField] private Animator animator;

    // This collider represents the actual walkable surface.
    // It stays disabled while the flower is closed so the player cannot stand on it.
    [SerializeField] private Collider2D platformCollider;

    [Header("Bloom Timing")]

    // This is how long the flower remains fully usable after it has opened.
    // A generous default is useful for the introductory Light Beam section.
    [SerializeField] private float bloomDuration = 7f;

    // The collider should not become solid immediately when the Bloom trigger is sent.
    // This delay gives the opening animation time to visually create the platform first.
    [SerializeField] private float colliderEnableDelay = 0.5f;

    // The collider remains active briefly after the closing animation starts.
    // This prevents the platform from disappearing under the player the instant
    // the Unbloom trigger is sent.
    [SerializeField] private float colliderDisableDelay = 0.35f;

    [Header("Animator Parameters")]

    // These names must match the Trigger parameters created in the Animator.
    [SerializeField] private string bloomTriggerName = "Bloom";
    [SerializeField] private string unbloomTriggerName = "Unbloom";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Coroutine bloomRoutine;
    private bool isBloomedOrBlooming;

    private void Awake()
    {
        // These fallbacks support a simple setup where the Animator or Collider
        // remain on the same object. Child references should still be assigned
        // manually when the visuals and collision are separated.
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }

        // The flower always begins non-solid because the default visual state
        // is closed and should not provide a platform until it has bloomed.
        if (platformCollider != null)
        {
            platformCollider.enabled = false;
        }
        else
        {
            Debug.LogError(
                "BloomPlatform requires a Collider2D to act as the walkable platform.",
                this
            );
        }

        if (animator == null)
        {
            Debug.LogError(
                "BloomPlatform requires an Animator for the flower animations.",
                this
            );
        }

        // Prevent invalid negative timing values from creating unexpected
        // coroutine behaviour during playtesting.
        bloomDuration =
            Mathf.Max(
                0f,
                bloomDuration
            );

        colliderEnableDelay =
            Mathf.Max(
                0f,
                colliderEnableDelay
            );

        colliderDisableDelay =
            Mathf.Max(
                0f,
                colliderDisableDelay
            );
    }

    public void ActivateBloom()
    {
        // A Beam shot should not refresh the timer of an already active flower.
        // This keeps the traversal window predictable and prevents repeated
        // Beam shots from maintaining a platform indefinitely.
        if (isBloomedOrBlooming)
        {
            return;
        }

        if (
            animator == null ||
            platformCollider == null
        )
        {
            return;
        }

        isBloomedOrBlooming = true;

        if (bloomRoutine != null)
        {
            StopCoroutine(
                bloomRoutine
            );
        }

        bloomRoutine =
            StartCoroutine(
                BloomRoutine()
            );

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " began blooming."
            );
        }
    }

    public bool IsBloomedOrBlooming()
    {
        // Bloom Receivers use this state to determine whether another Beam hit
        // should activate this flower. Active flowers deliberately ignore it.
        return isBloomedOrBlooming;
    }

    private IEnumerator BloomRoutine()
    {
        // Clearing the opposite trigger prevents a previous animation request
        // from interfering with a new bloom cycle.
        animator.ResetTrigger(
            unbloomTriggerName
        );

        animator.SetTrigger(
            bloomTriggerName
        );

        // The collider appears only after the flower has visually opened enough
        // to reasonably communicate that the player can stand on it.
        if (colliderEnableDelay > 0f)
        {
            yield return new WaitForSeconds(
                colliderEnableDelay
            );
        }

        platformCollider.enabled = true;

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " is now solid."
            );
        }

        // The flower remains available for its complete traversal window before
        // beginning the closing animation.
        if (bloomDuration > 0f)
        {
            yield return new WaitForSeconds(
                bloomDuration
            );
        }

        animator.ResetTrigger(
            bloomTriggerName
        );

        animator.SetTrigger(
            unbloomTriggerName
        );

        // Collision remains briefly during the beginning of the closing
        // animation so the visual provides warning before the platform vanishes.
        if (colliderDisableDelay > 0f)
        {
            yield return new WaitForSeconds(
                colliderDisableDelay
            );
        }

        platformCollider.enabled = false;

        isBloomedOrBlooming = false;
        bloomRoutine = null;

        if (showDebugLogs)
        {
            Debug.Log(
                gameObject.name +
                " finished unblooming and is no longer solid."
            );
        }
    }

    [ContextMenu("Test Bloom")]
    private void TestBloom()
    {
        // This Inspector command allows the complete flower behaviour to be
        // tested without requiring the Light Beam Receiver to be connected.
        ActivateBloom();
    }

    private void OnDisable()
    {
        // Resetting the temporary state prevents the platform from remaining
        // solid if it is disabled during an active bloom cycle.
        if (bloomRoutine != null)
        {
            StopCoroutine(
                bloomRoutine
            );

            bloomRoutine = null;
        }

        isBloomedOrBlooming = false;

        if (platformCollider != null)
        {
            platformCollider.enabled = false;
        }
    }
}