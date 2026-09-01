using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LightBurstWallMesh : MonoBehaviour
{
    [Header("Burst Reference")]
    [SerializeField] private LightBurstController lightBurstController;

    [Header("Rendering")]

    // The Burst uses a MeshRenderer rather than a SpriteRenderer, so its
    // 2D sorting position is assigned explicitly to keep its position in the
    // environment predictable while we build the separate occlusion system.
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    [Header("Ring Shape")]

    // The visual no longer needs adaptive wall sampling because geometry will
    // not alter the main Burst shape. This value simply controls how smooth
    // the circular generated mesh appears.
    [SerializeField] private int rayCount = 96;

    // The bright white outer ring remains separate from the softer inner glow
    // so its thickness can still be tuned without changing the Shader Graph.
    [SerializeField] private float ringThickness = 0.15f;

    [Header("Inner Glow")]

    // The mesh extends inward by this distance so the existing Shader Graph
    // still has enough UV range to create the same continuous inner glow.
    [SerializeField] private float innerGlowDistance = 3f;

    private Mesh burstMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private Color[] colours;
    private int[] triangles;

    private const int VerticesPerRay = 6;

    private void Awake()
    {
        FindReferences();

        // Preparing the generated mesh before the first Burst prevents mesh
        // creation from happening at the same moment the ability activates.
        EnsureMeshExists();
    }

    private void OnEnable()
    {
        FindReferences();

        // BurstWallMesh is disabled between ability uses, so the runtime mesh
        // is checked whenever the visual becomes active again.
        EnsureMeshExists();
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
            // Explicit sorting keeps the generated mesh consistent with the
            // project's 2D render order despite using a normal MeshRenderer.
            meshRenderer.sortingLayerName =
                sortingLayerName;

            meshRenderer.sortingOrder =
                sortingOrder;
        }

        if (lightBurstController == null)
        {
            // BurstWallMesh normally sits underneath Player, so searching
            // upwards avoids requiring another manually maintained reference.
            lightBurstController =
                GetComponentInParent<LightBurstController>();
        }

        if (lightBurstController == null)
        {
            Debug.LogError(
                "LightBurstWallMesh could not find LightBurstController. " +
                "The Burst visual cannot follow the gameplay radius."
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

        if (burstMesh == null)
        {
            burstMesh =
                new Mesh();

            burstMesh.name =
                "Light Burst Wall Mesh";

            meshFilter.sharedMesh =
                burstMesh;

            return;
        }

        // Runtime-generated meshes can lose their MeshFilter assignment after
        // enable/disable cycles, so reconnecting it keeps repeated Burst uses safe.
        if (
            meshFilter != null &&
            meshFilter.sharedMesh != burstMesh
        )
        {
            meshFilter.sharedMesh =
                burstMesh;
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
            return;
        }

        // The visual remains synchronised with the gameplay ability by using
        // the same live radius exposed by LightBurstController.
        UpdateBurstMesh(
            lightBurstController.GetCurrentBurstRadius()
        );
    }

    private void UpdateBurstMesh(
        float currentRadius
    )
    {
        EnsureMeshExists();

        if (burstMesh == null)
        {
            return;
        }

        Vector2 worldOrigin =
            transform.position;

        /*
         * The main Burst deliberately ignores every wall, floor, ceiling and
         * platform. Every radial sample reaches the same current radius so the
         * mesh can never form the sharp blocked/open triangles seen previously.
         *
         * A separate occlusion mesh will later decide which parts should be
         * visually hidden behind level geometry.
         */
        int sampleCount =
            rayCount + 1;

        BuildMeshArrays(
            sampleCount
        );

        for (
            int i = 0;
            i < sampleCount;
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

            BuildRayVertices(
                i,
                angle,
                direction,
                currentRadius,
                worldOrigin
            );
        }

        BuildTriangles(
            sampleCount
        );

        burstMesh.Clear();

        burstMesh.vertices =
            vertices;

        burstMesh.uv =
            uvs;

        burstMesh.colors =
            colours;

        burstMesh.triangles =
            triangles;

        // The generated circle changes size during expansion, so its bounds
        // must be refreshed for correct camera culling.
        burstMesh.RecalculateBounds();
        burstMesh.RecalculateNormals();
    }

    private void BuildMeshArrays(
        int sampleCount
    )
    {
        int vertexCount =
            sampleCount *
            VerticesPerRay;

        vertices =
            new Vector3[
                vertexCount
            ];

        uvs =
            new Vector2[
                vertexCount
            ];

        colours =
            new Color[
                vertexCount
            ];

        int connectionCount =
            sampleCount - 1;

        // Every neighbouring sample still contains five radial bands because
        // the existing Shader Graph depends on their UV interpolation.
        triangles =
            new int[
                connectionCount *
                5 *
                6
            ];
    }

    private void BuildRayVertices(
        int rayIndex,
        float angle,
        Vector2 direction,
        float currentRadius,
        Vector2 worldOrigin
    )
    {
        // Every outer point now uses exactly the same current radius, which is
        // what guarantees a circular Burst regardless of nearby geometry.
        float outerDistance =
            currentRadius;

        float ringInnerDistance =
            Mathf.Max(
                0f,
                outerDistance -
                ringThickness
            );

        // The same inward glow structure is retained so the existing material
        // keeps the appearance already tuned in Shader Graph.
        float glowStartDistance =
            Mathf.Max(
                0f,
                ringInnerDistance -
                innerGlowDistance
            );

        float faintDistance =
            Mathf.Lerp(
                glowStartDistance,
                ringInnerDistance,
                0.35f
            );

        float middleDistance =
            Mathf.Lerp(
                glowStartDistance,
                ringInnerDistance,
                0.65f
            );

        float nearDistance =
            Mathf.Lerp(
                glowStartDistance,
                ringInnerDistance,
                0.88f
            );

        Vector2 transparentWorld =
            worldOrigin +
            direction *
            glowStartDistance;

        Vector2 faintWorld =
            worldOrigin +
            direction *
            faintDistance;

        Vector2 middleWorld =
            worldOrigin +
            direction *
            middleDistance;

        Vector2 nearWorld =
            worldOrigin +
            direction *
            nearDistance;

        Vector2 ringInnerWorld =
            worldOrigin +
            direction *
            ringInnerDistance;

        Vector2 ringOuterWorld =
            worldOrigin +
            direction *
            outerDistance;

        int baseIndex =
            rayIndex *
            VerticesPerRay;

        vertices[baseIndex] =
            transform.InverseTransformPoint(
                transparentWorld
            );

        vertices[baseIndex + 1] =
            transform.InverseTransformPoint(
                faintWorld
            );

        vertices[baseIndex + 2] =
            transform.InverseTransformPoint(
                middleWorld
            );

        vertices[baseIndex + 3] =
            transform.InverseTransformPoint(
                nearWorld
            );

        vertices[baseIndex + 4] =
            transform.InverseTransformPoint(
                ringInnerWorld
            );

        vertices[baseIndex + 5] =
            transform.InverseTransformPoint(
                ringOuterWorld
            );

        float normalisedAngle =
            Mathf.Repeat(
                angle /
                (Mathf.PI * 2f),
                1f
            );

        // UV Y keeps the exact same radial layout as before so the current
        // Shader Graph can continue controlling the glow and outer fade.
        uvs[baseIndex] =
            new Vector2(
                normalisedAngle,
                0f
            );

        uvs[baseIndex + 1] =
            new Vector2(
                normalisedAngle,
                0.2f
            );

        uvs[baseIndex + 2] =
            new Vector2(
                normalisedAngle,
                0.4f
            );

        uvs[baseIndex + 3] =
            new Vector2(
                normalisedAngle,
                0.6f
            );

        uvs[baseIndex + 4] =
            new Vector2(
                normalisedAngle,
                0.8f
            );

        uvs[baseIndex + 5] =
            new Vector2(
                normalisedAngle,
                1f
            );

        // Vertex colours remain neutral because the Shader Graph controls
        // transparency through the generated UV gradient.
        for (
            int i = 0;
            i < VerticesPerRay;
            i++
        )
        {
            colours[baseIndex + i] =
                Color.white;
        }
    }

    private void BuildTriangles(
        int sampleCount
    )
    {
        int triangleIndex = 0;

        for (
            int i = 0;
            i < sampleCount - 1;
            i++
        )
        {
            int currentBase =
                i *
                VerticesPerRay;

            int nextBase =
                (i + 1) *
                VerticesPerRay;

            for (
                int band = 0;
                band < 5;
                band++
            )
            {
                int currentInner =
                    currentBase +
                    band;

                int currentOuter =
                    currentBase +
                    band +
                    1;

                int nextInner =
                    nextBase +
                    band;

                int nextOuter =
                    nextBase +
                    band +
                    1;

                // The original triangle winding is retained because it already
                // faces the project's 2D camera correctly.
                triangles[triangleIndex++] =
                    currentInner;

                triangles[triangleIndex++] =
                    nextOuter;

                triangles[triangleIndex++] =
                    currentOuter;

                triangles[triangleIndex++] =
                    currentInner;

                triangles[triangleIndex++] =
                    nextInner;

                triangles[triangleIndex++] =
                    nextOuter;
            }
        }
    }

    private void OnValidate()
    {
        // Safe minimums prevent Inspector values from creating invalid or
        // degenerate circular mesh geometry.
        rayCount =
            Mathf.Max(
                8,
                rayCount
            );

        ringThickness =
            Mathf.Max(
                0.01f,
                ringThickness
            );

        innerGlowDistance =
            Mathf.Max(
                0.01f,
                innerGlowDistance
            );
    }
}