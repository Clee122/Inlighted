using UnityEngine;
using System.Collections;

public class LightBurstController : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private float burstDuration = 2f;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    private bool isBurstActive = false;
    private bool isOnCooldown = false;
    private Coroutine burstCoroutine;
    private Coroutine cooldownCoroutine;

    public bool IsBurstActive()
    {
        return isBurstActive;
    }

    public bool IsOnCooldown()
    {
        return isOnCooldown;
    }

    public void ActivateBurst()
    {
        if (isBurstActive || isOnCooldown)
            return;

        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
        }

        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
        }

        burstCoroutine = StartCoroutine(BurstRoutine());
        cooldownCoroutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        isBurstActive = true;

        if (burstVisual != null)
        {
            burstVisual.SetActive(true);
        }

        Debug.Log("Light burst active");

        yield return new WaitForSeconds(burstDuration);

        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        burstCoroutine = null;
        Debug.Log("Light burst ended");
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;
        Debug.Log("Light burst cooldown started");

        yield return new WaitForSeconds(burstDuration);

        isOnCooldown = false;
        cooldownCoroutine = null;
        Debug.Log("Light burst cooldown ended");
    }
}