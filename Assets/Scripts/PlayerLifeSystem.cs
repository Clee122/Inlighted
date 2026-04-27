using UnityEngine;
using System.Collections;

public class PlayerLifeSystem : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;
    private int currentLives;

    [Header("Damage Settings")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    private bool isInvulnerable = false;
    private bool isDead = false;

    private void Start()
    {
        currentLives = maxLives;
    }

    // =========================
    // DAMAGE FUNCTION (IMPORTANT)
    // =========================
    public void TakeDamage(int amount)
    {
        if (isDead || isInvulnerable)
            return;

        currentLives -= amount;

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

    void OnCollisionEnter2D(Collision2D collision) 
    { if (collision.gameObject.tag == "dead pit") 
        { 
        Die(); 
        } 
    }
}
