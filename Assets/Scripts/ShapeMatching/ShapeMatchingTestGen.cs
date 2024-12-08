/*using UnityEngine;

public class ShapeMatchingTest : MonoBehaviour
{
    public float strength = 50f; // Force threshold for fracturing
    private Mesh originalMesh;

    private MeshFilter meshFilter;
    Vector3[] original_vertices_position;
    Vector3[] current_vertices_position;
    Vector3[] velocities;
    int[] triangles;

    void Start()
    {
        // Try to retrieve the mesh filter and the mesh from the GameObject
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            originalMesh = meshFilter.mesh;
        }
        else
        {
            Debug.LogError("No MeshFilter found on this GameObject!");
        }

        original_vertices_position = originalMesh.vertices;
        int vertexCount = original_vertices_position.Length;
        current_vertices_position = new Vector3[vertexCount];
        velocities = new Vector3[vertexCount];
        triangles = originalMesh.triangles;

        // Initialize current positions and velocities
        for (int i = 0; i < vertexCount; i++)
        {
            current_vertices_position[i] = original_vertices_position[i];
            velocities[i] = Vector3.zero;
        }

        Debug.Log("Number of vertices: " + original_vertices_position.Length);
        Debug.Log("Number of triangles: " + triangles.Length);
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        ApplyForces(deltaTime);
        ShapeMatching();
        UpdateMesh();
    }

    void ApplyForces(float deltaTime)
    {
        for (int i = 0; i < velocities.Length; i++)
        {
            // Apply gravity
            velocities[i] += Physics.gravity * deltaTime;

            // Update positions
            current_vertices_position[i] += velocities[i] * deltaTime;
        }
    }

    void ShapeMatching()
    {
        // Compute centroids
        Vector3 originalCentroid = ComputeCentroid(original_vertices_position);
        Vector3 currentCentroid = ComputeCentroid(current_vertices_position);

        // Remove translational components
        int vertexCount = original_vertices_position.Length;
        Vector3[] p = new Vector3[vertexCount]; // Original relative positions
        Vector3[] q = new Vector3[vertexCount]; // Current relative positions

        for (int i = 0; i < vertexCount; i++)
        {
            p[i] = original_vertices_position[i] - originalCentroid;
            q[i] = current_vertices_position[i] - currentCentroid;
        }

        // Compute the covariance matrix
        float[,] A = new float[3, 3];

        for (int i = 0; i < vertexCount; i++)
        {
            AddOuterProduct(ref A, q[i], p[i]);
        }

        // Compute the optimal rotation
        Quaternion R = ComputeOptimalRotation(A);

        // Apply rotation and translation
        float stiffness = 1.0f; // Adjust this value to control the stiffness

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 goalPosition = R * p[i] + currentCentroid;
            Vector3 correction = (goalPosition - current_vertices_position[i]) * stiffness;

            velocities[i] += correction / Time.deltaTime;
            current_vertices_position[i] += correction;
        }
    }

    Vector3 ComputeCentroid(Vector3[] positions)
    {
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            centroid += pos;
        }
        return centroid / positions.Length;
    }

    void AddOuterProduct(ref float[,] A, Vector3 a, Vector3 b)
    {
        // A = A + a * b^T
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

    Quaternion ComputeOptimalRotation(float[,] A)
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
            Vector4 qNew = MultiplyMatrixVector(N, q);
            qNew.Normalize();

            if (Vector4.Dot(qNew, q) > 0.9999f)
                break;

            q = qNew;
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

    void UpdateMesh()
    {
        Mesh mesh = meshFilter.mesh;
        mesh.vertices = current_vertices_position;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
*/