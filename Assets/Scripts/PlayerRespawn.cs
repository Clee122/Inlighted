using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 1f;

    private PlayerLifeSystem playerLifeSystem;
    private Rigidbody2D rb;
    private PlayerController2D playerController;

    private void Awake()
    {
        playerLifeSystem = GetComponent<PlayerLifeSystem>();
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController2D>();
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
        }

        if (playerLifeSystem != null)
        {
            playerLifeSystem.RestoreFullLives();
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }
}