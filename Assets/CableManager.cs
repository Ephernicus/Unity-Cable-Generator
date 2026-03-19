using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways] // run during edit mode
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))] // object must haves

public class CableManager : MonoBehaviour
{
    public Transform startPoint; // transform info of the points
    public Transform endPoint;

    // cable settings
    [Min(2)] public int segmentCount = 12;
    [Min(3)] public int ringSides = 8;
    [Min(0.001f)] public float cableRadius = 0.03f;

    // TODO*** fake physics 
    [Range(0f, 2f)] public float sag = 0.2f;

    [HideInInspector] public List<Vector3> cablePoints = new List<Vector3>(); // hidden list storing points

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
        Rebuild(); // generate cable
    }

    // builds the cable 
    [HideInInspector] public void Rebuild()
    {
        if (startPoint == null || endPoint == null) return; // stop if neither points exist
        BuildPointChain();
        BuildTubeMesh();
    }

    // generates a chain of points between start and end points
    void BuildPointChain() 
    {
        cablePoints.Clear(); // remove any old points

        // get start and end positions in world space
        Vector3 a = startPoint.position;
        Vector3 b = endPoint.position;

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount; // divides line into equal segments
            Vector3 p = Vector3.Lerp(a, b, t); 

            // TODO*** fake sag for now
            float sagAmount = Mathf.Sin(t * Mathf.PI) * sag;
            p += Vector3.down * sagAmount;

            cablePoints.Add(transform.InverseTransformPoint(p)); // convert world space to local space and add to list
        }
    }

    // builds 3d mesh tube along points
    void BuildTubeMesh() 
    {
        if (cablePoints.Count < 2) return;
        cableMesh.Clear();

        List<Vector3> vertices = new(); 
        List<Vector3> normals = new();
        List<Vector2> uvs = new();
        List<int> triangles = new();

        Vector3 prevNormal = Vector3.up;

        // loop through each point and generate a ring of vertices around it
        for (int i = 0; i < cablePoints.Count; i++)
        {
            // builds local frame for each point to orient rings
            // compute forward direction
            Vector3 center = cablePoints[i]; // current point location, will be center of generated ring

            Vector3 forward; // vector direction of cable through each point (tangent of ring)
            if (i == 0) // first point
                forward = (cablePoints[i + 1] - cablePoints[i]).normalized; // forward points to next
            else if (i == cablePoints.Count - 1) // last point
                forward = (cablePoints[i] - cablePoints[i - 1]).normalized; // forward points backwards
            else // somewhere in middle
                forward = (cablePoints[i + 1] - cablePoints[i - 1]).normalized; // forward is avg
            
            // compute right vector by cross product, perpendicular to both forward and prevNormal 
            Vector3 right = Vector3.Cross(prevNormal, forward).normalized;

            // if forward and prevNormal are parallel, use world up/right as fallback
            if (right.sqrMagnitude < 0.001f) 
                right = Vector3.Cross(Vector3.up, forward).normalized; 
            if (right.sqrMagnitude < 0.001f) // 
                right = Vector3.Cross(Vector3.right, forward).normalized;

            // ccompute new normal for current ring
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
                triangles.Add(b0);
                triangles.Add(a1);

                triangles.Add(a1);
                triangles.Add(b0);
                triangles.Add(b1);
            }
        }

        // assign data to mesh
        cableMesh.SetVertices(vertices);
        cableMesh.SetNormals(normals);
        cableMesh.SetUVs(0, uvs);
        cableMesh.SetTriangles(triangles, 0);
    }
}

