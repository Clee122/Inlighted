using UnityEngine;

public class spawn : MonoBehaviour
{
   public GameObject light_brige;
   public Transform spawnPoint;

      // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

   public void Spawn()
    {
        Instantiate(light_brige, spawnPoint.position, spawnPoint.rotation);
    }
}
