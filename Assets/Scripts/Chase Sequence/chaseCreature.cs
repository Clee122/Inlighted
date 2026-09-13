using System.Collections;
using UnityEngine;

public class ChaseCreature : MonoBehaviour
{
    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("movement")]
    public float speed = 5f;
    public Vector2 direction = Vector2.right;

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
    
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip startChase;
    public AudioClip endChase;
    public AudioClip caught;



    public void StartMoving()
    {
        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
            despawnRoutine = null;
        }

        if (spawnPoint != null)
        {
            transform.position= spawnPoint.position;
        }
        gameObject.SetActive(true);
        isMoving = true;

        PlayClip(startChase);
    }

    public void StopMoving()
    {
        isMoving = false;

        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
        }
        despawnRoutine = StartCoroutine(DespawnAfterDelay());

        PlayClip(endChase);
     
    }

    private void Update()
    {
        if (!isMoving) return;

        transform.position += (Vector3)(direction.normalized * speed * Time.deltaTime);
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
 
    private void FlashRed()
    {
        if (flashImage == null) return;
        StartCoroutine(FlashRedRoutine());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(delay);
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

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}

