using System.Collections.Generic;
using UnityEngine;

public class DarknessSafeAreaTest : MonoBehaviour
{
    [Header("Safe Area Controller")]
    [SerializeField]
    private DarknessSafeAreaExperimentController experimentController;

    [Header("Damage Settings")]
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float damageInterval = 1f;

    // A set is used because the Player may have more than one collider.
    // Without tracking them separately, one child collider leaving the darkness
    // could incorrectly mark the entire Player as having left the zone.
    private readonly HashSet<Collider2D> playerCollidersInside =
        new HashSet<Collider2D>();

    private PlayerLifeSystem playerLifeSystem;
    private Transform playerTransform;

    private float damageTimer = 0f;

    private void Update()
    {
        if (
            playerCollidersInside.Count == 0 ||
            playerLifeSystem == null ||
            playerTransform == null ||
            experimentController == null
        )
        {
            return;
        }

        bool playerIsProtectedByLight =
            experimentController.IsPositionSafe(
                playerTransform.position
            );

        if (playerIsProtectedByLight)
        {
            // Clearing the timer while protected prevents an old accumulated
            // damage tick from firing immediately when the safe area disappears.
            damageTimer = 0f;

            return;
        }

        damageTimer += Time.deltaTime;

        if (damageTimer < damageInterval)
        {
            return;
        }

        damageTimer = 0f;

        // The experiment delegates actual health handling to PlayerLifeSystem
        // so existing invulnerability, hurt animation, death, audio and HUD
        // behaviour remain consistent with the rest of the game.
        playerLifeSystem.TakeDamage(
            damageAmount
        );
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        PlayerLifeSystem lifeSystem =
            other.GetComponentInParent<PlayerLifeSystem>();

        if (lifeSystem == null)
        {
            return;
        }

        playerCollidersInside.Add(
            other
        );

        playerLifeSystem =
            lifeSystem;

        playerTransform =
            lifeSystem.transform;

        // Starting from zero gives the player the intended full damage interval
        // after first entering darkness rather than taking damage immediately.
        damageTimer = 0f;
    }

    private void OnTriggerExit2D(
        Collider2D other
    )
    {
        if (!playerCollidersInside.Contains(other))
        {
            return;
        }

        playerCollidersInside.Remove(
            other
        );

        // Only clear the Player reference after every Player collider has left.
        // This avoids incorrect exits when the Player uses multiple colliders.
        if (playerCollidersInside.Count > 0)
        {
            return;
        }

        playerLifeSystem = null;
        playerTransform = null;
        damageTimer = 0f;
    }
}