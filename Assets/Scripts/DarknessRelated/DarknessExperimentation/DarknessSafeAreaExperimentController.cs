using UnityEngine;

public class DarknessSafeAreaExperimentController : MonoBehaviour
{
    [Header("Darkness Cutout Reference")]

    // The combined darkness controller owns both visual deformation and gameplay
    // safety so the player is protected by exactly the openings shown on screen.
    [SerializeField]
    private DarknessCombinedCutoutUVTest darknessCutout;

    public bool IsPositionSafe(
        Vector2 worldPosition
    )
    {
        if (darknessCutout == null)
        {
            return false;
        }

        return
            darknessCutout.IsPositionInsideLightCutout(
                worldPosition
            );
    }
}