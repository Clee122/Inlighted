using UnityEngine;
using System.Collections;

public class DarknessZone : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float damageInterval = 1f;

    private Coroutine damageCoroutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerLifeSystem playerLife = other.GetComponent<PlayerLifeSystem>();

        if (playerLife != null)
        {
            damageCoroutine = StartCoroutine(DamageOverTime(other.gameObject, playerLife));
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerLifeSystem playerLife = other.GetComponent<PlayerLifeSystem>();

        if (playerLife != null && damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }
    }

    private IEnumerator DamageOverTime(GameObject playerObject, PlayerLifeSystem playerLife)
    {
        LightBurstController burstController = playerObject.GetComponent<LightBurstController>();

        while (playerLife != null && !playerLife.IsDead())
        {
            bool burstActive = burstController != null && burstController.IsBurstActive();

            if (!burstActive)
            {
                playerLife.TakeDamage(damageAmount);
            }

            yield return new WaitForSeconds(damageInterval);
        }
    }
}