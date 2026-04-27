using UnityEngine;
using System.Collections;

public class checkpoint : MonoBehaviour
{
    private PlayerRespawn respawn;
    void Awake()
    {
        respawn = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerRespawn>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
       { if (collision.gameObject.tag == "Player")
             respawn.SetCheckpoint(transform);
       }
    }
}
