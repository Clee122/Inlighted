using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LightBurstOcclusionMesh : MonoBehaviour
{
    [Header("Burst Reference")]
    [SerializeField] private LightBurstController lightBurstController;

    [Header("Wall Detection")]

    // The occlusion mesh still checks the normal Ground layer because both
    // genuine blockers and Burst-pass-through platforms need player collision.
    // The tag/component checks later decide which Ground colliders actually block Burst.
    [SerializeField] private LayerMask wallLayer;

    // A higher ray count gives the cover more precision around walls, floors,
    // ceilings and platform edges while the Burst is expanding.
    [SerializeField] private int rayCount = 128;

    // The cover begins slightly beyond the collision point so it does not
    // noticeably overlap the visible contact boundary of the Burst.
    [SerializeField] private float occlusionOverlap = 0.03f;

    [Header("Rendering")]

    // The occlusion mesh must render above the circular Burst because its job
    // is to redraw the captured environment over sections blocked by geometry.
    [SerializeField] private string sortingLayerName = "Player";
    [SerializeField] private int sortingOrder = 6;

    private Mesh occlusionMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private readonly List<Vector3> vertices =
        new List<Vector3>();

    private readonly List<int> triangles =
        new List<int>();

    private struct OcclusionSample
    {
        public Vector2 direction;
        public float hitDistance;
        public bool blocked;
    }

    private void Awake()
    {
        FindReferences();
        EnsureMeshExists();
    }

    private void OnEnable()
    {
        FindReferences();
        EnsureMeshExists();
    }

    private void OnDisable()
    {
        // Clearing the generated mesh prevents geometry from a previous Burst
        // briefly appearing when this object becomes active again.
        if (occlusionMesh != null)
        {
            occlusionMesh.Clear();
        }
    }

    private void FindReferences()
    {
        if (meshFilter == null)
        {
            meshFilter =
                GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer =
                GetComponent<MeshRenderer>();
        }

        if (meshRenderer != null)
        {
            // Explicit sorting keeps the environment-cover mesh above the Burst
            // while still allowing foreground artwork to render over both.
            meshRenderer.sortingLayerName =
                sortingLayerName;

            meshRenderer.sortingOrder =
                sortingOrder;
        }

        if (lightBurstController == null)
        {
            // BurstOcclusionMesh normally sits underneath Player, so finding the
            // controller through the parent avoids another manual reference.
            lightBurstController =
                GetComponentInParent<LightBurstController>();
        }

        if (lightBurstController == null)
        {
            Debug.LogError(
                "LightBurstOcclusionMesh could not find LightBurstController. " +
                "The occlusion mesh cannot follow the Burst radius."
            );
        }
    }

    private void EnsureMeshExists()
    {
        if (meshFilter == null)
        {
            meshFilter =
                GetComponent<MeshFilter>();
        }

        if (occlusionMesh == null)
        {
            occlusionMesh =
                new Mesh();

            occlusionMesh.name =
                "Light Burst Occlusion Mesh";

            meshFilter.sharedMesh =
                occlusionMesh;

            return;
        }

        // Runtime-generated meshes can lose their MeshFilter assignment after
        // enable/disable cycles, so reconnecting it keeps repeated uses reliable.
        if (
            meshFilter != null &&
            meshFilter.sharedMesh != occlusionMesh
        )
        {
            meshFilter.sharedMesh =
                occlusionMesh;
        }
    }

    private void Update()
    {
        if (lightBurstController == null)
        {
            return;
        }

        if (!lightBurstController.IsBurstActive())
        {
            if (occlusionMesh != null)
            {
                occlusionMesh.Clear();
            }

            return;
        }

        BuildOcclusionMesh(
            lightBurstController.GetCurrentBurstRadius()
        );
    }

    private void BuildOcclusionMesh(
        float currentRadius
    )
    {
        EnsureMeshExists();

        if (occlusionMesh == null)
        {
            return;
        }

        vertices.Clear();
        triangles.Clear();

        Vector2 worldOrigin =
            transform.position;

        /*
         * Each radial sample looks for the first genuine Burst blocker.
         *
         * RaycastAll is important here because BurstPassThrough platforms and
         * Bloom Platforms remain on the Ground layer for normal player collision.
         * A single Raycast would stop at those platforms before discovering a
         * genuine wall or other blocking surface behind them.
         */
        OcclusionSample[] samples =
            new OcclusionSample[
                rayCount + 1
            ];

        for (
            int i = 0;
            i <= rayCount;
            i++
        )
        {
            float angle =
                ((float)i / rayCount) *
                Mathf.PI *
                2f;

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ).normalized;

            RaycastHit2D[] hits =
                Physics2D.RaycastAll(
                    worldOrigin,
                    direction,
                    currentRadius,
                    wallLayer
                );

            RaycastHit2D blockingHit =
                new RaycastHit2D();

            bool foundBlockingHit =
                false;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                /*
                 * Small Burst-pass-through platforms still need to behave as
                 * Ground for player movement, but they should not visually cut
                 * the Burst. Their tag marks them as safe to ignore here.
                 */
                bool isBurstPassThrough =
                    hit.collider.CompareTag(
                        "BurstPassThrough"
                    );

                /*
                 * Bloom Platforms also need solid Ground colliders while they
                 * are bloomed so the player can stand on them, but they should
                 * not act like walls for the Light Burst.
                 *
                 * GetComponentInParent is used intentionally because the
                 * BoxCollider2D may live on a child GameObject while the
                 * BloomPlatform component remains on the prefab root.
                 */
                bool belongsToBloomPlatform =
                    hit.collider.GetComponentInParent<BloomPlatform>() != null;

                if (
                    isBurstPassThrough ||
                    belongsToBloomPlatform
                )
                {
                    continue;
                }

                blockingHit =
                    hit;

                foundBlockingHit =
                    true;

                break;
            }

            OcclusionSample sample =
                new OcclusionSample();

            sample.direction =
                direction;

            if (foundBlockingHit)
            {
                sample.blocked =
                    true;

                sample.hitDistance =
                    Mathf.Min(
                        currentRadius,
                        blockingHit.distance +
                        occlusionOverlap
                    );
            }
            else
            {
                /*
                 * If this direction contains only BurstPassThrough platforms,
                 * Bloom Platforms, or completely open space, no occlusion is
                 * generated. The circular Burst therefore continues naturally
                 * in that direction.
                 */
                sample.blocked =
                    false;

                sample.hitDistance =
                    currentRadius;
            }

            samples[i] =
                sample;
        }

        for (
            int i = 0;
            i < rayCount;
            i++
        )
        {
            OcclusionSample first =
                samples[i];

            OcclusionSample second =
                samples[i + 1];

            /*
             * Cover geometry is created only when both neighbouring samples find
             * genuine blockers. This avoids bridging large open gaps with a single
             * triangle and keeps platform pass-through regions visually open.
             */
            if (
                !first.blocked ||
                !second.blocked
            )
            {
                continue;
            }

            Vector2 firstInnerWorld =
                worldOrigin +
                first.direction *
                first.hitDistance;

            Vector2 firstOuterWorld =
                worldOrigin +
                first.direction *
                currentRadius;

            Vector2 secondInnerWorld =
                worldOrigin +
                second.direction *
                second.hitDistance;

            Vector2 secondOuterWorld =
                worldOrigin +
                second.direction *
                currentRadius;

            int baseIndex =
                vertices.Count;

            vertices.Add(
                transform.InverseTransformPoint(
                    firstInnerWorld
                )
            );

            vertices.Add(
                transform.InverseTransformPoint(
                    firstOuterWorld
                )
            );

            vertices.Add(
                transform.InverseTransformPoint(
                    secondInnerWorld
                )
            );

            vertices.Add(
                transform.InverseTransformPoint(
                    secondOuterWorld
                )
            );

            // Two triangles form the small quad that redraws the environment
            // from the blocking surface out to the current Burst edge.
            triangles.Add(
                baseIndex
            );

            triangles.Add(
                baseIndex + 3
            );

            triangles.Add(
                baseIndex + 1
            );

            triangles.Add(
                baseIndex
            );

            triangles.Add(
                baseIndex + 2
            );

            triangles.Add(
                baseIndex + 3
            );
        }

        occlusionMesh.Clear();

        occlusionMesh.SetVertices(
            vertices
        );

        occlusionMesh.SetTriangles(
            triangles,
            0
        );

        // The occlusion shape changes every frame while the Burst expands, so
        // Unity needs refreshed bounds for correct camera culling.
        occlusionMesh.RecalculateBounds();
        occlusionMesh.RecalculateNormals();
    }

    private void OnValidate()
    {
        // A safe minimum prevents very coarse angular sampling from creating
        // large visible gaps around genuine wall and floor boundaries.
        rayCount =
            Mathf.Max(
                16,
                rayCount
            );

        occlusionOverlap =
            Mathf.Max(
                0f,
                occlusionOverlap
            );
    }
}