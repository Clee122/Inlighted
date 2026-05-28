using UnityEngine;

public class spawn : MonoBehaviour
{
   public GameObject light_platform;
   public Transform spawnPoint;
   private GameObject currentplatform;

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
         if (currentplatform != null)
            return;

        currentplatform = Instantiate(light_platform, spawnPoint.position, spawnPoint.rotation);

    }
}
