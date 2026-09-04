using UnityEngine;

public class DarknessCombinedCutoutUVTest : MonoBehaviour
{
    [Header("Light Ability References")]
    [SerializeField]
    private LightBurstController lightBurstController;

    [SerializeField]
    private LightBeamController lightBeamController;

    [Header("Beam Visual")]
    [SerializeField]
    private Transform beamVisualTransform;

    [Header("Darkness Visual")]
    [SerializeField]
    private SpriteRenderer darknessRenderer;

    [Header("Beam Cutout Test")]
    [SerializeField]
    private float beamWorldLength = 6f;

    [SerializeField]
    private float beamMaximumPushDistance = 1.5f;

    [SerializeField]
    private float beamPushDuration = 0.3f;

    private Material darknessMaterial;

    private float currentBeamPushDistance = 0f;
    private bool wasBeamActive = false;

    private Vector2 storedBeamOriginWorld;
    private Vector2 storedBeamDirectionWorld;

    // These IDs match the exposed properties in the combined Shader Graph.
    // Caching them avoids repeated string-based lookups while the abilities update.
    private static readonly int BurstCenterUVID =
        Shader.PropertyToID("_BurstCenterUV");

    private static readonly int BurstRadiusUVID =
        Shader.PropertyToID("_BurstRadiusUV");

    private static readonly int CutoutEnabledID =
        Shader.PropertyToID("_CutoutEnabled");

    private static readonly int BeamOriginUVID =
        Shader.PropertyToID("_BeamOriginUV");

    private static readonly int BeamDirectionUVID =
        Shader.PropertyToID("_BeamDirectionUV");

    private static readonly int BeamLengthUVID =
        Shader.PropertyToID("_BeamLengthUV");

    private static readonly int BeamPushDistanceUVID =
        Shader.PropertyToID("_BeamPushDistanceUV");

    private static readonly int BeamEnabledID =
        Shader.PropertyToID("_BeamEnabled");

    private void Awake()
    {
        if (darknessRenderer == null)
        {
            darknessRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (darknessRenderer != null)
        {
            // A local material instance keeps the experiment isolated so changing
            // one darkness visual does not alter every renderer using the asset.
            darknessMaterial =
                darknessRenderer.material;
        }
    }

    private void Update()
    {
        if (
            darknessMaterial == null ||
            darknessRenderer == null ||
            darknessRenderer.sprite == null
        )
        {
            return;
        }

        UpdateBurst();
        UpdateBeam();
    }

    private void UpdateBurst()
    {
        if (
            lightBurstController == null ||
            !lightBurstController.IsBurstActive()
        )
        {
            darknessMaterial.SetFloat(
                CutoutEnabledID,
                0f
            );

            return;
        }

        Vector3 burstWorldPosition =
            lightBurstController.transform.position;

        Vector3 burstLocalPosition =
            darknessRenderer.transform.InverseTransformPoint(
                burstWorldPosition
            );

        Bounds spriteBounds =
            darknessRenderer.sprite.bounds;

        // Manual UV conversion deliberately allows coordinates outside 0-1 so
        // Burst does not snap onto the darkness edge when it is out of range.
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

        // Separate UV radii compensate for a darkness sprite stretched into a
        // rectangle so the Burst opening still appears circular in world space.
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

    private void UpdateBeam()
    {
        if (
            lightBeamController == null ||
            beamVisualTransform == null
        )
        {
            darknessMaterial.SetFloat(
                BeamEnabledID,
                0f
            );

            return;
        }

        bool beamActive =
            lightBeamController.IsBeamActive();

        if (
            beamActive &&
            !wasBeamActive
        )
        {
            // Capture the fired Beam once so the darkness opening stays aligned
            // with the shot rather than following later Player movement.
            storedBeamOriginWorld =
                lightBeamController.transform.position;

            storedBeamDirectionWorld =
                beamVisualTransform.right.normalized;

            currentBeamPushDistance = 0f;
        }

        if (beamActive)
        {
            // The Beam opening expands vertically over time so the darkness feels
            // pushed away from the Beam line rather than instantly disappearing.
            currentBeamPushDistance =
                Mathf.MoveTowards(
                    currentBeamPushDistance,
                    beamMaximumPushDistance,
                    (
                        beamMaximumPushDistance /
                        Mathf.Max(
                            beamPushDuration,
                            0.01f
                        )
                    ) *
                    Time.deltaTime
                );

            UpdateBeamShaderValues();

            darknessMaterial.SetFloat(
                BeamEnabledID,
                1f
            );
        }
        else
        {
            // For now the Beam deformation disappears immediately when the Beam
            // ends. Delayed reform will be added only after both abilities work
            // correctly through the same material.
            currentBeamPushDistance = 0f;

            darknessMaterial.SetFloat(
                BeamEnabledID,
                0f
            );
        }

        wasBeamActive =
            beamActive;
    }

    private void UpdateBeamShaderValues()
    {
        Bounds spriteBounds =
            darknessRenderer.sprite.bounds;

        Vector3 beamOriginLocal =
            darknessRenderer.transform.InverseTransformPoint(
                storedBeamOriginWorld
            );

        // Manual UV conversion avoids clamping the Beam origin onto the darkness
        // surface when the shot begins outside the visible darkness area.
        float originUVX =
            spriteBounds.size.x > 0f
                ? (beamOriginLocal.x - spriteBounds.min.x) /
                  spriteBounds.size.x
                : 0f;

        float originUVY =
            spriteBounds.size.y > 0f
                ? (beamOriginLocal.y - spriteBounds.min.y) /
                  spriteBounds.size.y
                : 0f;

        Vector3 lossyScale =
            darknessRenderer.transform.lossyScale;

        float darknessWorldWidth =
            spriteBounds.size.x *
            Mathf.Abs(lossyScale.x);

        float darknessWorldHeight =
            spriteBounds.size.y *
            Mathf.Abs(lossyScale.y);

        // The Beam direction is converted into UV space because the darkness
        // visual may have different horizontal and vertical scaling.
        Vector2 beamDirectionUV =
            new Vector2(
                darknessWorldWidth > 0f
                    ? storedBeamDirectionWorld.x / darknessWorldWidth
                    : 0f,

                darknessWorldHeight > 0f
                    ? storedBeamDirectionWorld.y / darknessWorldHeight
                    : 0f
            ).normalized;

        float beamLengthUV =
            darknessWorldWidth > 0f
                ? beamWorldLength /
                  darknessWorldWidth
                : 0f;

        float beamPushDistanceUV =
            darknessWorldHeight > 0f
                ? currentBeamPushDistance /
                  darknessWorldHeight
                : 0f;

        darknessMaterial.SetVector(
            BeamOriginUVID,
            new Vector4(
                originUVX,
                originUVY,
                0f,
                0f
            )
        );

        darknessMaterial.SetVector(
            BeamDirectionUVID,
            new Vector4(
                beamDirectionUV.x,
                beamDirectionUV.y,
                0f,
                0f
            )
        );

        darknessMaterial.SetFloat(
            BeamLengthUVID,
            beamLengthUV
        );

        darknessMaterial.SetFloat(
            BeamPushDistanceUVID,
            beamPushDistanceUV
        );
    }
}
