using UnityEngine;
using UnityEngine.InputSystem;

public class DeveloperTeleporter : MonoBehaviour
{
    [Header("Developer Teleporter")]
    // This tool exists only to speed up development and playtesting of the
    // large level. It should not be used as part of normal player progression.
    [SerializeField] private GameObject player;

    // Each Transform represents a section of the level developers may want
    // to jump to quickly. Their array order corresponds to number keys 1-9.
    [SerializeField] private Transform[] teleportPoints;

    [Header("Settings")]
    // Disabling this allows the developer tool to remain in the scene without
    // responding to shortcuts when a normal playthrough is being tested.
    [SerializeField] private bool enableDeveloperTeleport = true;

    private Rigidbody2D playerRigidbody;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError(
                "DeveloperTeleporter requires the Player to be assigned.",
                this
            );

            return;
        }

        // Keeping the Rigidbody reference lets us clear existing momentum after
        // teleporting so the player does not arrive while still falling or moving.
        playerRigidbody =
            player.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (
            !enableDeveloperTeleport ||
            player == null ||
            Keyboard.current == null
        )
        {
            return;
        }

        // Number keys provide quick access during Play Mode without requiring
        // any temporary UI or modifying the player's normal control scheme.
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            TeleportToPoint(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            TeleportToPoint(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            TeleportToPoint(2);
        }
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            TeleportToPoint(3);
        }
        else if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            TeleportToPoint(4);
        }
        else if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            TeleportToPoint(5);
        }
        else if (Keyboard.current.digit7Key.wasPressedThisFrame)
        {
            TeleportToPoint(6);
        }
        else if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            TeleportToPoint(7);
        }
        else if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            TeleportToPoint(8);
        }
    }

    private void TeleportToPoint(int pointIndex)
    {
        // Ignore shortcuts that do not have a corresponding destination so the
        // developer does not need to fill all nine slots before using the tool.
        if (
            teleportPoints == null ||
            pointIndex < 0 ||
            pointIndex >= teleportPoints.Length ||
            teleportPoints[pointIndex] == null
        )
        {
            return;
        }

        // Clearing velocity prevents existing movement, jumps, or falls from
        // carrying through the teleport and interfering with the section test.
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        player.transform.position =
            teleportPoints[pointIndex].position;
    }
}