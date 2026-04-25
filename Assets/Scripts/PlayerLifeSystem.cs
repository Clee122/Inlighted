using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PlayerLifeSystem : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;
    private int currentLives;

    [Header("Damage Settings")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    private bool isInvulnerable = false;
    private bool isDead = false;

    [Header("Darkness Indicator settings")]
    public Image DarknessIndicator;
    private Color tempColor;
    public float AMult; //set to 1/3
    private int DarknessDamage;
    private float ResetAlpha = 0.001f;

    private void Start()
    {
        currentLives = maxLives;

        DarknessDamage = 0;
    }

    // =========================
    // DAMAGE FUNCTION (IMPORTANT)
    // =========================
    public void TakeDamage(int amount)
    {
        if (isDead || isInvulnerable)
            return;

        currentLives -= amount;
        DarknessDamage += amount;

        if (currentLives < 0)
            currentLives = 0;

        Debug.Log("Player took damage. Lives left: " + currentLives);

        if (currentLives <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityCoroutine());
        }

        tempColor = DarknessIndicator.color;
        tempColor.a = DarknessDamage*AMult;
        DarknessIndicator.color = tempColor;

    }

    // =========================
    // DEATH
    // =========================
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("Player died");

        PlayerRespawn playerRespawn = GetComponent<PlayerRespawn>();

        if (playerRespawn != null)
        {
            playerRespawn.RespawnPlayer();
        }
    }

    // =========================
    // INVULNERABILITY (PREVENT SPAM DAMAGE)
    // =========================
    private IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }

    // =========================
    // PUBLIC GETTERS (GOOD PRACTICE)
    // =========================
    public int GetCurrentLives()
    {
        return currentLives;
    }

    public int GetMaxLives()
    {
        return maxLives;
    }

    public bool IsDead()
    {
        return isDead;
    }

    // =========================
    // RESET (USED LATER FOR RESPAWN)
    // =========================
    public void RestoreFullLives()
    {
        currentLives = maxLives;
        isDead = false;
        isInvulnerable = false;
    }

    public void DarknessIndicatorReset()
    {
        Debug.Log("reached code for alpha change");
        tempColor = DarknessIndicator.color;
        tempColor.a = ResetAlpha;
        DarknessIndicator.color = tempColor;
    }
}
