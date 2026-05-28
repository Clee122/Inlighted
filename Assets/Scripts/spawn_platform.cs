using UnityEngine;

public class spawn_platform : MonoBehaviour
{
    [SerializeField] private spawn[] lightplatformSpawner; // Select the spawners want to activate

 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void Activatespawn()
    {
        foreach (spawn spawner in lightplatformSpawner) // loop through every spawner being select in lightplatformSpawner
        {
            if (spawner != null) // check if the spawner exists
            {
                spawner.Spawn(); // Active the Spawn() function to spawn the platform
            }
        }

    }
}