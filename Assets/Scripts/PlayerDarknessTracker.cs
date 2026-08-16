using UnityEngine;
using System.Collections;

public class PlayerDarknessTracker : MonoBehaviour
{
    [Header("Darkness Damage")]
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float damageInterval = 1f;

    private int darknessZoneCount = 0;
    private Coroutine damageCoroutine;

    private PlayerLifeSystem playerLifeSystem;
    private LightBurstController lightBurstController;
    private PlayerDash playerDash;

    private void Awake()
    {
        // These references are stored once because this script constantly checks
        // player health and temporary protection while the player is inside darkness.
        playerLifeSystem =
            GetComponent<PlayerLifeSystem>();

        lightBurstController =
            GetComponent<LightBurstController>();

        // Dash protection is checked without removing the player from DarknessZone
        // tracking. This allows darkness to become dangerous immediately if the
        // dash ends before the player has successfully crossed the zone.
        playerDash =
            GetComponent<PlayerDash>();
    }

    public void EnterDarkness()
    {
        // A counter is used instead of a simple bool because the player can overlap multiple darkness zones.
        // This prevents stacked darkness pieces from multiplying the damage unfairly.
        darknessZoneCount++;

        if (damageCoroutine == null)
        {
            damageCoroutine =
                StartCoroutine(
                    DarknessDamageRoutine()
                );
        }
    }

    public void ExitDarkness()
    {
        darknessZoneCount--;

        // This keeps the counter safe if trigger exit events happen in an unexpected order.
        if (darknessZoneCount < 0)
        {
            darknessZoneCount = 0;
        }

        // Damage stops only when the player has exited all darkness zones, not just one of them.
        if (
            darknessZoneCount == 0 &&
            damageCoroutine != null
        )
        {
            StopCoroutine(
                damageCoroutine
            );

            damageCoroutine = null;
        }
    }

    private IEnumerator DarknessDamageRoutine()
    {
        // This loop centralises darkness damage on the player.
        // Darkness zones only report enter/exit, while this script controls the
        // actual damage timing and temporary protection from light or dash.
        while (
            darknessZoneCount > 0 &&
            playerLifeSystem != null
        )
        {
            // If the player dies while inside darkness, the tracker clears its
            // state so old darkness damage cannot continue after respawn.
            if (playerLifeSystem.IsDead())
            {
                darknessZoneCount = 0;
                damageCoroutine = null;

                yield break;
            }

            bool dashActive =
                playerDash != null &&
                playerDash.IsDashing();

            if (dashActive)
            {
                // Dash protection must exist only for the actual dash duration.
                // Checking again next frame avoids accidentally giving the player
                // the full darkness damage interval as extra protection after dash ends.
                yield return null;
                continue;
            }

            bool burstActive =
                lightBurstController != null &&
                lightBurstController.IsBurstActive();

            if (burstActive)
            {
                // Light Burst intentionally protects the player for its active
                // duration. Checking regularly allows darkness to become dangerous
                // again shortly after Burst protection has actually finished.
                yield return null;
                continue;
            }

            // Once neither Dash nor Light Burst is protecting the player,
            // darkness damage should happen immediately while they remain inside.
            playerLifeSystem.TakeDamage(
                damageAmount
            );

            if (playerLifeSystem.IsDead())
            {
                darknessZoneCount = 0;
                damageCoroutine = null;

                yield break;
            }

            // The normal damage interval applies only after actual darkness
            // damage occurs. Temporary dash protection should never consume
            // this timer or create extra protection after movement ends.
            yield return new WaitForSeconds(
                damageInterval
            );
        }

        damageCoroutine = null;
    }
}