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

    private void Awake()
    {
        // These references are stored once because this script constantly checks player health
        // and burst protection while the player is inside darkness.
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        lightBurstController = GetComponent<LightBurstController>();
    }

    public void EnterDarkness()
    {
        // A counter is used instead of a simple bool because the player can overlap multiple darkness zones.
        // This prevents stacked darkness pieces from multiplying the damage unfairly.
        darknessZoneCount++;

        if (damageCoroutine == null)
        {
            damageCoroutine = StartCoroutine(DarknessDamageRoutine());
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
        if (darknessZoneCount == 0 && damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }
    }

    private IEnumerator DarknessDamageRoutine()
    {
        // This loop centralises darkness damage on the player.
        // The darkness zones only report enter/exit, while this script controls the actual damage timing.
        while (darknessZoneCount > 0 && playerLifeSystem != null)
        {
            // If the player dies while inside darkness, the tracker clears its state so old darkness damage
            // does not continue after respawn.
            if (playerLifeSystem.IsDead())
            {
                darknessZoneCount = 0;
                damageCoroutine = null;
                yield break;
            }

            bool burstActive = lightBurstController != null && lightBurstController.IsBurstActive();

            // Light Burst protects the player from darkness while active.
            // This makes the burst function as a survival tool, not just a visual effect.
            if (!burstActive)
            {
                playerLifeSystem.TakeDamage(damageAmount);

                if (playerLifeSystem.IsDead())
                {
                    darknessZoneCount = 0;
                    damageCoroutine = null;
                    yield break;
                }
            }

            yield return new WaitForSeconds(damageInterval);
        }

        damageCoroutine = null;
    }
}