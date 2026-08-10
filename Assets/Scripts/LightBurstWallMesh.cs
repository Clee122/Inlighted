using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LightBurstWallMesh : MonoBehaviour
{
    [Header("Burst Reference")]
    [SerializeField] private LightBurstController lightBurstController;

    [Header("Wall Detection")]
    [SerializeField] private LayerMask wallLayer;

    // A tiny overlap prevents visible seams where the Burst meets level
    // geometry without allowing the effect to noticeably pass through it.
    [SerializeField] private float wallOverlap = 0.03f;

    [Header("Rendering")]

    // The Burst uses a normal MeshRenderer rather than a SpriteRenderer, so its
    // 2D sorting position is assigned explicitly to keep it above environment
    // artwork that would otherwise draw in front of the generated mesh.
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    [Header("Ring Shape")]

    // The main ray count provides the general circular shape while adaptive
    // refinement adds extra precision only around obstacle edges.
    [SerializeField] private int rayCount = 96;

    // The bright white outer ring is kept separate from the softer inner glow
    // so its thickness can be tuned without altering the glow distance.
    [SerializeField] private float ringThickness = 0.15f;

    [Header("Adaptive Edge Refinement")]

    // Extra rays are added around changes between blocked and open space so
    // corners are represented more accurately without increasing every ray.
    [Range(0, 6)]
    [SerializeField] private int edgeRefinementDepth = 4;

    // A large difference between neighbouring hit distances usually represents
    // a sharp corner and therefore triggers additional adaptive samples.
    [SerializeField] private float edgeDistanceThreshold = 0.2f;

    [Header("Inner Glow")]

    // The mesh extends inward by this distance so the Shader Graph has enough
    // geometry and UV range to create one continuous glow towards the player.
    [SerializeField] private float innerGlowDistance = 3f;

    private Mesh burstMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private Color[] colours;
    private int[] triangles;

    private const int VerticesPerRay = 6;

    /*
     * Each radial sample remembers what it hit and how far the Burst could
     * travel. This information is used to refine obstacle boundaries while
     * keeping every floor, wall and ceiling as a genuine blocker.
     */
    private struct RaySample
    {
        public float angle;
        public Vector2 direction;
        public float distance;

        public bool blocked;
        public Collider2D collider;
    }

    private void Awake()
    {
        FindReferences();

        // Preparing the generated mesh before the first ability use prevents
        // mesh creation from happening at the moment the Burst is activated.
        EnsureMeshExists();
    }

    private void OnEnable()
    {
        FindReferences();

        // BurstWallMesh is disabled between ability uses, so its runtime mesh
        // reference is checked whenever the visual becomes active again.
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
            // The Burst needs an explicit 2D sorting position because its
            // MeshRenderer does not expose the same convenient sorting controls
            // as SpriteRenderers and TilemapRenderers in this Inspector setup.
            meshRenderer.sortingLayerName =
                sortingLayerName;

            meshRenderer.sortingOrder =
                sortingOrder;
        }

        if (lightBurstController == null)
        {
            // BurstWallMesh normally sits underneath Player, so searching
            // upwards avoids needing another manually maintained reference.
            lightBurstController =
                GetComponentInParent<LightBurstController>();
        }

        if (lightBurstController == null)
        {
            Debug.LogError(
                "LightBurstWallMesh could not find LightBurstController. " +
                "The wall-aware Burst visual cannot update."
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
        // enable/disable cycles, so reconnect it if Unity drops the reference.
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

        // The visual uses the gameplay Burst's current radius so its expansion
        // remains synchronised with the ability's actual effective range.
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

        List<RaySample> samples =
            BuildAdaptiveRaySamples(
                worldOrigin,
                currentRadius
            );

        BuildMeshArrays(
            samples.Count
        );

        for (
            int i = 0;
            i < samples.Count;
            i++
        )
        {
            BuildRayVertices(
                i,
                samples[i],
                worldOrigin
            );
        }

        BuildTriangles(
            samples.Count
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

        // The mesh continuously changes shape as the Burst expands against
        // geometry, so its bounds need refreshing for correct camera culling.
        burstMesh.RecalculateBounds();
        burstMesh.RecalculateNormals();
    }

    private List<RaySample> BuildAdaptiveRaySamples(
        Vector2 origin,
        float currentRadius
    )
    {
        List<RaySample> finalSamples =
            new List<RaySample>();

        /*
         * Starting at zero degrees and repeating that direction at 360 degrees
         * closes the generated circular mesh without special end triangles.
         */
        RaySample firstSample =
            CastBurstRay(
                origin,
                0f,
                currentRadius
            );

        finalSamples.Add(
            firstSample
        );

        RaySample previousSample =
            firstSample;

        for (
            int i = 1;
            i <= rayCount;
            i++
        )
        {
            float angle =
                ((float)i / rayCount) *
                Mathf.PI *
                2f;

            RaySample nextSample =
                CastBurstRay(
                    origin,
                    angle,
                    currentRadius
                );

            AddRefinedSamples(
                previousSample,
                nextSample,
                origin,
                currentRadius,
                edgeRefinementDepth,
                finalSamples
            );

            previousSample =
                nextSample;
        }

        return finalSamples;
    }

    private void AddRefinedSamples(
        RaySample start,
        RaySample end,
        Vector2 origin,
        float currentRadius,
        int remainingDepth,
        List<RaySample> output
    )
    {
        if (remainingDepth <= 0)
        {
            output.Add(
                end
            );

            return;
        }

        float middleAngle =
            (start.angle + end.angle) *
            0.5f;

        RaySample middle =
            CastBurstRay(
                origin,
                middleAngle,
                currentRadius
            );

        bool needsRefinement =
            SamplesNeedRefinement(
                start,
                middle
            ) ||
            SamplesNeedRefinement(
                middle,
                end
            );

        if (!needsRefinement)
        {
            output.Add(
                end
            );

            return;
        }

        // Only the angular region containing a likely geometry edge is split
        // further, preserving good accuracy without excessive raycasts elsewhere.
        AddRefinedSamples(
            start,
            middle,
            origin,
            currentRadius,
            remainingDepth - 1,
            output
        );

        AddRefinedSamples(
            middle,
            end,
            origin,
            currentRadius,
            remainingDepth - 1,
            output
        );
    }

    private bool SamplesNeedRefinement(
        RaySample first,
        RaySample second
    )
    {
        // A blocked/open transition represents the edge of geometry and benefits
        // from more angular precision.
        if (
            first.blocked !=
            second.blocked
        )
        {
            return true;
        }

        // Different colliders can represent a boundary between separate pieces
        // of level geometry and therefore deserve additional sampling.
        if (
            first.blocked &&
            second.blocked &&
            first.collider !=
            second.collider
        )
        {
            return true;
        }

        // A sudden distance change can reveal a corner even when both rays hit
        // the same collider.
        if (
            Mathf.Abs(
                first.distance -
                second.distance
            ) >
            edgeDistanceThreshold
        )
        {
            return true;
        }

        return false;
    }

    private RaySample CastBurstRay(
        Vector2 origin,
        float angle,
        float currentRadius
    )
    {
        Vector2 direction =
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ).normalized;

        RaycastHit2D hit =
            Physics2D.Raycast(
                origin,
                direction,
                currentRadius,
                wallLayer
            );

        RaySample sample =
            new RaySample();

        sample.angle =
            angle;

        sample.direction =
            direction;

        if (hit.collider != null)
        {
            sample.blocked =
                true;

            sample.collider =
                hit.collider;

            // Every Ground-layer collider is authoritative: the visible Burst
            // stops at the first floor, platform, ceiling or wall it encounters.
            sample.distance =
                Mathf.Min(
                    currentRadius,
                    hit.distance +
                    wallOverlap
                );
        }
        else
        {
            sample.blocked =
                false;

            sample.collider =
                null;

            // Rays travelling through genuinely open space reach the full radius.
            sample.distance =
                currentRadius;
        }

        return sample;
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

        // Every neighbouring sample has five connected radial sections. The UVs
        // across these sections allow the Shader Graph to create a continuous
        // inward glow while keeping the outer ring sharply defined.
        triangles =
            new int[
                connectionCount *
                5 *
                6
            ];
    }

    private void BuildRayVertices(
        int rayIndex,
        RaySample sample,
        Vector2 worldOrigin
    )
    {
        float outerDistance =
            sample.distance;

        float ringInnerDistance =
            Mathf.Max(
                0f,
                outerDistance -
                ringThickness
            );

        // The glow begins inward from the same collision boundary as the white
        // ring so it cannot independently extend through blocked geometry.
        float glowStartDistance =
            Mathf.Max(
                0f,
                ringInnerDistance -
                innerGlowDistance
            );

        // Intermediate positions remain in the mesh because their UV values
        // provide enough geometry for the shader to interpolate a smooth glow.
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
            sample.direction *
            glowStartDistance;

        Vector2 faintWorld =
            worldOrigin +
            sample.direction *
            faintDistance;

        Vector2 middleWorld =
            worldOrigin +
            sample.direction *
            middleDistance;

        Vector2 nearWorld =
            worldOrigin +
            sample.direction *
            nearDistance;

        Vector2 ringInnerWorld =
            worldOrigin +
            sample.direction *
            ringInnerDistance;

        Vector2 ringOuterWorld =
            worldOrigin +
            sample.direction *
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
                sample.angle /
                (Mathf.PI * 2f),
                1f
            );

        // UV Y now describes the radial position from the inner transparent
        // boundary to the bright outer edge. The Shader Graph uses this smoothly
        // interpolated value instead of relying on several separate alpha bands.
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

        // Vertex colours no longer control the glow strength. Keeping them fully
        // white allows the Shader Graph's UV-based gradient to control transparency
        // consistently across the entire generated Burst mesh.
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

                // This winding faces the project's 2D camera so the generated
                // mesh remains visible in Game view.
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
        // Safe minimums prevent accidental Inspector values from generating
        // invalid or degenerate mesh geometry.
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

        wallOverlap =
            Mathf.Max(
                0f,
                wallOverlap
            );

        edgeDistanceThreshold =
            Mathf.Max(
                0.01f,
                edgeDistanceThreshold
            );

        edgeRefinementDepth =
            Mathf.Clamp(
                edgeRefinementDepth,
                0,
                6
            );
    }
}