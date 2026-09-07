using UnityEngine;

public class chaseCreature : MonoBehaviour
{
    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("movement")]
    public float speed = 5f;
    public Vector2 direction = Vector2.right;

    [Header("damage")]
    public string playerTag = "Player";
    private bool isMoving = false;

    public void StartMoving()
    {
        if (spawnPoint != null)
        {
            transform.position= spawnPoint.position;
        }
        gameObject.SetActive(true);
        isMoving = true;
    }

    public void StopMoving()
    {
        isMoving = false;
        gameObject.SetActive(false); //dissapears after chase ends 
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

        PlayerLifeSystem life = collision.GetComponent<PlayerLifeSystem>();
        if (life == null) //no life script found no damage and stop
        {
            Debug.LogWarning($"{name}: touched object {playerTag} with no playerlife system ");
            return;
        }

        life.KillInstantly(); //player tagged and is moving then attack it however kills outright ignores invulnerability    

    }
}

