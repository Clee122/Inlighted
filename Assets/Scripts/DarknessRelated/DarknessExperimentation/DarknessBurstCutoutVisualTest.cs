using UnityEngine;

public class DarknessBurstCutoutVisualTest : MonoBehaviour
{
    [Header("Light Burst Reference")]
    [SerializeField] private LightBurstController lightBurstController;

    [Header("Darkness Visual")]
    [SerializeField] private SpriteRenderer darknessRenderer;

    private Material darknessMaterial;

    // These property IDs match the Shader Graph references. Caching them avoids
    // repeatedly searching for shader properties while the Burst updates.
    private static readonly int CutoutCenterID =
        Shader.PropertyToID("_CutoutCenter");

    private static readonly int CutoutRadiusID =
        Shader.PropertyToID("_CutoutRadius");

    private static readonly int CutoutEnabledID =
        Shader.PropertyToID("_CutoutEnabled");

    private void Awake()
    {
        if (darknessRenderer == null)
        {
            darknessRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (darknessRenderer != null)
        {
            // Renderer.material creates a local material instance so this
            // experiment changes only this darkness visual rather than every
            // object that may happen to share the same material asset.
            darknessMaterial =
                darknessRenderer.material;
        }
    }

    private void Update()
    {
        if (
            darknessMaterial == null ||
            lightBurstController == null
        )
        {
            return;
        }

        if (!lightBurstController.IsBurstActive())
        {
            // Turning the mask off restores the continuous darkness immediately.
            // Reform timing can be tested later after the basic cut-out works.
            darknessMaterial.SetFloat(
                CutoutEnabledID,
                0f
            );

            return;
        }

        Vector2 burstPosition =
            lightBurstController.transform.position;

        float burstRadius =
            lightBurstController.GetCurrentBurstRadius();

        // The shader receives the same live position and expanding radius used
        // by the real Light Burst so the visible hole should match gameplay.
        darknessMaterial.SetVector(
            CutoutCenterID,
            new Vector4(
                burstPosition.x,
                burstPosition.y,
                0f,
                0f
            )
        );

        darknessMaterial.SetFloat(
            CutoutRadiusID,
            burstRadius
        );

        darknessMaterial.SetFloat(
            CutoutEnabledID,
            1f
        );
    }
}