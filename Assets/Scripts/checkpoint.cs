using UnityEngine;

public class checkpoint : MonoBehaviour
{
    private PlayerRespawn respawn;
    private PlayerLightResource playerLightResource;

    private void Awake()
    {
        // The checkpoint stores references to the player's respawn and light systems
        // so reaching it can update the respawn position and fully restore light.
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError(
                "Checkpoint could not find a GameObject tagged Player. " +
                "Checkpoint activation will not work."
            );

            return;
        }

        respawn = player.GetComponent<PlayerRespawn>();
        playerLightResource = player.GetComponent<PlayerLightResource>();

        if (respawn == null)
        {
            Debug.LogError(
                "Checkpoint found the Player, but PlayerRespawn is missing."
            );
        }

        if (playerLightResource == null)
        {
            Debug.LogError(
                "Checkpoint found the Player, but PlayerLightResource is missing."
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Only the Player should activate the checkpoint. This prevents other
        // trigger objects, projectiles, or moving level objects from activating it.
        if (!collision.CompareTag("Player"))
        {
            return;
        }

        if (respawn != null)
        {
            // The checkpoint Transform becomes the player's new respawn position.
            respawn.SetCheckpoint(transform);

            Debug.Log(
                "Checkpoint activated. New respawn point: " +
                gameObject.name
            );
        }

        if (playerLightResource != null)
        {
            // Checkpoints restore light beyond the renewable 50% movement limit,
            // giving the player access to their full resource again.
            playerLightResource.RestoreFullLight(
                "Checkpoint " + gameObject.name
            );
        }
    }
}