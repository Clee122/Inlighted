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
            damageCoroutine = StartCoroutine(DamageOverTime(playerLife));
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

    private IEnumerator DamageOverTime(PlayerLifeSystem playerLife)
    {
        while (playerLife != null && !playerLife.IsDead())
        {
            playerLife.TakeDamage(damageAmount);
            yield return new WaitForSeconds(damageInterval);
        }
    }
}
