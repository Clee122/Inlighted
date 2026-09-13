using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

public class appear_and_disappeear_by_burst : MonoBehaviour
{
    [Header("Platform Collider")]
    [SerializeField]
    private Collider2D platformCollider;

    [Header("Platform Active Duration")]

    /*
     * The platform needs to remain usable after the short Burst visual has
     * disappeared so the player has time to recognise it and complete a jump.
     * FormerlySerializedAs preserves the value from the old "disable" field.
     */
    [FormerlySerializedAs("disable")]
    [SerializeField]
    private float activeDuration = 3f;

    private Coroutine disableCoroutine;

    private SpriteRenderer[] platformSprites;

    private void Awake()
    {
        if (platformCollider == null)
        {
            platformCollider =
                GetComponent<Collider2D>();
        }

        // Burst platforms begin non-solid because they should only become
        // traversable after Light Burst successfully reaches them.
        EnableCollider(false);

        platformSprites =
            GetComponentsInChildren<SpriteRenderer>(
                true
            );

        foreach (SpriteRenderer sprite in platformSprites)
        {
            if (sprite == null)
            {
                continue;
            }

            sprite.enabled = true;

            /*
             * While inactive, the platform is only visible through the Burst
             * reveal mask. This preserves the hidden-platform discovery effect.
             */
            sprite.maskInteraction =
                SpriteMaskInteraction.VisibleInsideMask;
        }
    }

    public void ActivatePlatform()
    {
        EnableCollider(true);

        /*
         * Once Burst has successfully revealed the platform, temporarily remove
         * its dependency on the Sprite Mask. This lets it remain visible after
         * the short Burst visual and reveal mask have disappeared.
         */
        SetPlatformPersistentlyVisible(true);

        if (disableCoroutine != null)
        {
            /*
             * Repeated Burst hits restart the available-time window rather than
             * allowing an older timer to hide the platform during a new cast.
             */
            StopCoroutine(
                disableCoroutine
            );
        }

        disableCoroutine =
            StartCoroutine(
                TurnOffPlatform()
            );
    }

    private IEnumerator TurnOffPlatform()
    {
        yield return new WaitForSeconds(
            activeDuration
        );

        EnableCollider(false);

        /*
         * Returning to VisibleInsideMask hides the platform again while still
         * allowing a future Burst to reveal it before activation.
         */
        SetPlatformPersistentlyVisible(false);

        disableCoroutine = null;
    }

    private void EnableCollider(
        bool enable
    )
    {
        if (platformCollider != null)
        {
            platformCollider.enabled =
                enable;
        }
    }

    private void SetPlatformPersistentlyVisible(
        bool visible
    )
    {
        if (platformSprites == null)
        {
            return;
        }

        foreach (SpriteRenderer sprite in platformSprites)
        {
            if (sprite == null)
            {
                continue;
            }

            /*
             * None makes the activated platform independent from the reveal mask.
             * VisibleInsideMask restores its hidden state when the timer expires.
             */
            sprite.maskInteraction =
                visible
                    ? SpriteMaskInteraction.None
                    : SpriteMaskInteraction.VisibleInsideMask;
        }
    }
}