using UnityEngine;
using UnityEngine;
using System.Collections;

public class LightBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    [SerializeField] private float beamRange = 6f;
    [SerializeField] private float beamWidth = 1.5f;
    [SerializeField] private LayerMask darknessLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("Beam Timing")]
    [SerializeField] private float beamActiveDuration = 0.5f;
    [SerializeField] private float beamCooldown = 2f;
    [SerializeField] private float beamCheckInterval = 0.05f;

    [Header("Beam Origin")]
    [SerializeField] private Transform beamOrigin;

    [Header("Beam Visuals")]
    [SerializeField] private GameObject beamIndicatorVisual;
    [SerializeField] private GameObject beamVisual;

     private bool isAiming = false;
    private bool isBeamActive = false;
    private bool isOnCooldown = false;

    private Coroutine beamCoroutine;
    private Coroutine cooldownCoroutine;

    private Vector2 lastBeamCenter;
    private Vector2 lastBeamSize;
    private Vector2 lastBeamDirection = Vector2.right;
    private float lastBeamAngle = 0f;

    private Camera mainCamera;
    private PlayerAbilityUnlocks abilityUnlocks;

    private void Awake()
    {
        mainCamera = Camera.main;
        abilityUnlocks = GetComponent<PlayerAbilityUnlocks>();

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.transform.SetParent(null, true);
            beamIndicatorVisual.transform.localScale = Vector3.one;
            beamIndicatorVisual.SetActive(false);
        }

        if (beamVisual != null)
        {
            beamVisual.transform.SetParent(null, true);
            beamVisual.transform.localScale = Vector3.one;
            beamVisual.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isAiming)
            return;

        UpdateBeamPreview(beamIndicatorVisual);

        if (UnityEngine.InputSystem.Mouse.current == null)
            return;

        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            ConfirmFireBeam();
        }

        if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelBeamAim();
        }
    }

    public bool IsBeamActive()
    {
        return isBeamActive;
    }

    // Called by E input.
    public void FireBeam()
    {
        BeginBeamAim();
    }

    public void BeginBeamAim()
    {
        if (abilityUnlocks != null && !abilityUnlocks.HasLightBeam())
        {
            Debug.Log("Light Beam is locked");
            return;
        }

        if (isOnCooldown || isBeamActive)
            return;

        isAiming = true;

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.SetActive(true);
        }

        if (beamVisual != null)
        {
            beamVisual.SetActive(false);
        }

        UpdateBeamPreview(beamIndicatorVisual);

        Debug.Log("Light beam aiming started");
    }

    private void CancelBeamAim()
    {
        isAiming = false;

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.SetActive(false);
        }

        Debug.Log("Light beam aiming cancelled");
    }

    private void ConfirmFireBeam()
    {
        if (!isAiming || isOnCooldown || isBeamActive)
            return;

        isAiming = false;

        if (beamIndicatorVisual != null)
        {
            beamIndicatorVisual.SetActive(false);
        }

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

        Debug.Log("Light beam fired");
    }

    private IEnumerator BeamRoutine()
    {
        isBeamActive = true;

        if (beamVisual != null)
        {
            beamVisual.SetActive(true);
        }

        float timer = 0f;

        while (timer < beamActiveDuration)
        {
            UpdateBeamPreview(beamVisual);
            DispelDarknessInBeam();
            CheckLightGateInBeam();


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

    private void UpdateBeamPreview(GameObject visualObject)
    {
        Vector2 originPosition = beamOrigin != null
            ? beamOrigin.position
            : transform.position;

        Vector2 direction = GetMouseAimDirection(originPosition);

        float actualRange = GetBeamRangeBeforeWall(originPosition, direction);

        Vector2 boxCenter = originPosition + direction * (actualRange * 0.5f);
        Vector2 boxSize = new Vector2(actualRange, beamWidth);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        lastBeamCenter = boxCenter;
        lastBeamSize = boxSize;
        lastBeamDirection = direction;
        lastBeamAngle = angle;

        if (visualObject != null)
        {
            visualObject.transform.position = originPosition;
            visualObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            visualObject.transform.localScale = new Vector3(actualRange, beamWidth, 1f);
            visualObject.transform.Translate(Vector3.right * (actualRange * 0.5f), Space.Self);
        }
    }

    private Vector2 GetMouseAimDirection(Vector2 originPosition)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || UnityEngine.InputSystem.Mouse.current == null)
        {
            return lastBeamDirection;
        }

        Vector2 mouseScreenPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;

        Vector2 direction = ((Vector2)mouseWorldPosition - originPosition).normalized;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return lastBeamDirection;
        }

        return direction;
    }

    private float GetBeamRangeBeforeWall(Vector2 originPosition, Vector2 direction)
    {
        RaycastHit2D wallHit = Physics2D.Raycast(
            originPosition,
            direction,
            beamRange,
            wallLayer
        );

        if (wallHit.collider != null)
        {
            return wallHit.distance;
        }

        return beamRange;
    }

    private void DispelDarknessInBeam()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            lastBeamCenter,
            lastBeamSize,
            lastBeamAngle,
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

        Debug.Log("Light beam dispelled darkness zones: " + hits.Length);
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

        Vector2 beamRight = lastBeamDirection.normalized;
        Vector2 beamUp = new Vector2(-beamRight.y, beamRight.x);

        Vector2 difference = (Vector2)darknessBounds.center - lastBeamCenter;

        float distanceAlongBeam = Mathf.Abs(Vector2.Dot(difference, beamRight));
        float distanceAcrossBeam = Mathf.Abs(Vector2.Dot(difference, beamUp));

        Vector2 boundsExtents = darknessBounds.extents;

        float darknessProjectionAlongBeam =
            Mathf.Abs(Vector2.Dot(Vector2.right * boundsExtents.x, beamRight)) +
            Mathf.Abs(Vector2.Dot(Vector2.up * boundsExtents.y, beamRight));

        float darknessProjectionAcrossBeam =
            Mathf.Abs(Vector2.Dot(Vector2.right * boundsExtents.x, beamUp)) +
            Mathf.Abs(Vector2.Dot(Vector2.up * boundsExtents.y, beamUp));

        float beamHalfLength = lastBeamSize.x * 0.5f;
        float beamHalfWidth = lastBeamSize.y * 0.5f;

        bool overlapsAlongBeam = distanceAlongBeam <= beamHalfLength + darknessProjectionAlongBeam;
        bool overlapsAcrossBeam = distanceAcrossBeam <= beamHalfWidth + darknessProjectionAcrossBeam;

        return overlapsAlongBeam && overlapsAcrossBeam;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 originPosition = beamOrigin != null
            ? beamOrigin.position
            : transform.position;

        Vector2 direction = lastBeamDirection.normalized;

        Vector2 boxCenter = originPosition + direction * (beamRange * 0.5f);
        Vector2 boxSize = new Vector2(beamRange, beamWidth);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Gizmos.color = Color.yellow;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
        Gizmos.matrix = oldMatrix;
    }

    public void OnMoveForBeam(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // No longer needed for mouse aiming, but kept so existing Player Input events do not break.
    }

       private void CheckLightGateInBeam()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll( // Checks all the collider objects belongs on the beam
            lastBeamCenter,
            lastBeamSize,
            lastBeamAngle,
            darknessLayer
        );

        foreach (Collider2D hit in hits)
        {
            spawn_platform gate = hit.GetComponentInParent<spawn_platform>(); // Use to checks do the object has a spawn_platform script.

            if  (gate != null)
            {
                gate.Activatespawn(); // Active the Activatespawn() function in the spawn_platform script
            }
        }
    }

    

}