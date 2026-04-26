using UnityEngine;
using System.Collections;

public class LightBurstController : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private float burstDuration = 2f;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    [Header("Darkness Dispel")]
    [SerializeField] private float burstDispelRadius = 3f;
    [SerializeField] private LayerMask darknessLayer;

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

        float timer = 0f;
        float dispelCheckInterval = 0.1f;

        while (timer < burstDuration)
        {
            DispelDarknessInRadius();

            timer += dispelCheckInterval;
            yield return new WaitForSeconds(dispelCheckInterval);
        }

        isBurstActive = false;

        if (burstVisual != null)
        {
            burstVisual.SetActive(false);
        }

        burstCoroutine = null;
        Debug.Log("Light burst ended");
    }

    private void DispelDarknessInRadius()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            burstDispelRadius,
            darknessLayer
        );

        foreach (Collider2D hit in hits)
        {
            DarknessZone darknessZone = hit.GetComponent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
            }
        }

        Debug.Log("Light burst dispelled darkness zones: " + hits.Length);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, burstDispelRadius);
    }

    public float GetBurstDispelRadius()
    {
        return burstDispelRadius;
    }

}