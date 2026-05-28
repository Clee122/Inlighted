using UnityEngine;

public class spawn_brige : MonoBehaviour
{
    [SerializeField] private spawn[] lightplatformSpawner;

 
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
        foreach (spawn spawner in lightplatformSpawner)
        {
            if (spawner != null)
            {
                spawner.Spawn();
            }
        }

    }
}