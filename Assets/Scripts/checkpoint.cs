using UnityEngine;

public class checkpoint : MonoBehaviour
{
    [Header("Visuals")]
    // These separate child objects allow the checkpoint artwork to change state
    // without disabling the parent object that contains the trigger and gameplay logic.
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject unlockedVisual;

    // Only one checkpoint should visually appear active at a time.
    // Keeping a shared reference lets a newly activated checkpoint tell the previous
    // checkpoint to return to its locked appearance.
    private static checkpoint activeCheckpoint;

    [Header("Audio")]
    // Checkpoint audio belongs to the checkpoint itself so different checkpoint
    // prefabs can later use different sounds without changing player systems.
    [SerializeField] private AudioClip checkpointSound;

    private PlayerRespawn respawn;
    private PlayerLightResource playerLightResource;

    private void Awake()
    {
        // The old greybox SpriteRenderer is no longer needed visually, but the
        // parent GameObject must remain active because it contains the checkpoint
        // collider and gameplay logic.
        SpriteRenderer greyboxRenderer = GetComponent<SpriteRenderer>();

        if (greyboxRenderer != null)
        {
            greyboxRenderer.enabled = false;
        }

        // Checkpoints begin visually locked. The active checkpoint will switch to
        // the unlocked artwork when the player reaches it.
        SetVisualState(false);

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

        // When the player reaches a different checkpoint, the previous checkpoint
        // returns to its locked appearance before this one becomes the active shrine.
        if (activeCheckpoint != this)
        {
            if (activeCheckpoint != null)
            {
                activeCheckpoint.SetVisualState(false);
            }

            activeCheckpoint = this;
            SetVisualState(true);
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

        // The checkpoint sound plays only after the Player has legitimately
        // entered the checkpoint trigger and its gameplay effects have run.
        if (
            checkpointSound != null &&
            AudioManager.Instance != null
        )
        {
            AudioManager.Instance.PlaySFX(
                checkpointSound
            );
        }
    }

    private void SetVisualState(bool isUnlocked)
    {
        // Both visual children stay under the same checkpoint so swapping states
        // does not affect its collider, respawn position, audio, or other components.
        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!isUnlocked);
        }

        if (unlockedVisual != null)
        {
            unlockedVisual.SetActive(isUnlocked);
        }
    }
}