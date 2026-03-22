using UnityEngine;

[ExecuteAlways] // runs in edit mode
[RequireComponent(typeof(CableManager))] // ensures CableManager is present 
public class CablePhysics : MonoBehaviour
{
    // inspector settings
    [Tooltip("How much the cable drapes. Light -> Heavy")]
    [Range(0f, 20f)] public float gravity = 9.8f;

    [Tooltip("How quickly the cable settles. Quick -> Slow")]
    [Range(0.5f, 1f)] public float damping = 0.98f;

    [Tooltip("How rigid the cable is. Stretchy -> Stiff")]
    [Range(1, 30)] public int stiffness = 10;

    Vector3[] positions;
    Vector3[] prevPositions;
    float segmentLength;

    CableManager cable;

    // grabs CableManager reference and initializes physics
    void OnEnable()
    {
        cable = GetComponent<CableManager>();
        Initialize();
    }

    // set up particles evenly between start and end
    public void Initialize()
    {
        if (cable == null) return;

        int count = cable.segmentCount + 1; // refers to chain points
        positions = new Vector3[count];
        prevPositions = new Vector3[count];

        // loops through each points and places them evenly between start and end
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)cable.segmentCount;
            positions[i] = Vector3.Lerp(cable.startPoint, cable.endPoint, t);
            prevPositions[i] = positions[i]; // set prev pos to current pos = zero velocity at start
        }

        segmentLength = Vector3.Distance(cable.startPoint, cable.endPoint) / cable.segmentCount; // calculates segment length
    }

    // runs physics each frame
    void Update() => RunPhysics();

    // runs one frame of physics
    void RunPhysics()
    {
        if (cable == null || positions == null || positions.Length < 2) return;

        float time = Mathf.Min(Time.deltaTime, 0.02f); // time since last frame capped to 0.02 for stability

        // loop through points except fixed endpoints
        for (int i = 1; i < positions.Length - 1; i++)
        {
            Vector3 velocity = (positions[i] - prevPositions[i]) * damping; // velocity = current - previous
            prevPositions[i] = positions[i]; // save current as prev for next frame
            positions[i] += velocity + Vector3.down * gravity * time * time; // apply velocity and gravity
        }

        // rest of physics pipeline
        Collisions();
        Spacing();
        Collisions(); // extra collision after spacing to prevent tunneling
        UpdateMesh();
    }

    // handles collisions with others
    void Collisions()
    {
        for (int i = 1; i < positions.Length - 1; i++)
        {
            Collider[] hits = Physics.OverlapSphere(positions[i], cable.cableRadius); // check for colliders within cable radius of point
            foreach (var col in hits)
            {
                Vector3 closest = col.ClosestPoint(positions[i]); // get closest point on collider to cable point
                float dist = Vector3.Distance(positions[i], closest); // distance from cable point to closest point

                // particle is inside the collider
                if (dist < cable.cableRadius)
                {
                    Vector3 pushDir = (positions[i] - closest).normalized; // direction to push cable point out of collider
                    if (pushDir.sqrMagnitude < 0.001f) // use an arbitrary push direction if too close
                        pushDir = Vector3.up;
                    positions[i] = closest + pushDir * cable.cableRadius; // move cable point 
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
                Vector3 delta = positions[j + 1] - positions[j]; // point a to b
                float dist = delta.magnitude;
                if (dist < 0.0001f) continue; // avoid division by zero

                float error = (dist - segmentLength) / dist; // how much the segment deviates from desired spacing
                Vector3 correction = delta * 0.5f * error; // how much to move each point to correct spacing (half the error)

                // don't move pinned endpoints
                if (j != 0)
                    positions[j] += correction;
                if (j + 1 != positions.Length - 1)
                    positions[j + 1] -= correction;
            }

            // re-pin endpoints after each iteration
            positions[0] = cable.startPoint;
            positions[positions.Length - 1] = cable.endPoint;
        }
    }

    // updates/rebuilds cable mesh
    void UpdateMesh()
    {
        cable.cablePoints.Clear();
        for (int i = 0; i < positions.Length; i++)
            cable.cablePoints.Add(cable.transform.InverseTransformPoint(positions[i]));

        cable.RebuildMesh();
    }
}
