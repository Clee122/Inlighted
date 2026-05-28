using UnityEngine;

public class spawn : MonoBehaviour
{
   public GameObject light_platform; //Select the light platform object want to spawn
   public Transform spawnPoint; // Set the spawner where the platform will appear
   private GameObject currentplatform; // Use to see has the platform spawn

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
         if (currentplatform != null) // do nothing when the platform spawn
            return;

        currentplatform = Instantiate(light_platform, spawnPoint.position, spawnPoint.rotation); // spawn the light platform at the spawners position

    }
}
