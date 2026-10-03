using UnityEngine;

public class ChaseZone : MonoBehaviour
{
   public enum ZoneType {Start, Stop} 

    [Header("setup")]
    public ChaseCreature Trigger;
    public ZoneType zonetype = ZoneType.Start; //pick what the collider does in the inspector 
    public string playerTag = "Player";

    private Collider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        PlayerLifeSystem.OnPlayerRespawned += ResetZone;
    }

    private void OnDisable()
    {
        PlayerLifeSystem.OnPlayerRespawned -= ResetZone;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {

        if (!collision.CompareTag(playerTag)) return; //if not player dont continue

        if (zonetype == ZoneType.Start)
        {
            if(zoneCollider != null) zoneCollider.enabled = false;// one-shot until reset
            Trigger.StartMoving(); //collider start line
        }
        else
        {
            Trigger.StopMoving(); //collider stopline 
        }
    }

    private void ResetZone()
    {
        if (zonetype == ZoneType.Start && zoneCollider != null)
        {
            zoneCollider.enabled = true;
        }
    }
}
