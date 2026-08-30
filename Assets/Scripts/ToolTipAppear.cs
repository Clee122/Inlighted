using UnityEngine;

public class ToolTipAppear : MonoBehaviour
{
    public GameObject ChosenTip;

    [Header("Tooltip Position")]
    // This is a local offset from the sign rather than a world position,
    // so every sign can use simple values such as X = 0 and Y = 2.
    public Vector3 locationOffset = new Vector3(0f, 2f, 0f);

    private GameObject ToolTip;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only the player should cause tutorial information to appear.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        SpawnToolTip();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // The tooltip should remain visible until the player actually
        // leaves this sign's tutorial area.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        KillToolTip();
    }

    private void Update()
    {
        // Keeping the spawned tooltip tied to this offset means the position
        // can be adjusted during Play Mode and immediately previewed.
        // It also keeps the tooltip correctly positioned if the sign moves.
        if (ToolTip != null)
        {
            ToolTip.transform.position =
                transform.position + locationOffset;
        }
    }

    private void SpawnToolTip()
    {
        // Prevent duplicate copies if the trigger is entered more than once
        // before the existing tooltip has been removed.
        if (ToolTip != null || ChosenTip == null)
        {
            return;
        }

        ToolTip = Instantiate(
            ChosenTip,
            transform.position + locationOffset,
            transform.rotation
        );
    }

    private void KillToolTip()
    {
        if (ToolTip == null)
        {
            return;
        }

        Destroy(ToolTip);
        ToolTip = null;
    }
}