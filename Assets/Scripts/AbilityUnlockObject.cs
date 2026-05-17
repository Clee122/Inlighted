using UnityEngine;

public class AbilityUnlockObject : MonoBehaviour
{
    // Using an enum means this one script can be reused for different ability unlocks.
    // At the moment we only need Light Burst and Light Beam, but this also makes it easy
    // to add more abilities later without creating a new unlock script every time.
    public enum AbilityType
    {
        LightBurst,
        LightBeam
    }

    [Header("Ability Unlock")]
    [SerializeField] private AbilityType abilityToUnlock;

    [Header("Prompt")]
    [SerializeField] private GameObject promptObject;

    [Header("After Unlock")]
    [SerializeField] private bool disableAfterUnlock = false;
    [SerializeField] private Color unlockedColor = Color.yellow;

    private bool hasBeenUnlocked = false;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // Storing the SpriteRenderer lets the object visually change after it has been used.
        // I wanted the unlock object to remain in the level rather than disappear, because
        // these objects are meant to feel important instead of behaving like a normal pickup.
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // The prompt should not be visible all the time, otherwise it clutters the screen.
        // It only becomes useful once the player is close enough to interact.
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // After the ability is unlocked, the object should stop acting like an interactable.
        // This prevents repeated prompts and makes it clear that the player has already used it.
        if (hasBeenUnlocked)
            return;

        PlayerAbilityUnlocks playerUnlocks = other.GetComponent<PlayerAbilityUnlocks>();

        if (playerUnlocks != null)
        {
            // The player keeps track of the current nearby unlock object.
            // This makes the interaction more intentional than unlocking the ability by accident
            // just because the player walked into the trigger.
            playerUnlocks.SetNearbyUnlockObject(this);

            if (promptObject != null)
            {
                promptObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerAbilityUnlocks playerUnlocks = other.GetComponent<PlayerAbilityUnlocks>();

        if (playerUnlocks != null)
        {
            // Clear the reference when the player leaves so they cannot press C from far away
            // and unlock something they are no longer standing near.
            playerUnlocks.ClearNearbyUnlockObject(this);

            if (promptObject != null)
            {
                promptObject.SetActive(false);
            }
        }
    }

    public void UnlockAbility(PlayerAbilityUnlocks playerUnlocks)
    {
        // This protects against the object being triggered twice, which could happen if the
        // player presses the interact button repeatedly or if multiple trigger events overlap.
        if (hasBeenUnlocked || playerUnlocks == null)
            return;

        // The actual ability is chosen in the Inspector. This keeps the script flexible,
        // because the same prefab/script can be used for the first Light Burst unlock
        // and the later Light Beam unlock.
        switch (abilityToUnlock)
        {
            case AbilityType.LightBurst:
                playerUnlocks.UnlockLightBurst();
                break;

            case AbilityType.LightBeam:
                playerUnlocks.UnlockLightBeam();
                break;
        }

        hasBeenUnlocked = true;

        // Once the unlock happens, the prompt is no longer needed. Leaving it on would make
        // the player think there is still another interaction available.
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }

        // I am changing the colour instead of removing the object so the scene still shows
        // that this was an important unlock point. It also gives the player feedback that
        // the interaction worked.
        if (spriteRenderer != null)
        {
            spriteRenderer.color = unlockedColor;
        }

        Debug.Log(abilityToUnlock + " ability unlocked from " + gameObject.name);

        // This option is still here in case we later want smaller pickup-style objects
        // to disappear after use. For the main ability unlocks, I would keep this unticked.
        if (disableAfterUnlock)
        {
            gameObject.SetActive(false);
        }
    }
}