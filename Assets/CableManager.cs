using UnityEngine;
using System.Collections.Generic;

public enum CableMode { Static, Physics }
public enum CableGroup { None, Group1, Group2, Group3, Group4, Group5, Group6 }

[ExecuteAlways] // run during edit mode
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))] // object must haves

public class CableManager : MonoBehaviour
{
    public CableMode mode = CableMode.Static;
    public CableGroup group = CableGroup.None;

    public Vector3 startPoint;
    public Vector3 endPoint;

    // cable settings
    [Min(1)] public int segmentCount = 12;
    [Min(3)] public int ringSides = 8;
    [Min(0.001f)] public float cableRadius = 0.03f;

    // static sag settings
    [Range(0f, 8f)] public float sag = 0.2f;

    // physics settings
    [Tooltip("How much the cable drapes. Light -> Heavy")]
    [Range(0f, 20f)] public float gravity = 9.8f;

    [Tooltip("How quickly the cable settles. Quick -> Slow")]
    [Range(0.5f, 1f)] public float damping = 0.98f;

    [Tooltip("How rigid the cable is. Stretchy -> Stiff")]
    [Range(1, 30)] public int stiffness = 10;

    [SerializeField] private List<Vector3> cablePoints = new List<Vector3>(); // stores cable point chain

    // physics state
    Vector3[] positions;
    Vector3[] prevPositions;
    float segmentLength;

    private Collider[] collisionRange = new Collider[16]; // buffer for collision detection

    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<int> triangles = new List<int>();

    // private mesh references
    MeshFilter meshFilter; // mesh component
    Mesh cableMesh; // cable geometry

    // generates cable upon component creation
    void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();

        if (cableMesh == null)
        {
            cableMesh = new Mesh();
            cableMesh.name = "Cable Mesh";
        }

        meshFilter.sharedMesh = cableMesh; // object will render geometry placed into cableMesh

        // set default endpoints on first creation
        if (startPoint == Vector3.zero && endPoint == Vector3.zero)
        {
            startPoint = transform.position + Vector3.left;
            endPoint = transform.position + Vector3.right;
        }

        if (mode == CableMode.Physics)
            InitializePhysics();
        else
            Rebuild(); // generate cable
    }

    // constant refresh when values change
    void OnValidate()
    {
        if (!Application.isPlaying && mode == CableMode.Static)
            Rebuild();
    }

    // runs physics each frame
    void Update()
    {
        if (mode == CableMode.Physics)
            RunPhysics();
    }

    // builds the cable
    public void Rebuild()
    {
        if (startPoint == endPoint) return;
        BuildPointChain();
        BuildTubeMesh();
    }

    // rebuilds mesh from existing cablePoints (used by physics)
    public void RebuildMesh()
    {
        BuildTubeMesh();
    }

    // ==================== STATIC MODE ====================

    // generates a chain of points between start and end points
    void BuildPointChain()
    {
        cablePoints.Clear(); // remove any old points

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount; // divides line into equal segments
            Vector3 p = Vector3.Lerp(startPoint, endPoint, t);

            // sag
            float sagAmount = Mathf.Sin(t * Mathf.PI) * sag;
            p += Vector3.down * sagAmount;

            cablePoints.Add(transform.InverseTransformPoint(p)); // convert world space to local space and add to list
        }
    }

    // ==================== PHYSICS MODE ====================

    // set up particles evenly between start and end
    public void InitializePhysics()
    {
        if (startPoint == endPoint) return;

        int count = segmentCount + 1; // refers to chain points
        positions = new Vector3[count];
        prevPositions = new Vector3[count];

        // loops through each points and places them evenly between start and end
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)segmentCount;
            positions[i] = Vector3.Lerp(startPoint, endPoint, t);
            prevPositions[i] = positions[i]; // set prev pos to current pos = zero velocity at start
        }

        segmentLength = Vector3.Distance(startPoint, endPoint) / segmentCount; // calculates segment length
    }

    // runs one frame of physics
    void RunPhysics()
    {
        if (positions == null || positions.Length < 2) return;

        // eun physics pipeline
        Gravity();
        Collisions();
        Spacing();
        Collisions(); // extra collision after spacing to prevent tunneling
        UpdatePhysicsMesh();
    }

    // applies gravity and damping to all except fixed endpoints
    void Gravity()
    {
        float time = Mathf.Min(Time.deltaTime, 0.02f); // time since last frame capped to 0.02 for stability
        
        // loop through points except fixed endpoints
        for (int i = 1; i < positions.Length - 1; i++)
        {
            Vector3 velocity = (positions[i] - prevPositions[i]) * damping; // velocity = current - previous
            prevPositions[i] = positions[i]; // save current as prev for next frame
            positions[i] += velocity + Vector3.down * gravity * time * time; // apply velocity and gravity
        }
    }

    // handles collisions with others
    void Collisions()
    {
        for (int i = 1; i < positions.Length - 1; i++)
        {
            int hits = Physics.OverlapSphereNonAlloc(positions[i], cableRadius, collisionRange); // check for colliders within cable radius of point
            for (int j = 0; j < hits; j++)
            {
                Collider col = collisionRange[j];
                Vector3 closest = col.ClosestPoint(positions[i]); // get closest point on collider to cable point
                float dist = Vector3.Distance(positions[i], closest); // distance from cable point to closest point

                // particle is inside the collider
                if (dist < cableRadius)
                {
                    Vector3 pushDir = (positions[i] - closest).normalized; // direction to push cable point out of collider
                    if (pushDir.sqrMagnitude < 0.001f) // use an arbitrary push direction if too close
                        pushDir = Vector3.up;
                    positions[i] = closest + pushDir * cableRadius; // move cable point
                }
            }
        }
    }

    // enforces fixed particle distance
    void Spacing()
    {   // multi pass
        for (int i = 0; i < stiffness; i++)
        {
            for (int j = 0; j < positions.Length - 1; j++)
            {
                Vector3 space = positions[j + 1] - positions[j]; // vector from one point to the next
                float dist = space.magnitude;
                if (dist < 0.0001f) continue; // avoid division by zero

                float error = (dist - segmentLength) / dist; // how much the segment deviates from desired spacing
                Vector3 correction = space * 0.5f * error; // how much to move each point to correct spacing (half the error)

                // don't move pinned endpoints
                if (j != 0) // if j isnt the first point move it forward
                    positions[j] += correction;
                if (j + 1 != positions.Length - 1) // if j+1 isnt the last point move it backward
                    positions[j + 1] -= correction;
            }

            // re-pin endpoints after each iteration
            positions[0] = startPoint;
            positions[positions.Length - 1] = endPoint;
        }
    }

    // updates/rebuilds cable mesh
    void UpdatePhysicsMesh()
    {
        cablePoints.Clear(); // clear old points
        for (int i = 0; i < positions.Length; i++) // convert new point positions from world space to local
            cablePoints.Add(transform.InverseTransformPoint(positions[i]));

        RebuildMesh(); // build mesh around new points
    }

    // ==================== MESH GENERATION ====================

    // builds 3d mesh tube along points
    void BuildTubeMesh()
    {
        if (cablePoints.Count < 2) return;
        cableMesh.Clear();
        vertices.Clear();
        normals.Clear();
        uvs.Clear();
        triangles.Clear();

        Vector3 prevNormal = Vector3.up;

        // loop through each point and generate a ring of vertices around it
        for (int i = 0; i < cablePoints.Count; i++)
        {
            // builds local frame for each point to orient rings
            // compute forward direction
            Vector3 center = cablePoints[i]; // current point location, will be center of generated ring

            Vector3 forward; //vector direction of cable through each point (tangent of ring)
            if (i == 0) // first point
                forward = (cablePoints[i + 1] - cablePoints[i]).normalized; // forward points to next
            else if (i == cablePoints.Count - 1) // last point
                forward = (cablePoints[i] - cablePoints[i - 1]).normalized; // forward points backwards
            else // somewhere in middle
                forward = (cablePoints[i + 1] - cablePoints[i - 1]).normalized; // forward is avg

            // compute right vector by cross product, perpendicular to both forward and prevNormal
            Vector3 right = Vector3.Cross(prevNormal, forward).normalized;

            // checks if forward and prevnormal are parallel -> if cross prod = 0
            // if so use world up/right as fallback
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Vector3.right, forward).normalized;

            // compute new normal for current ring
            Vector3 normal = Vector3.Cross(forward, right).normalized;
            prevNormal = normal;

            // build ring
            for (int j = 0; j < ringSides; j++) // each iteration makes 1 vertex in ring
            {
                float angle = j / (float)ringSides * Mathf.PI * 2f; // angle around ring in radians
                Vector3 radial = right * Mathf.Cos(angle) + Mathf.Sin(angle) * normal; // direction vector from center to vertex
                Vector3 vertex = center + radial * cableRadius; // position of vertex around ring

                vertices.Add(vertex);
                normals.Add(radial.normalized);
                uvs.Add(new Vector2(j / (float)ringSides, i / (float)segmentCount));
            }
        }

        // connect rings
        for (int i = 0; i < cablePoints.Count - 1; i++)
        {
            int ringStartA = i * ringSides; // index of first vertex in current ring
            int ringStartB = (i + 1) * ringSides; // index of first vertex in next ring

            for (int j = 0; j < ringSides; j++)
            {
                int nextJ = (j + 1) % ringSides; // connects last vertex back to first

                // 4 corners of quad forming a size of the ring segment
                int a0 = ringStartA + j; // first vertex in current ring
                int a1 = ringStartA + nextJ; // second vertex in current ring
                int b0 = ringStartB + j; // first vertex in next ring
                int b1 = ringStartB + nextJ; // second vertex in next ring

                // turn quad into 2 triangles
                triangles.Add(a0);
                triangles.Add(a1);
                triangles.Add(b0);

                triangles.Add(a1);
                triangles.Add(b1);
                triangles.Add(b0);
            }
        }

        // assign data to mesh
        cableMesh.SetVertices(vertices);
        cableMesh.SetNormals(normals);
        cableMesh.SetUVs(0, uvs);
        cableMesh.SetTriangles(triangles, 0);
    }
}
