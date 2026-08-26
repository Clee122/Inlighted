using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LightBurstOcclusionMesh : MonoBehaviour
{
    [Header("Burst Reference")]
    [SerializeField] private LightBurstController lightBurstController;

    [Header("Wall Detection")]

    // The occlusion mesh only reacts to level geometry on this layer.
    // Keeping this separate from the main Burst visual means walls can hide
    // portions of the effect without deforming the circular Burst itself.
    [SerializeField] private LayerMask wallLayer;

    // A higher ray count gives the cover more precision around platform edges.
    // We are using a reasonably high default because this mesh only exists
    // while Light Burst is active.
    [SerializeField] private int rayCount = 128;

    // The cover begins slightly beyond the collision point so it does not
    // accidentally hide the visible contact edge of the Burst.
    [SerializeField] private float occlusionOverlap = 0.03f;

    [Header("Rendering")]

    // The occlusion mesh must draw above the main Burst during this debug stage
    // so the black material clearly shows which part of the Burst is being covered.
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 11;

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
        // Clearing the generated geometry prevents the previous Burst shape
        // from briefly appearing when this object is enabled again later.
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
            // Explicit sorting ensures the debug cover renders above the
            // circular Burst while we verify the occlusion geometry.
            meshRenderer.sortingLayerName =
                sortingLayerName;

            meshRenderer.sortingOrder =
                sortingOrder;
        }

        if (lightBurstController == null)
        {
            // This object is expected to be a child of Player, so searching
            // upwards avoids requiring a manually maintained reference.
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
        // enable/disable cycles, so reconnect it if necessary.
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
         * Each ray records whether it encounters solid geometry before reaching
         * the outer Burst radius. The cover is generated only between adjacent
         * rays that are both blocked, which avoids filling completely open gaps.
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

            RaycastHit2D hit =
                Physics2D.Raycast(
                    worldOrigin,
                    direction,
                    currentRadius,
                    wallLayer
                );

            OcclusionSample sample =
                new OcclusionSample();

            sample.direction =
                direction;

            if (hit.collider != null)
            {
                sample.blocked =
                    true;

                sample.hitDistance =
                    Mathf.Min(
                        currentRadius,
                        hit.distance +
                        occlusionOverlap
                    );
            }
            else
            {
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
             * A cover section is created only when both neighbouring rays hit
             * geometry. This keeps open spaces and gaps uncovered instead of
             * bridging across them with large triangles.
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

            // Two triangles form the small quad covering the region beyond the
            // obstacle between these neighbouring radial samples.
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

        // The cover changes continuously as the Burst expands, so its bounds
        // must be refreshed for Unity's camera culling.
        occlusionMesh.RecalculateBounds();
        occlusionMesh.RecalculateNormals();
    }

    private void OnValidate()
    {
        // A reasonable minimum prevents extremely coarse geometry from creating
        // large visible gaps around walls and platform edges.
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