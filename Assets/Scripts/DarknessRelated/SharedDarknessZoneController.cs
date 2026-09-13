using UnityEngine;

public class SharedDarknessZoneController : MonoBehaviour
{
    [Header("Darkness Zone Colliders")]

    /*
     * Several trigger colliders can belong to one visual darkness mass. This
     * lets the level use simple gameplay shapes while the player sees one
     * continuous darkness region instead of separate visual blocks.
     */
    [SerializeField]
    private Collider2D[] darknessZones;

    [Header("Shared Darkness Cutout")]

    /*
     * Every gameplay zone checks the same cut-out controller so Light Burst and
     * Light Beam create safe areas consistently across the entire darkness mass.
     */
    [SerializeField]
    private DarknessCutoutController darknessCutoutController;

    [Header("Player")]

    /*
     * Damage is sent through the existing PlayerLifeSystem so its normal Hurt,
     * invulnerability, HUD, audio, death and respawn behaviour remain authoritative.
     */
    [SerializeField]
    private PlayerLifeSystem playerLifeSystem;

    [SerializeField]
    private Transform playerTransform;

    [Header("Damage Settings")]

    /*
     * The controller checks damage at a short interval instead of every frame.
     * PlayerLifeSystem still owns the actual invulnerability protection, while
     * this interval avoids repeatedly calling TakeDamage while protection is active.
     */
    [SerializeField]
    private float damageCheckInterval = 0.1f;

    private float damageCheckTimer = 0f;

    private void Awake()
    {
        /*
         * The PlayerLifeSystem and player Transform normally live on the same
         * Player object, so missing Transform setup can safely be recovered here.
         */
        if (
            playerTransform == null &&
            playerLifeSystem != null
        )
        {
            playerTransform =
                playerLifeSystem.transform;
        }
    }

    private void Update()
    {
        if (
            playerLifeSystem == null ||
            playerTransform == null ||
            darknessCutoutController == null
        )
        {
            return;
        }

        /*
         * Leaving all of the darkness colliders resets the local check timer so
         * re-entering darkness does not inherit a partially completed interval.
         */
        if (!IsPlayerInsideAnyDarknessZone())
        {
            damageCheckTimer = 0f;
            return;
        }

        /*
         * Light Burst and Light Beam use the same cut-out data for visuals and
         * gameplay safety. A player inside one of those openings should therefore
         * never receive darkness damage even though they remain inside a collider.
         */
        if (
            darknessCutoutController.IsPositionInsideLightCutout(
                playerTransform.position
            )
        )
        {
            damageCheckTimer = 0f;
            return;
        }

        damageCheckTimer +=
            Time.deltaTime;

        if (
            damageCheckTimer <
            damageCheckInterval
        )
        {
            return;
        }

        damageCheckTimer = 0f;

        /*
         * Darkness removes one life at a time. PlayerLifeSystem decides whether
         * this request is accepted based on death and Hurt invulnerability state.
         */
        playerLifeSystem.TakeDamage(
            1
        );
    }

    private bool IsPlayerInsideAnyDarknessZone()
    {
        if (
            darknessZones == null ||
            playerTransform == null
        )
        {
            return false;
        }

        Vector2 playerPosition =
            playerTransform.position;

        foreach (
            Collider2D darknessZone
            in darknessZones
        )
        {
            if (
                darknessZone == null ||
                !darknessZone.enabled
            )
            {
                continue;
            }

            /*
             * OverlapPoint lets separate rectangular colliders behave as one
             * darkness region without physically merging their collider shapes.
             */
            if (
                darknessZone.OverlapPoint(
                    playerPosition
                )
            )
            {
                return true;
            }
        }

        return false;
    }
}
