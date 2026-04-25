using UnityEngine;
using System.Collections;

public class LightBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    [SerializeField] private float beamRange = 6f;
    [SerializeField] private float beamWidth = 1.5f;
    [SerializeField] private LayerMask darknessLayer;

    [Header("Beam Timing")]
    [SerializeField] private float beamVisualDuration = 0.2f;
    [SerializeField] private float beamCooldown = 2f;

    [Header("Beam Origin")]
    [SerializeField] private Transform beamOrigin;

    [Header("Beam Visual")]
    [SerializeField] private GameObject beamVisual;

    private bool isOnCooldown = false;
    private Coroutine visualCoroutine;
    private Coroutine cooldownCoroutine;

    public void FireBeam()
    {
        if (isOnCooldown)
            return;

        Vector2 direction = GetFacingDirection();

        Vector2 originPosition = beamOrigin != null
            ? beamOrigin.position
            : transform.position;

        Vector2 boxCenter = originPosition + direction * (beamRange * 0.5f);
        Vector2 boxSize = new Vector2(beamRange, beamWidth);

        float angle = 0f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            boxCenter,
            boxSize,
            angle,
            darknessLayer
        );

        foreach (Collider2D hit in hits)
        {
            DarknessZone darknessZone = hit.GetComponent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
            }
        }

        ShowBeamVisual(boxCenter, boxSize);

        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
        }

        cooldownCoroutine = StartCoroutine(CooldownRoutine());

        Debug.Log("Light beam fired. Darkness zones hit: " + hits.Length);
    }

    private void ShowBeamVisual(Vector2 boxCenter, Vector2 boxSize)
    {
        if (beamVisual == null)
            return;

        beamVisual.transform.position = boxCenter;
        beamVisual.transform.rotation = Quaternion.identity;
        beamVisual.transform.localScale = new Vector3(boxSize.x, boxSize.y, 1f);
        beamVisual.SetActive(true);

        if (visualCoroutine != null)
        {
            StopCoroutine(visualCoroutine);
        }

        visualCoroutine = StartCoroutine(HideBeamVisualRoutine());
    }

    private IEnumerator HideBeamVisualRoutine()
    {
        yield return new WaitForSeconds(beamVisualDuration);

        if (beamVisual != null)
        {
            beamVisual.SetActive(false);
        }

        visualCoroutine = null;
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;
        Debug.Log("Light beam cooldown started");

        yield return new WaitForSeconds(beamCooldown);

        isOnCooldown = false;
        cooldownCoroutine = null;
        Debug.Log("Light beam cooldown ended");
    }

    private Vector2 GetFacingDirection()
    {
        if (transform.localScale.x < 0f)
        {
            return Vector2.left;
        }

        return Vector2.right;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 direction = GetFacingDirection();

        Vector2 originPosition = beamOrigin != null
            ? beamOrigin.position
            : transform.position;

        Vector2 boxCenter = originPosition + direction * (beamRange * 0.5f);
        Vector2 boxSize = new Vector2(beamRange, beamWidth);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}