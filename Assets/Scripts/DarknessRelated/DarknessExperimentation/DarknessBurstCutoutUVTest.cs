using UnityEngine;

public class DarknessBurstCutoutUVTest : MonoBehaviour
{
    [Header("Light Burst")]
    [SerializeField]
    private LightBurstController lightBurstController;

    [Header("Darkness Visual")]
    [SerializeField]
    private SpriteRenderer darknessRenderer;

    private Material darknessMaterial;

    // Shader property IDs are cached because the values are updated every frame
    // while Burst is active. This avoids repeatedly looking up properties by name.
    private static readonly int BurstCenterUVID =
        Shader.PropertyToID("_BurstCenterUV");

    private static readonly int BurstRadiusUVID =
        Shader.PropertyToID("_BurstRadiusUV");

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
            // A local material instance is used so this experimental cut-out only
            // changes this darkness visual and cannot affect other shared materials.
            darknessMaterial =
                darknessRenderer.material;
        }
    }

    private void Update()
    {
        if (
            darknessMaterial == null ||
            darknessRenderer == null ||
            darknessRenderer.sprite == null ||
            lightBurstController == null
        )
        {
            return;
        }

        if (!lightBurstController.IsBurstActive())
        {
            // The first version reforms immediately because this test only needs
            // to prove that the UV-based cut-out can replace the crashing Position node.
            darknessMaterial.SetFloat(
                CutoutEnabledID,
                0f
            );

            return;
        }

        UpdateBurstCutout();
    }

    private void UpdateBurstCutout()
    {
        Vector3 burstWorldPosition =
            lightBurstController.transform.position;

        // Converting the Burst into this sprite's local space means the shader
        // does not need a World Position node, which is currently crashing Unity.
        Vector3 burstLocalPosition =
            darknessRenderer.transform.InverseTransformPoint(
                burstWorldPosition
            );

        Bounds spriteBounds =
            darknessRenderer.sprite.bounds;

        // UV coordinates are calculated manually instead of using Mathf.InverseLerp
        // because InverseLerp clamps values to 0-1. We deliberately allow values
        // outside that range so a Burst located outside the darkness remains outside
        // the shader surface rather than being incorrectly snapped to its nearest edge.
        float uvX =
            spriteBounds.size.x > 0f
                ? (burstLocalPosition.x - spriteBounds.min.x) /
                  spriteBounds.size.x
                : 0f;

        float uvY =
            spriteBounds.size.y > 0f
                ? (burstLocalPosition.y - spriteBounds.min.y) /
                  spriteBounds.size.y
                : 0f;

        float burstWorldRadius =
            lightBurstController.GetCurrentBurstRadius();

        Vector3 lossyScale =
            darknessRenderer.transform.lossyScale;

        float darknessWorldWidth =
            spriteBounds.size.x *
            Mathf.Abs(lossyScale.x);

        float darknessWorldHeight =
            spriteBounds.size.y *
            Mathf.Abs(lossyScale.y);

        // Separate X and Y UV radii compensate for a darkness sprite that has
        // been stretched into a rectangle. Without this, a world-space circle
        // would appear as an ellipse when represented in ordinary UV coordinates.
        float radiusUVX =
            darknessWorldWidth > 0f
                ? burstWorldRadius / darknessWorldWidth
                : 0f;

        float radiusUVY =
            darknessWorldHeight > 0f
                ? burstWorldRadius / darknessWorldHeight
                : 0f;

        darknessMaterial.SetVector(
            BurstCenterUVID,
            new Vector4(
                uvX,
                uvY,
                0f,
                0f
            )
        );

        darknessMaterial.SetVector(
            BurstRadiusUVID,
            new Vector4(
                radiusUVX,
                radiusUVY,
                0f,
                0f
            )
        );

        darknessMaterial.SetFloat(
            CutoutEnabledID,
            1f
        );
    }
}