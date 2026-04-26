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
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        lightBurstController = GetComponent<LightBurstController>();
    }

    public void EnterDarkness()
    {
        darknessZoneCount++;

        if (damageCoroutine == null)
        {
            damageCoroutine = StartCoroutine(DarknessDamageRoutine());
        }
    }

    public void ExitDarkness()
    {
        darknessZoneCount--;

        if (darknessZoneCount < 0)
        {
            darknessZoneCount = 0;
        }

        if (darknessZoneCount == 0 && damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }
    }

    private IEnumerator DarknessDamageRoutine()
    {
        while (darknessZoneCount > 0 && playerLifeSystem != null)
        {
            if (playerLifeSystem.IsDead())
            {
                darknessZoneCount = 0;
                damageCoroutine = null;
                yield break;
            }

            bool burstActive = lightBurstController != null && lightBurstController.IsBurstActive();

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