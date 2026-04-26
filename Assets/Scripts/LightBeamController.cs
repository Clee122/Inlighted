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
    [SerializeField] private float beamCheckInterval = 0.05f;

    [Header("Beam Origin")]
    [SerializeField] private Transform beamOrigin;

    [Header("Beam Visual")]
    [SerializeField] private GameObject beamVisual;

    private bool isBeamActive = false;
    private bool isOnCooldown = false;

    private Coroutine beamCoroutine;
    private Coroutine cooldownCoroutine;

    private Vector2 lastBeamCenter;
    private Vector2 lastBeamSize;

    private int facingDirection = 1;

    public bool IsBeamActive()
    {
        return isBeamActive;
    }

    public void FireBeam()
    {
        if (isOnCooldown || isBeamActive)
            return;

        if (beamCoroutine != null)
        {
            StopCoroutine(beamCoroutine);
        }

        beamCoroutine = StartCoroutine(BeamRoutine());

        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
        }

        cooldownCoroutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator BeamRoutine()
    {
        isBeamActive = true;

        if (beamVisual != null)
        {
            beamVisual.SetActive(true);
        }

        Debug.Log("Light beam active");

        float timer = 0f;

        while (timer < beamVisualDuration)
        {
            UpdateBeamPosition();
            DispelDarknessInBeam();

            timer += beamCheckInterval;
            yield return new WaitForSeconds(beamCheckInterval);
        }

        isBeamActive = false;

        if (beamVisual != null)
        {
            beamVisual.SetActive(false);
        }

        beamCoroutine = null;

        Debug.Log("Light beam ended");
    }

    private void UpdateBeamPosition()
    {
        Vector2 direction = GetFacingDirection();

        Vector2 originPosition = beamOrigin != null
            ? beamOrigin.position
            : transform.position;

        Vector2 boxCenter = originPosition + direction * (beamRange * 0.5f);
        Vector2 boxSize = new Vector2(beamRange, beamWidth);

        lastBeamCenter = boxCenter;
        lastBeamSize = boxSize;

        if (beamVisual != null)
        {
            beamVisual.transform.position = boxCenter;
            beamVisual.transform.rotation = Quaternion.identity;
            beamVisual.transform.localScale = new Vector3(boxSize.x, boxSize.y, 1f);
        }
    }

    private void DispelDarknessInBeam()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            lastBeamCenter,
            lastBeamSize,
            0f,
            darknessLayer
        );

        foreach (Collider2D hit in hits)
        {
            DarknessZone darknessZone = hit.GetComponentInParent<DarknessZone>();

            if (darknessZone != null)
            {
                darknessZone.Dispel();
            }
        }
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

    public bool IsBoundsOverlappingActiveBeam(Bounds darknessBounds)
    {
        if (!isBeamActive)
            return false;

        Bounds beamBounds = new Bounds(lastBeamCenter, lastBeamSize);

        return beamBounds.Intersects(darknessBounds);
    }

    private Vector2 GetFacingDirection()
    {
        return facingDirection < 0 ? Vector2.left : Vector2.right;
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

    public void OnMoveForBeam(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        if (input.x > 0.01f)
        {
            facingDirection = 1;
        }
        else if (input.x < -0.01f)
        {
            facingDirection = -1;
        }
    }
}