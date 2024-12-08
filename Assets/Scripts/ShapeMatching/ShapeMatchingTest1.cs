/*
using UnityEngine;

public class ShapeMatchingTest : MonoBehaviour
{
    public float strength = 50f; // Force threshold for fracturing
    private Mesh originalMesh;

    private MeshFilter mesh_filter;
    int[] triangles;
    Vector3[] original_vertices_position;
    Vector3[] current_vertices_position;
    Vector3[] velocities;
    Vector3 original_centroid;
    Vector3 current_centroid;

    float delta_time;
    public float stiffness = 0.5f; // Adjust between 0 (elastic) and 1 (rigid)

    void Start()
    {
        // Retrieve the mesh filter and mesh
        mesh_filter = GetComponent<MeshFilter>();
        if (mesh_filter != null)
        {
            originalMesh = mesh_filter.mesh;
        }
        else
        {
            Debug.LogError("No MeshFilter found on this GameObject!");
        }

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
        HandleCollisions();
        UpdateMesh();

        // Optionally update transform.position
        // transform.position = ComputeCentroidWorldSpace(current_vertices_position);
    }

    void ApplyForces(float deltaTime)
    {
        float damping_factor = 0.98f; // Adjust between 0 and 1

        // Gravity is in world space; convert to local space if necessary
        Vector3 gravity = Physics.gravity;

        for (int i = 0; i < velocities.Length; i++)
        {
            // Apply gravity
            velocities[i] += gravity * deltaTime;

            // Apply damping
            velocities[i] *= damping_factor;

            // Update vertex positions
            current_vertices_position[i] += velocities[i] * deltaTime;
        }
    }

    void ShapeMatching()
    {
        // Compute centroids in world space
        original_centroid = ComputeCentroidWorldSpace(original_vertices_position);
        current_centroid = ComputeCentroidWorldSpace(current_vertices_position);

        int vertex_count = original_vertices_position.Length;
        Vector3[] p = new Vector3[vertex_count];
        Vector3[] q = new Vector3[vertex_count];

        for (int i = 0; i < vertex_count; i++)
        {
            // Convert to world space
            Vector3 original_vertex_world = transform.TransformPoint(original_vertices_position[i]);
            Vector3 current_vertex_world = transform.TransformPoint(current_vertices_position[i]);

            // Relative positions in world space
            p[i] = original_vertex_world - original_centroid;
            q[i] = current_vertex_world - current_centroid;
        }

        // Compute covariance matrix
        float[,] a = new float[3, 3];
        for (int i = 0; i < vertex_count; i++)
        {
            AddOuterProduct(ref a, q[i], p[i]);
        }

        Quaternion r = GetOptimalRotation(a);

        for (int i = 0; i < vertex_count; i++)
        {
            // Calculate goal position in world space
            Vector3 goal_position = r * p[i] + current_centroid;

            // Compute correction in world space
            Vector3 correction = (goal_position - transform.TransformPoint(current_vertices_position[i])) * stiffness;

            // Convert correction to local space
            correction = transform.InverseTransformDirection(correction);

            // Update velocity and position
            velocities[i] += correction / delta_time;
            current_vertices_position[i] += correction;
        }
    }

    void HandleCollisions()
    {
        for (int i = 0; i < current_vertices_position.Length; i++)
        {
            // Convert vertex position to world space
            Vector3 vertexWorldPos = transform.TransformPoint(current_vertices_position[i]);

            if (vertexWorldPos.y < 0f)
            {
                // Set vertex position to plane level in world space
                vertexWorldPos.y = 0f;

                // Reset vertical velocity
                Vector3 velocityWorld = transform.TransformDirection(velocities[i]);
                if (velocityWorld.y < 0f)
                {
                    velocityWorld.y = 0f;
                    velocities[i] = transform.InverseTransformDirection(velocityWorld);
                }

                // Convert back to local space
                current_vertices_position[i] = transform.InverseTransformPoint(vertexWorldPos);
            }
        }
    }

    Vector3 ComputeCentroidWorldSpace(Vector3[] positions)
    {
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            centroid += transform.TransformPoint(pos);
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
        // Use the Kabsch algorithm to compute the optimal rotation
        // Create a symmetric 4x4 matrix for quaternion computation
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

        // Compute the eigenvector corresponding to the largest eigenvalue
        // Since N is symmetric, we can use the power iteration method

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
            Gizmos.color = Color.red;
            foreach (Vector3 vertex in current_vertices_position)
            {
                Vector3 worldPos = transform.TransformPoint(vertex);
                Gizmos.DrawSphere(worldPos, 0.01f);
            }
        }
    }
}*/