using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LightBurstEdgeSpill : MonoBehaviour
{
    [Header("Burst Reference")]
    [SerializeField] private LightBurstController lightBurstController;

    [Header("Collision")]
    [SerializeField] private LayerMask wallLayer;

    [Header("Edge Detection")]
    [SerializeField] private int rayCount = 96;

    // The generated spill begins slightly outside the collider so the ribbon is
    // not hidden inside the platform or wall it is wrapping around.
    [SerializeField] private float edgeSurfaceOffset = 0.02f;

    [Header("Spill Shape")]

    // The spill should remain a short extension around exposed geometry edges
    // rather than behaving like a second full Light Burst.
    [SerializeField] private float spillLength = 0.75f;

    // This controls how strongly the spill bends behind the blocking surface.
    // A larger value makes light wrap further around the corner.
    [SerializeField] private float spillBend = 0.5f;

    // More segments make the curved ribbon smoother without changing the
    // behaviour of the main Burst mesh.
    [Range(4, 20)]
    [SerializeField] private int spillSegments = 10;

    [SerializeField] private float spillRingThickness = 0.15f;

    [Header("Spill Glow")]

    // The glow extends behind the bright spill ribbon so it resembles the
    // existing Burst rather than appearing as an isolated solid white strip.
    [SerializeField] private float spillGlowDistance = 0.55f;

    [Range(0f, 1f)]
    [SerializeField] private float nearGlowAlpha = 0.14f;

    [Range(0f, 1f)]
    [SerializeField] private float middleGlowAlpha = 0.06f;

    [Range(0f, 1f)]
    [SerializeField] private float faintGlowAlpha = 0.015f;

    [Header("Debug")]
    [SerializeField] private bool showSpillDebug = false;

    private Mesh spillMesh;
    private MeshFilter meshFilter;

    private const int VerticesPerPoint = 6;

    private struct EdgeRay
    {
        public Vector2 direction;
        public bool blocked;
        public RaycastHit2D hit;
    }

    private struct SpillEdge
    {
        public Vector2 point;
        public Vector2 surfaceNormal;
        public Vector2 openDirection;
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

        Debug.Log(
            "LightBurstEdgeSpill enabled. Spill mesh ready: " +
            (spillMesh != null)
        );
    }

    private void FindReferences()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (lightBurstController == null)
        {
            // The spill lives underneath the same Player hierarchy as the main
            // Burst, so searching upward keeps both visuals using one controller.
            lightBurstController =
                GetComponentInParent<LightBurstController>();
        }

        if (lightBurstController == null)
        {
            Debug.LogError(
                "LightBurstEdgeSpill could not find LightBurstController."
            );
        }
    }

    private void EnsureMeshExists()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (spillMesh == null)
        {
            spillMesh = new Mesh();
            spillMesh.name = "Light Burst Edge Spill Mesh";

            meshFilter.sharedMesh = spillMesh;
        }
        else if (meshFilter.sharedMesh != spillMesh)
        {
            meshFilter.sharedMesh = spillMesh;
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
            if (spillMesh != null)
            {
                spillMesh.Clear();
            }

            return;
        }

        UpdateSpillMesh(
            lightBurstController.GetCurrentBurstRadius()
        );
    }

    private void UpdateSpillMesh(float currentRadius)
    {
        EnsureMeshExists();

        Vector2 origin =
            lightBurstController.transform.position;

        List<SpillEdge> edges =
            FindExposedEdges(
                origin,
                currentRadius
            );

        BuildSpillGeometry(
            edges
        );
    }

    private List<SpillEdge> FindExposedEdges(
        Vector2 origin,
        float currentRadius
    )
    {
        List<SpillEdge> edges =
            new List<SpillEdge>();

        EdgeRay[] rays =
            new EdgeRay[rayCount];

        for (int i = 0; i < rayCount; i++)
        {
            float angle =
                ((float)i / rayCount) *
                Mathf.PI *
                2f;

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                );

            RaycastHit2D hit =
                Physics2D.Raycast(
                    origin,
                    direction,
                    currentRadius,
                    wallLayer
                );

            rays[i] =
                new EdgeRay
                {
                    direction = direction,
                    blocked = hit.collider != null,
                    hit = hit
                };
        }

        for (int i = 0; i < rayCount; i++)
        {
            int nextIndex =
                (i + 1) % rayCount;

            EdgeRay current =
                rays[i];

            EdgeRay next =
                rays[nextIndex];

            // A blocked/open transition means we have reached an exposed end of
            // geometry where light has somewhere to continue around the corner.
            if (current.blocked == next.blocked)
            {
                continue;
            }

            EdgeRay blockedRay =
                current.blocked
                    ? current
                    : next;

            EdgeRay openRay =
                current.blocked
                    ? next
                    : current;

            if (blockedRay.hit.collider == null)
            {
                continue;
            }

            SpillEdge edge =
                new SpillEdge
                {
                    point =
                        blockedRay.hit.point +
                        blockedRay.hit.normal *
                        edgeSurfaceOffset,

                    surfaceNormal =
                        blockedRay.hit.normal.normalized,

                    openDirection =
                        openRay.direction.normalized
                };

            edges.Add(edge);

            if (showSpillDebug)
            {
                // Cyan confirms the obstacle edge that produced a spill.
                Debug.DrawLine(
                    origin,
                    edge.point,
                    Color.cyan
                );
            }
        }

        return edges;
    }

    private void BuildSpillGeometry(
        List<SpillEdge> edges
    )
    {
        if (spillMesh == null)
        {
            return;
        }

        if (edges.Count == 0)
        {
            spillMesh.Clear();
            return;
        }

        int pointsPerSpill =
            spillSegments + 1;

        int verticesPerSpill =
            pointsPerSpill *
            VerticesPerPoint;

        int indicesPerSegment =
            5 * 6;

        Vector3[] vertices =
            new Vector3[
                edges.Count *
                verticesPerSpill
            ];

        Vector2[] uvs =
            new Vector2[
                vertices.Length
            ];

        Color[] colours =
            new Color[
                vertices.Length
            ];

        int[] triangles =
            new int[
                edges.Count *
                spillSegments *
                indicesPerSegment
            ];

        int triangleIndex = 0;

        for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            SpillEdge edge =
                edges[edgeIndex];

            /*
             * The open direction points towards the side where the normal Burst
             * ray was able to continue. The opposite surface normal points behind
             * the blocker. Blending between them creates the desired corner spill.
             *
             * Platform underneath:
             *     open → outward
             *     behind → downward
             *
             * Platform overhead:
             *     open → outward
             *     behind → upward
             */
            Vector2 behindSurface =
                -edge.surfaceNormal;

            int spillVertexStart =
                edgeIndex *
                verticesPerSpill;

            Vector2 previousPathPoint =
                edge.point;

            for (int segment = 0; segment <= spillSegments; segment++)
            {
                float progress =
                    (float)segment /
                    spillSegments;

                float smoothProgress =
                    SmoothProgress(progress);

                /*
                 * The centre of the ribbon follows a curved path instead of
                 * creating a second radial fan. This is the key difference from
                 * the previous version that collapsed into a white edge marker.
                 */
                Vector2 outwardOffset =
                    edge.openDirection *
                    spillLength *
                    smoothProgress;

                Vector2 bendOffset =
                    behindSurface *
                    spillBend *
                    smoothProgress *
                    smoothProgress;

                Vector2 pathPoint =
                    edge.point +
                    outwardOffset +
                    bendOffset;

                Vector2 pathDirection;

                if (segment == 0)
                {
                    pathDirection =
                        (
                            edge.openDirection +
                            behindSurface * 0.2f
                        ).normalized;
                }
                else
                {
                    pathDirection =
                        (
                            pathPoint -
                            previousPathPoint
                        ).normalized;
                }

                /*
                 * The ribbon width is perpendicular to the direction in which the
                 * light is travelling. This prevents all vertices from collapsing
                 * into the same point at the start of the spill.
                 */
                Vector2 ribbonNormal =
                    new Vector2(
                        -pathDirection.y,
                        pathDirection.x
                    ).normalized;

                float halfRingThickness =
                    spillRingThickness * 0.5f;

                Vector2 ringOuterPoint =
                    pathPoint +
                    ribbonNormal *
                    halfRingThickness;

                Vector2 ringInnerPoint =
                    pathPoint -
                    ribbonNormal *
                    halfRingThickness;

                Vector2 nearGlowPoint =
                    ringInnerPoint -
                    ribbonNormal *
                    (spillGlowDistance * 0.12f);

                Vector2 middleGlowPoint =
                    ringInnerPoint -
                    ribbonNormal *
                    (spillGlowDistance * 0.35f);

                Vector2 faintGlowPoint =
                    ringInnerPoint -
                    ribbonNormal *
                    (spillGlowDistance * 0.65f);

                Vector2 transparentPoint =
                    ringInnerPoint -
                    ribbonNormal *
                    spillGlowDistance;

                int baseIndex =
                    spillVertexStart +
                    segment *
                    VerticesPerPoint;

                vertices[baseIndex] =
                    transform.InverseTransformPoint(
                        transparentPoint
                    );

                vertices[baseIndex + 1] =
                    transform.InverseTransformPoint(
                        faintGlowPoint
                    );

                vertices[baseIndex + 2] =
                    transform.InverseTransformPoint(
                        middleGlowPoint
                    );

                vertices[baseIndex + 3] =
                    transform.InverseTransformPoint(
                        nearGlowPoint
                    );

                vertices[baseIndex + 4] =
                    transform.InverseTransformPoint(
                        ringInnerPoint
                    );

                vertices[baseIndex + 5] =
                    transform.InverseTransformPoint(
                        ringOuterPoint
                    );

                uvs[baseIndex] =
                    new Vector2(progress, 0f);

                uvs[baseIndex + 1] =
                    new Vector2(progress, 0.2f);

                uvs[baseIndex + 2] =
                    new Vector2(progress, 0.4f);

                uvs[baseIndex + 3] =
                    new Vector2(progress, 0.6f);

                uvs[baseIndex + 4] =
                    new Vector2(progress, 0.8f);

                uvs[baseIndex + 5] =
                    new Vector2(progress, 1f);

                colours[baseIndex] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        0f
                    );

                colours[baseIndex + 1] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        faintGlowAlpha
                    );

                colours[baseIndex + 2] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        middleGlowAlpha
                    );

                colours[baseIndex + 3] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        nearGlowAlpha
                    );

                colours[baseIndex + 4] =
                    Color.white;

                colours[baseIndex + 5] =
                    Color.white;

                if (showSpillDebug && segment > 0)
                {
                    // Yellow shows the actual curved centre path the spill mesh
                    // should follow around the obstacle.
                    Debug.DrawLine(
                        previousPathPoint,
                        pathPoint,
                        Color.yellow
                    );
                }

                previousPathPoint =
                    pathPoint;
            }

            for (int segment = 0; segment < spillSegments; segment++)
            {
                int currentBase =
                    spillVertexStart +
                    segment *
                    VerticesPerPoint;

                int nextBase =
                    currentBase +
                    VerticesPerPoint;

                for (int band = 0; band < 5; band++)
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

                    // The same camera-facing winding as the main Burst keeps the
                    // generated spill visible in the 2D Game view.
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

        spillMesh.Clear();

        spillMesh.vertices =
            vertices;

        spillMesh.uv =
            uvs;

        spillMesh.colors =
            colours;

        spillMesh.triangles =
            triangles;

        spillMesh.RecalculateBounds();
        spillMesh.RecalculateNormals();
    }

    private float SmoothProgress(
        float progress
    )
    {
        // Smoothing keeps the spill attached to the Burst edge before gradually
        // curving it around the obstacle rather than producing a sharp kink.
        return progress *
               progress *
               (3f - 2f * progress);
    }

    private void OnValidate()
    {
        rayCount =
            Mathf.Max(
                8,
                rayCount
            );

        spillSegments =
            Mathf.Clamp(
                spillSegments,
                4,
                20
            );

        edgeSurfaceOffset =
            Mathf.Max(
                0f,
                edgeSurfaceOffset
            );

        spillLength =
            Mathf.Max(
                0.01f,
                spillLength
            );

        spillBend =
            Mathf.Max(
                0f,
                spillBend
            );

        spillRingThickness =
            Mathf.Max(
                0.01f,
                spillRingThickness
            );

        spillGlowDistance =
            Mathf.Max(
                0.01f,
                spillGlowDistance
            );
    }
}