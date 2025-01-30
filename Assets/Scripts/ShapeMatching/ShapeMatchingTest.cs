using UnityEngine;

public class ShapeMatchingTest : MonoBehaviour
{
    private Mesh originalMesh;
    private Rigidbody rb;

    private MeshFilter mesh_filter;
    int[] triangles;
    Vector3[] original_vertices_position;
    Vector3[] current_vertices_position;
    Vector3[] velocities;
    Vector3 original_centroid;
    Vector3 current_centroid;

    float delta_time;
    public float stiffness = 0.1f; // 0 = elastic, 1 = rigid
    public float damping_factor = 0.98f; // Adjust as needed
    
    // Apply deformation to vertices near the contact point
    public float radius = 0.5f; // Adjust as needed
    public float deformationStrength = 0.5f; // Adjust as needed

    void Start()
    {
        mesh_filter = GetComponent<MeshFilter>();
        if (mesh_filter != null)
        {
            originalMesh = mesh_filter.mesh;
        }
        else
        {
            Debug.LogError("No MeshFilter found on this GameObject!");
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("No Rigidbody found on this GameObject! Adding one.");
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = true;
        rb.isKinematic = false;
        // rb.freezeRotation = true; // Uncomment if you want to prevent rotation

        original_vertices_position = originalMesh.vertices;
        current_vertices_position = new Vector3[original_vertices_position.Length];
        velocities = new Vector3[original_vertices_position.Length];
        triangles = originalMesh.triangles;

        for (int i = 0; i < original_vertices_position.Length; i++)
        {
            current_vertices_position[i] = original_vertices_position[i];
            velocities[i] = Vector3.zero;
        }

        Debug.Log("Vertices: " + original_vertices_position.Length);
        Debug.Log("Triangles: " + triangles.Length);
    }

    void FixedUpdate()
    {
        delta_time = Time.fixedDeltaTime;

        ApplyForces(delta_time);
        ShapeMatching();
        HandleVertexCollisions();
        UpdateMesh();
    }

    void ApplyForces(float deltaTime)
    {

        for (int i = 0; i < velocities.Length; i++)
        {
            // Apply damping
            velocities[i] *= damping_factor;

            // Update positions
            current_vertices_position[i] += velocities[i] * deltaTime;
        }
    }

    void ShapeMatching()
    {
        void ShapeMatching()
        {
            // Compute centroids in local space
            original_centroid = ComputeCentroid(original_vertices_position);
            current_centroid = ComputeCentroid(current_vertices_position);

            int vertex_count = original_vertices_position.Length;
            Vector3[] p = new Vector3[vertex_count];
            Vector3[] q = new Vector3[vertex_count];

            for (int i = 0; i < vertex_count; i++)
            {
                // Relative positions in local space
                p[i] = original_vertices_position[i] - original_centroid;
                q[i] = current_vertices_position[i] - current_centroid;
            }

            // Compute covariance matrix
            float[,] a = new float[3, 3];
            for (int i = 0; i < vertex_count; i++)
            {
                AddOuterProduct(ref a, q[i], p[i]);
            }

            Quaternion r = GetOptimalRotation(a);

            // Calculate goal positions
            Vector3[] goal_positions = new Vector3[vertex_count];
            for (int i = 0; i < vertex_count; i++)
            {
                goal_positions[i] = r * p[i] + current_centroid;
            }

            // Compute corrections
            Vector3[] corrections = new Vector3[vertex_count];
            Vector3 total_correction = Vector3.zero;
            Vector3 total_torque = Vector3.zero;

            for (int i = 0; i < vertex_count; i++)
            {
                // Correction before momentum conservation
                corrections[i] = (goal_positions[i] - current_vertices_position[i]) * stiffness;

                // Sum up corrections and torque
                total_correction += corrections[i];
                total_torque += Vector3.Cross(current_vertices_position[i] - current_centroid, corrections[i]);
            }

            // Calculate average correction (to conserve linear momentum)
            Vector3 average_correction = total_correction / vertex_count;

            // Adjust corrections to conserve angular momentum
            for (int i = 0; i < vertex_count; i++)
            {
                // Calculate r_i (relative position to centroid)
                Vector3 r_i = current_vertices_position[i] - current_centroid;

                // Adjust correction to remove net torque
                Vector3 torque_correction = Vector3.Cross(r_i, total_torque) / (vertex_count * r_i.sqrMagnitude + 1e-6f);

                // Apply both linear and angular momentum conservation adjustments
                corrections[i] = corrections[i] - average_correction - torque_correction;
            }

            // Apply corrected corrections to velocities and positions
            for (int i = 0; i < vertex_count; i++)
            {
                velocities[i] += corrections[i] / delta_time;
                current_vertices_position[i] += corrections[i];
            }
        }

    }

    void HandleVertexCollisions()
    {
        for (int i = 0; i < current_vertices_position.Length; i++)
        {
            Vector3 worldVertexPos = transform.TransformPoint(current_vertices_position[i]);

            // Cast a ray from the vertex in the direction of its velocity
            RaycastHit hit;
            Vector3 velocityWorld = transform.TransformDirection(velocities[i]);
            if (Physics.Raycast(worldVertexPos, velocityWorld.normalized, out hit, velocityWorld.magnitude * delta_time))
            {
                // Adjust the velocity to prevent penetration
                velocities[i] = Vector3.zero;

                // Optionally, adjust the position
                Vector3 hitPointLocal = transform.InverseTransformPoint(hit.point);
                current_vertices_position[i] = hitPointLocal;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        ApplyCollisionDeformation(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        ApplyCollisionDeformation(collision);
    }

    void ApplyCollisionDeformation(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 localContactPoint = transform.InverseTransformPoint(contact.point);
            Vector3 localNormal = transform.InverseTransformDirection(contact.normal);

            for (int i = 0; i < current_vertices_position.Length; i++)
            {
                Vector3 toVertex = current_vertices_position[i] - localContactPoint;
                float distance = toVertex.magnitude;

                if (distance < radius)
                {
                    float deformationAmount = (radius - distance) / radius;
                    Vector3 deformation = localNormal * deformationAmount * deformationStrength;

                    velocities[i] += deformation;
                }
            }
        }
    }

    Vector3 ComputeCentroid(Vector3[] positions)
    {
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            centroid += pos;
        }
        centroid /= positions.Length;
        return centroid;
    }

    void AddOuterProduct(ref float[,] A, Vector3 a, Vector3 b)
    {
        A[0, 0] += a.x * b.x;
        A[0, 1] += a.x * b.y;
        A[0, 2] += a.x * b.z;
        A[1, 0] += a.y * b.x;
        A[1, 1] += a.y * b.y;
        A[1, 2] += a.y * b.z;
        A[2, 0] += a.z * b.x;
        A[2, 1] += a.z * b.y;
        A[2, 2] += a.z * b.z;
    }

    void UpdateMesh()
    {
        Mesh mesh = mesh_filter.mesh;
        mesh.vertices = current_vertices_position;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    Quaternion GetOptimalRotation(float[,] A)
    {
        // Kabsch algorithm to compute rotation
        float[,] N = new float[4, 4];

        N[0, 0] = A[0, 0] + A[1, 1] + A[2, 2];
        N[0, 1] = A[1, 2] - A[2, 1];
        N[0, 2] = A[2, 0] - A[0, 2];
        N[0, 3] = A[0, 1] - A[1, 0];

        N[1, 0] = N[0, 1];
        N[1, 1] = A[0, 0] - A[1, 1] - A[2, 2];
        N[1, 2] = A[0, 1] + A[1, 0];
        N[1, 3] = A[2, 0] + A[0, 2];

        N[2, 0] = N[0, 2];
        N[2, 1] = N[1, 2];
        N[2, 2] = -A[0, 0] + A[1, 1] - A[2, 2];
        N[2, 3] = A[1, 2] + A[2, 1];

        N[3, 0] = N[0, 3];
        N[3, 1] = N[1, 3];
        N[3, 2] = N[2, 3];
        N[3, 3] = -A[0, 0] - A[1, 1] + A[2, 2];

        // Power iteration method to find dominant eigenvector
        Vector4 q = new Vector4(1, 0, 0, 0); // Initial guess
        for (int iter = 0; iter < 10; iter++)
        {
            Vector4 q_new = MultiplyMatrixVector(N, q);
            q_new.Normalize();

            if (Vector4.Dot(q_new, q) > 0.9999f)
                break;

            q = q_new;
        }

        Quaternion rotation = new Quaternion(q.y, q.z, q.w, q.x);

        return rotation;
    }

    Vector4 MultiplyMatrixVector(float[,] M, Vector4 v)
    {
        Vector4 result = new Vector4(
            M[0, 0] * v.x + M[0, 1] * v.y + M[0, 2] * v.z + M[0, 3] * v.w,
            M[1, 0] * v.x + M[1, 1] * v.y + M[1, 2] * v.z + M[1, 3] * v.w,
            M[2, 0] * v.x + M[2, 1] * v.y + M[2, 2] * v.z + M[2, 3] * v.w,
            M[3, 0] * v.x + M[3, 1] * v.y + M[3, 2] * v.z + M[3, 3] * v.w
        );
        return result;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.TransformPoint(original_centroid), 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.TransformPoint(current_centroid), 0.05f);

        if (current_vertices_position != null)
        {
            Gizmos.color = Color.green;
            foreach (Vector3 vertex in current_vertices_position)
            {
                Vector3 worldPos = transform.TransformPoint(vertex);
                Gizmos.DrawSphere(worldPos, 0.01f);
            }
        }
    }
}
