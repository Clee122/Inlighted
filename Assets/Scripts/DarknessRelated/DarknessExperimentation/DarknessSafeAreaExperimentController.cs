using UnityEngine;

public class DarknessSafeAreaExperimentController : MonoBehaviour
{
    [SerializeField]
    private DarknessCutoutController darknessCutout;

    public bool IsPositionSafe(
        Vector2 worldPosition
    )
    {
        /*
         * Gameplay safety asks the same controller that generates the visible
         * Burst and Beam openings. This keeps damage behaviour aligned with the
         * actual darkness cut-outs instead of maintaining a second calculation.
         */
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