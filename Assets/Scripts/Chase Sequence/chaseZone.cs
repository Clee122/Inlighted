using UnityEngine;

public class chaseZone : MonoBehaviour
{
   public enum ZoneType {Start, Stop} 

    [Header("setup")]
    public chaseCreature Trigger;
    public ZoneType zonetype = ZoneType.Start; //pick what the collider does in the inspector 
    public string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag(playerTag)) return; //if not player dont continue

        if (zonetype == ZoneType.Start)
        {
            Trigger.StartMoving(); //collider start line
        }
        else
        {
            Trigger.StopMoving(); //collider stopline 
        }
    }

}
