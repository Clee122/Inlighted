using UnityEngine;
using System.Collections;

public class LightBurstController : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private float burstDuration = 2f;

    [Header("Burst Visual")]
    [SerializeField] private GameObject burstVisual;

    private bool isBurstActive = false;
    private Coroutine burstCoroutine;

    public bool IsBurstActive()
    {
        return isBurstActive;
    }

    public void ActivateBurst()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
        }

        burstCoroutine = StartCoroutine(BurstRoutine());
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
}