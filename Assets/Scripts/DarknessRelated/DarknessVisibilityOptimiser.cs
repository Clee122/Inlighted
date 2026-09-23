

using UnityEngine;

/*
 * Attach this script to each DarknessMass that has a DarknessCutoutController.
 *
 * DarknessCutoutController is disabled when its darkness mass leaves the
 * camera's view. This suspends CPU mask generation until it becomes visible.
 *
 * SharedDarknessZoneController and its damage colliders remain active.
 *
 * Disabling DarknessCutoutController also deliberately freezes its stored
 * Beam cut-out state. That state resumes when the controller is re-enabled.
 */
public class DarknessVisibilityOptimiser : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer darknessRenderer;
    [SerializeField] private DarknessCutoutController darknessCutoutController;
    [SerializeField] private DarknessMassSectionPulse sectionPulse;

    [Header("Camera Visibility")]

    /*
     * An extended camera rectangle activates darkness before it enters
     * the screen. This helps prevent visible activation delays near
     * the camera's boundaries.
     */
    [Min(0f)]
    [SerializeField] private float cameraBuffer = 2f;

    /*
     * Visibility checks are performed at regular intervals instead
     * of every frame to reduce unnecessary calculations.
     */
    [Min(0.02f)]
    [SerializeField] private float visibilityCheckInterval = 0.1f;

    [Header("Optional Visual Movement")]

    /*
     * The Section Pulse animation is purely visual.
     * When this option is enabled, its movement stops while the
     * darkness is outside the camera's view.
     */
    [SerializeField] private bool pauseSectionPulseOffScreen = true;

    private float nextVisibilityCheck;
    private bool? previousVisibility;

    private void Awake()
    {
        /*
         * Cache component references so visibility checks do not
         * repeatedly search for components on this GameObject.
         */
        if (darknessRenderer == null)
        {
            darknessRenderer = GetComponent<SpriteRenderer>();
        }

        if (darknessCutoutController == null)
        {
            darknessCutoutController =
                GetComponent<DarknessCutoutController>();
        }

        if (sectionPulse == null)
        {
            sectionPulse = GetComponent<DarknessMassSectionPulse>();
        }
    }

    private void OnEnable()
    {
        /*
         * Force a new visibility check whenever the optimiser
         * becomes active or the scene starts.
         */
        previousVisibility = null;
        nextVisibilityCheck = 0f;
    }

    private void LateUpdate()
    {
        /*
         * Checking visibility at intervals avoids unnecessary
         * camera-bound calculations every frame.
         */
        if (Time.unscaledTime < nextVisibilityCheck)
        {
            return;
        }

        nextVisibilityCheck =
            Time.unscaledTime +
            Mathf.Max(0.02f, visibilityCheckInterval);

        /*
         * Automatically recover the Main Camera reference if
         * it has not been assigned in the Inspector.
         */
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        /*
         * Missing references fail safely by keeping the darkness
         * controller enabled rather than accidentally freezing
         * an incorrectly configured darkness mass.
         */
        if (
            targetCamera == null ||
            darknessRenderer == null ||
            darknessRenderer.sprite == null ||
            darknessCutoutController == null
        )
        {
            ApplyVisibility(true);
            return;
        }

        /*
         * Report visibility transitions and the camera's position
         * so incorrect camera detection can be investigated.
         *
         * Messages are generated only when visibility changes,
         * preventing unnecessary Console spam during gameplay.
         */
        bool isVisible = IsInsideBufferedCameraView();

        if (
            !previousVisibility.HasValue ||
            previousVisibility.Value != isVisible
        )
        {
            Debug.Log(
                "[Darkness Optimiser] " + gameObject.name +
                " | Visible: " + isVisible +
                " | Camera: " + targetCamera.name +
                " | Camera Position: " +
                    targetCamera.transform.position +
                " | Darkness Bounds: " +
                    darknessRenderer.bounds,
                this
            );
        }

        ApplyVisibility(isVisible);
    }

    private bool IsInsideBufferedCameraView()
    {
        /*
         * In this 2D project, the Main Camera is orthographic.
         * Its viewport corners are converted into world coordinates
         * so the camera's visible region can be compared with
         * the darkness renderer's world-space bounds.
         *
         * Four corners also provide conservative bounds if the
         * orthographic camera is slightly rotated.
         */
        float depth = Mathf.Abs(
            darknessRenderer.bounds.center.z -
            targetCamera.transform.position.z
        );

        Vector3 bottomLeft =
            targetCamera.ViewportToWorldPoint(
                new Vector3(0f, 0f, depth)
            );

        Vector3 topLeft =
            targetCamera.ViewportToWorldPoint(
                new Vector3(0f, 1f, depth)
            );

        Vector3 bottomRight =
            targetCamera.ViewportToWorldPoint(
                new Vector3(1f, 0f, depth)
            );

        Vector3 topRight =
            targetCamera.ViewportToWorldPoint(
                new Vector3(1f, 1f, depth)
            );

        /*
         * The buffer extends detection beyond the visible camera
         * boundaries so darkness can activate before entering view.
         */
        float minX = Mathf.Min(
            bottomLeft.x,
            topLeft.x,
            bottomRight.x,
            topRight.x
        ) - cameraBuffer;

        float maxX = Mathf.Max(
            bottomLeft.x,
            topLeft.x,
            bottomRight.x,
            topRight.x
        ) + cameraBuffer;

        float minY = Mathf.Min(
            bottomLeft.y,
            topLeft.y,
            bottomRight.y,
            topRight.y
        ) - cameraBuffer;

        float maxY = Mathf.Max(
            bottomLeft.y,
            topLeft.y,
            bottomRight.y,
            topRight.y
        ) + cameraBuffer;

        Bounds darknessBounds = darknessRenderer.bounds;

        /*
         * The darkness is considered visible whenever any part
         * of its bounds overlaps the buffered camera rectangle.
         */
        return
            darknessBounds.max.x >= minX &&
            darknessBounds.min.x <= maxX &&
            darknessBounds.max.y >= minY &&
            darknessBounds.min.y <= maxY;
    }

    private void ApplyVisibility(bool isVisible)
    {
        /*
         * Only change component states when visibility changes.
         * This avoids repeatedly enabling or disabling components
         * during normal gameplay.
         */
        if (
            previousVisibility.HasValue &&
            previousVisibility.Value == isVisible
        )
        {
            return;
        }

        previousVisibility = isVisible;

        if (darknessCutoutController != null)
        {
            /*
             * Disable the entire cut-out controller while off-screen.
             *
             * This stops CPU mask generation and deliberately freezes
             * the current Beam deformation and lifecycle state.
             *
             * When the darkness becomes visible again, Unity enables
             * the controller and its normal Update method resumes.
             */
            darknessCutoutController.enabled = isVisible;
        }

        if (
            pauseSectionPulseOffScreen &&
            sectionPulse != null
        )
        {
            /*
             * The Section Pulse only controls the visual darkness
             * movement. Its animation is suspended separately
             * when the darkness is outside the camera view.
             */
            sectionPulse.enabled = isVisible;
        }

        /*
         * Confirm that the correct component states have been
         * applied whenever camera visibility changes.
         */
        Debug.Log(
            "[Darkness Optimiser] " + gameObject.name +
            " | Visible: " + isVisible +
            " | Cutout Controller Enabled: " +
            (
                darknessCutoutController != null
                    ? darknessCutoutController.enabled.ToString()
                    : "Missing"
            ) +
            " | Section Pulse Enabled: " +
            (
                sectionPulse != null
                    ? sectionPulse.enabled.ToString()
                    : "Missing"
            ),
            this
        );
    }

    private void OnDisable()
    {
        /*
         * Disabling or removing the optimiser must restore normal
         * darkness behaviour so components cannot remain
         * unintentionally suspended.
         */
        if (darknessCutoutController != null)
        {
            darknessCutoutController.enabled = true;
        }

        if (
            pauseSectionPulseOffScreen &&
            sectionPulse != null
        )
        {
            sectionPulse.enabled = true;
        }

        previousVisibility = null;
    }
}
