using UnityEngine;

public class DarknessBeamCutoutUVTest : MonoBehaviour
{
    [Header("Light Beam")]
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

    private float currentPushDistance = 0f;
    private bool wasBeamActive = false;

    private Vector2 storedBeamOriginWorld;
    private Vector2 storedBeamDirectionWorld;

    // Shader property IDs are cached because these values update repeatedly
    // during the Beam animation and should not rely on string lookups each frame.
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
            // A material instance is used so this Beam experiment cannot modify
            // another darkness visual that happens to share the material asset.
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
            lightBeamController == null ||
            beamVisualTransform == null
        )
        {
            return;
        }

        bool beamActive =
            lightBeamController.IsBeamActive();

        if (
            beamActive &&
            !wasBeamActive
        )
        {
            // The Beam origin and direction are captured once when firing begins
            // so the opening represents the fired shot rather than following the
            // Player if they move afterwards.
            storedBeamOriginWorld =
                lightBeamController.transform.position;

            storedBeamDirectionWorld =
                beamVisualTransform.right.normalized;

            currentPushDistance = 0f;
        }

        if (beamActive)
        {
            // The opening grows away from the Beam centreline over a short period
            // to test the idea of darkness being pushed upward and downward.
            currentPushDistance =
                Mathf.MoveTowards(
                    currentPushDistance,
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

            UpdateShaderValues();

            darknessMaterial.SetFloat(
                BeamEnabledID,
                1f
            );
        }
        else
        {
            // For this first low-fidelity test the darkness reforms immediately.
            // Delayed reform should only be added after the basic Beam push works.
            currentPushDistance = 0f;

            darknessMaterial.SetFloat(
                BeamEnabledID,
                0f
            );
        }

        wasBeamActive =
            beamActive;
    }

    private void UpdateShaderValues()
    {
        Bounds spriteBounds =
            darknessRenderer.sprite.bounds;

        Vector3 beamOriginLocal =
            darknessRenderer.transform.InverseTransformPoint(
                storedBeamOriginWorld
            );

        // Manual UV conversion intentionally allows values outside 0-1 so a Beam
        // outside the darkness is not incorrectly snapped onto its nearest edge.
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
                ? beamWorldLength / darknessWorldWidth
                : 0f;

        float beamPushDistanceUV =
            darknessWorldHeight > 0f
                ? currentPushDistance / darknessWorldHeight
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