using Cinemachine;
using System.Collections;
using UnityEngine;

public class ChaseCreature : MonoBehaviour
{
    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("animation")]
    public Animator animator;

    [Header("movement")]
    public float speed = 5f;
    public Vector2 direction = Vector2.right;

    //Place empty Transforms tracing the exact route (up climbs, around pits) 
    //and drag them in, in order. Leave empty to fall back to straight-line movement via speed/direction above.
    [Header("Waypoint Path")]
    public Transform[] waypoints;
    public float waypointReachedDistance = 0.15f; //how close the creature needs to be to waypoint before it moves to next
    private int currentWaypointIndex = 0;

    [Header("damage")]
    public string playerTag = "Player";
    private bool isMoving = false;

    [Header("despawn")]
    public float delay = 2.5f;
    private Coroutine despawnRoutine;

    [Header("Catch then kill")]
    public float time = 0.2f;
    private Coroutine catchRoutine;

    [Header("Screen flash when caught")]
    public UnityEngine.UI.Image flashImage;
    public float flashAlpha = 0.35f; //how opaque the flash is from 0-1 
    public float flashFadeTime = 0.2f;

    [Header("Spawn with Juice")]
    public SpriteRenderer visual;
    public ParticleSystem slamParticles;
    public Cinemachine.CinemachineImpulseSource impulseSource;
    public float anticipationTime = 2f; //in seconds
    public float shakeForce = 1f;

    [Header("Chase Camera")]
    public cameramanage cameraManager;
    public float chaseOrthoSize = 7f;// zoomed-out size during the chase
    public float zoomOutTime = 1.2f;
    public float zoomBackTime = 2.5f;
    public float zoomBackDelay = 0.6f;

    private Coroutine zoomRoutine;
    private float originalZoomSpeed;

    [Header("Repeated Slam particles")]
    public bool repeatSlamWhileChasing = true;
    public float repeatSlamMinInterval = 1.5f;
    public float repeatSlamMaxInterval = 3f;
    public float repeatShakeForce = 0.3f;

    private Coroutine repeatSlamRoutine;

    [Header("Particle Ground Snap")]
    public LayerMask groundLayer;
    public float groundCheckUpOffset = 1f;
    public float groundCheckMaxDistance = 5f;
    public float groundSnapYOffset = 0.05f;


    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip startChase;
    public AudioClip endChase;
    public AudioClip caught;

    private void Awake()
    {
        if (cameraManager != null) originalZoomSpeed = cameraManager.zoomSpeed;
    }
    public void StartMoving()
    {
        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
            despawnRoutine = null;
        }

        if (catchRoutine != null)
        {
            StopCoroutine(catchRoutine);
            catchRoutine = null;
        }

        if (repeatSlamRoutine != null)
        {
            StopCoroutine(repeatSlamRoutine);
            repeatSlamRoutine = null;
        }

        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
        }
        gameObject.SetActive(true);

        currentWaypointIndex = 0;  //start the route over from the first waypoint each time the chase begins

        if (visual != null)
        {
            visual.enabled = false; //hidden during anticipation, SpawnRoutine turns it back on
        }

        StartZoom(chaseOrthoSize, zoomOutTime);
        StartCoroutine(SpawnRoutine());

    }

    public void StopMoving()
    {
        if (!isMoving)
        {
            if (cameraManager != null) StartZoom(cameraManager.normalOrtho, zoomBackTime);
            return;
        }

        isMoving = false;
        if (cameraManager != null) StartZoom(cameraManager.normalOrtho, zoomBackTime, zoomBackDelay);

        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
        }
        despawnRoutine = StartCoroutine(DespawnAfterDelay());

        if (repeatSlamRoutine != null)
        {
            StopCoroutine(repeatSlamRoutine);
            repeatSlamRoutine = null;
        }

        PlayClip(endChase);

    }

    private void Update()
    {
        if (!isMoving) return;

        if (waypoints != null && waypoints.Length > 0)
        {
            MoveAlongWaypoints();
        }
        else
        {
            transform.position += (Vector3)(direction.normalized * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isMoving) return; //not moving do nothing 
        if (!collision.CompareTag(playerTag)) return; // if not player don't continue 
        if (catchRoutine != null) return; //if caught ignore the trigger

        PlayerLifeSystem life = collision.GetComponent<PlayerLifeSystem>();
        if (life == null) //no life script found no damage and stop
        {
            Debug.LogWarning($"{name}: touched object {playerTag} with no playerlife system ");
            return;
        }


        FlashRed(); //hits the player with a flash right away for player feedback to feel like you've been caught
        catchRoutine = StartCoroutine(CatchRoutine(life));
    }

    private void MoveAlongWaypoints()
    {
        Transform target = waypoints[currentWaypointIndex]; //assumes waypoints is fully populated, no empty-slot check

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) <= waypointReachedDistance)
        {
            AdvanceWaypointOrStop();
        }
    }

    private void AdvanceWaypointOrStop()//moves to the next waypoint in the array; if that was the last one, the route is done so end the chase
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >= waypoints.Length)
        {
            StopMoving();
        }
    }

    //draws path in scene view so can see when playing waypoints
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue; //skip empty slots so we don't crash trying to read their position

            Gizmos.DrawSphere(waypoints[i].position, 0.1f);

            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }

    private void FlashRed()
    {
        if (flashImage == null) return;
        StartCoroutine(FlashRedRoutine());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(delay, zoomBackDelay + zoomBackTime + 0.1f));
        gameObject.SetActive(false);
    }

    private IEnumerator CatchRoutine(PlayerLifeSystem life)
    {
        yield return new WaitForSeconds(time);

        PlayClip(caught);
        life.KillInstantly(); //ignores invulnerability

        catchRoutine = null;
        StopMoving();
    }    

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(anticipationTime);

        if (visual != null)
        {
            visual.enabled = true;
        }
        if (slamParticles != null)
        {
            SnapParticlesToGround();
            slamParticles.Play();
        }
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(shakeForce);
        }

        PlayClip(startChase);
        isMoving = true;

        if (repeatSlamWhileChasing)
        {
            repeatSlamRoutine = StartCoroutine(RepeatSlamRoutine());
        }
    }


    private IEnumerator RepeatSlamRoutine()
    {
        while (isMoving)
        {

            float wait = Random.Range(repeatSlamMinInterval, repeatSlamMaxInterval);
            yield return new WaitForSeconds(wait);

            if (!isMoving) yield break; //caught or stopped do not fire

            if (slamParticles != null)
            {
                SnapParticlesToGround();
                slamParticles.Play();
            }
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(repeatShakeForce);
            }
        }
    }
    
    private void SnapParticlesToGround()
    {
        Vector2 rayStart = (Vector2)transform.position + Vector2.up * groundCheckUpOffset;
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, groundCheckMaxDistance, groundLayer);

        if (hit.collider != null)
        {
            Vector3 groundPos = slamParticles.transform.position;
            groundPos.y = hit.point.y + groundSnapYOffset;
            slamParticles.transform.position = groundPos;// if nothing was hit (e.g. mid-air, gap in terrain), particles just stay wherever they already were rather than snapping somewhere wrong

        }
    }

    private IEnumerator FlashRedRoutine()
    {
        Color c = flashImage.color; //grabs the current colour so any rgb that is set is not overwritten 
        c.a = flashAlpha; //snaps straight to a full flash alpha with no ease in
        flashImage.color = c;

        float elapsed = 0f;
        while (elapsed < flashFadeTime)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(flashAlpha, 0f, elapsed / flashFadeTime); //lerps used to go from the full flash back down to invis
            flashImage.color = c;
            yield return null;
        }

        c.a = 0f;  //gaurentees it goes back to fully invisible 
        flashImage.color = c;
    }

    private void StartZoom(float target, float duration, float startDelay = 0f)
    {
        if (cameraManager == null) return;
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        zoomRoutine = StartCoroutine(ZoomRoutine(target, duration, startDelay));
    }
    private IEnumerator ZoomRoutine(float target, float duration, float startDelay)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float start = cameraManager.vcam.m_Lens.OrthographicSize;
        cameraManager.zoomSpeed = 1000f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cameraManager.targetOrtho = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        cameraManager.targetOrtho = target;
        cameraManager.zoomSpeed = originalZoomSpeed;
        zoomRoutine = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}

