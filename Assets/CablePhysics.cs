using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CableManager))]
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
        InitPhysics();
    }

    // set up particles evenly between start and end
    public void InitPhysics()
    {
        if (cable == null) return;

        int count = cable.segmentCount + 1;
        positions = new Vector3[count];
        prevPositions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)cable.segmentCount;
            positions[i] = Vector3.Lerp(cable.startPoint, cable.endPoint, t);
            prevPositions[i] = positions[i];
        }

        segmentLength = Vector3.Distance(cable.startPoint, cable.endPoint) / cable.segmentCount;
    }

    void Update() => SimulatePhysics();

    // runs one frame of physics
    void SimulatePhysics()
    {
        if (cable == null || positions == null || positions.Length < 2) return;

        float dt = Mathf.Min(Time.deltaTime, 0.02f); // clamp for stability in scene mode


        for (int i = 1; i < positions.Length - 1; i++)
        {
            Vector3 velocity = (positions[i] - prevPositions[i]) * damping;
            prevPositions[i] = positions[i];
            positions[i] += velocity + Vector3.down * gravity * dt * dt;
        }

        ResolveCollisions();
        SolveConstraints();
        ResolveCollisions(); 
        UpdateMesh();
    }

    // handles collisions with others
    void ResolveCollisions()
    {
        for (int i = 1; i < positions.Length - 1; i++)
        {
            Collider[] hits = Physics.OverlapSphere(positions[i], cable.cableRadius);
            foreach (var col in hits)
            {
                Vector3 closest = col.ClosestPoint(positions[i]);
                float dist = Vector3.Distance(positions[i], closest);

                // particle is inside the collider
                if (dist < cable.cableRadius)
                {
                    Vector3 pushDir = (positions[i] - closest).normalized;
                    if (pushDir.sqrMagnitude < 0.001f)
                        pushDir = Vector3.up;
                    positions[i] = closest + pushDir * cable.cableRadius;
                }
            }
        }
    }

    // enforces fixed particle distance
    void SolveConstraints()
    {
        for (int iter = 0; iter < stiffness; iter++)
        {
            for (int i = 0; i < positions.Length - 1; i++)
            {
                Vector3 delta = positions[i + 1] - positions[i];
                float dist = delta.magnitude;
                if (dist < 0.0001f) continue;

                float error = (dist - segmentLength) / dist;
                Vector3 correction = delta * 0.5f * error;

                // don't move pinned endpoints
                if (i != 0)
                    positions[i] += correction;
                if (i + 1 != positions.Length - 1)
                    positions[i + 1] -= correction;
            }

            // re-pin endpoints after each iteration
            positions[0] = cable.startPoint;
            positions[positions.Length - 1] = cable.endPoint;
        }
    }

    // updates cable mesh
    void UpdateMesh()
    {
        cable.cablePoints.Clear();
        for (int i = 0; i < positions.Length; i++)
            cable.cablePoints.Add(cable.transform.InverseTransformPoint(positions[i]));

        cable.RebuildMesh();
    }
}
