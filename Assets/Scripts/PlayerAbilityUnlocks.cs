using UnityEngine;

public class PlayerAbilityUnlocks : MonoBehaviour
{
    [Header("Unlocked Abilities")]
    [SerializeField] private bool hasLightBurst = false;
    [SerializeField] private bool hasLightBeam = false;

    private AbilityUnlockObject nearbyUnlockObject;

    public bool HasLightBurst()
    {
        return hasLightBurst;
    }

    public bool HasLightBeam()
    {
        return hasLightBeam;
    }

    public void SetNearbyUnlockObject(AbilityUnlockObject unlockObject)
    {
        // The player stores the current nearby unlock object so ability unlocks feel intentional.
        // This means the player must be close to the object and press interact, instead of unlocking
        // an ability accidentally just by touching something.
        nearbyUnlockObject = unlockObject;
    }

    public void ClearNearbyUnlockObject(AbilityUnlockObject unlockObject)
    {
        // Only clear the interaction target if it is the same object the player just left.
        // This avoids accidentally clearing another nearby unlock object if multiple triggers overlap.
        if (nearbyUnlockObject == unlockObject)
        {
            nearbyUnlockObject = null;
        }
    }

    public void OnInteract(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        // The interact input only does something if the player is currently near an unlock object.
        // This keeps the interaction system simple and prevents the player from unlocking abilities
        // from anywhere in the level.
        if (nearbyUnlockObject != null)
        {
            nearbyUnlockObject.UnlockAbility(this);
        }
    }

    public void UnlockLightBurst()
    {
        // Light Burst is unlocked before Light Beam so the player first learns the defensive,
        // close-range light ability before moving on to the more puzzle-focused beam ability.
        hasLightBurst = true;
        Debug.Log("Light Burst unlocked");
    }

    public void UnlockLightBeam()
    {
        // Light Beam is unlocked separately so level progression can teach the abilities one at a time.
        // This also lets the team design early areas around burst first, then introduce beam puzzles later.
        hasLightBeam = true;
        Debug.Log("Light Beam unlocked");
    }
}