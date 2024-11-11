using UnityEngine;

public class ShapeMatchingTest : MonoBehaviour
{
    public float strength = 50f; // Force threshold for fracturing
    private Mesh originalMesh;
    
    //probuilder meshfilter just a regular meshfilter? seems to work
    private MeshFilter mesh_filter;
    int[] triangles;
    Vector3[] original_vertices_position;
    Vector3[] current_vertices_position;
    Vector3[] velocities;
    Vector3 original_centroid;
    Vector3 current_centroid;

    float delta_time;
    void Start()
    {
        // Try to retrieve the mesh filter and the mesh from the GameObject
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
        
        Debug.Log(original_vertices_position.Length);
        Debug.Log(triangles.Length);
    }
    
    void Update()
    {
        delta_time = Time.deltaTime;

        ApplyForces(delta_time);
        ShapeMatching();
        UpdateMesh();
    }

    void ApplyForces(float _delta_time)
    {
        for(int i = 0; i < velocities.Length; i++)
        {
            velocities[i] += Physics.gravity * _delta_time;

            current_vertices_position[i] += velocities[i] * _delta_time;
        }
    }

    void ShapeMatching()
    {
        original_centroid = ComputeCentroid(original_vertices_position);
        current_centroid = ComputeCentroid(current_vertices_position);

        //Debug.Log("Original Centroid: " + original_centroid);
        //Debug.Log("Current Centroid: " + current_centroid);

        int vertex_count = original_vertices_position.Length;
        Vector3[] p = new Vector3[vertex_count];
        Vector3[] q = new Vector3[vertex_count];

        for (int i = 0; i < vertex_count; i++)
        {
            //p[i] represents the original position of vertex i relative to the original centroid
            //q[i] represents the current position of vertex i relative to the current centroid
            //by subtracting the centroid, we shift the coordinate system to the centroid, removing translation
            p[i] = original_vertices_position[i] - original_centroid;
            q[i] = current_vertices_position[i] - current_centroid;

            //Debug.Log($"p[{i}] = {p[i]}, q[{i}] = {q[i]}");
        }

        //Compute covariance matrix
        float[,] a = new float[3, 3];
        for (int i = 0; i < vertex_count; i++)
        {
            AddOuterProduct(ref a, q[i], p[i]);
        }
        //CovarianceMatrixLog(a);

        Quaternion r = GetOptimalRotation(a);

        float stiffness = 1.0f;

        for (int i = 0; i < vertex_count; i++)
        {
            //calculate goal position by rotating original relative position and translating back
            Vector3 goal_position = r * p[i] + current_centroid;
            //figure out correction needed
            Vector3 correction = (goal_position - current_vertices_position[i]) * stiffness;

            //update velocity and pos
            velocities[i] += correction / delta_time;
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
        centroid /= positions.Length;
        return centroid;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.TransformPoint(original_centroid), 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.TransformPoint(current_centroid), 0.05f);
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

    void CovarianceMatrixLog(float[,] A)
    {
        Debug.Log("Covariance Matrix A:");
        for (int i = 0; i < 3; i++)
        {
            string row = "";
            for (int j = 0; j < 3; j++)
            {
                row += A[i, j].ToString("F4") + " ";
            }
            Debug.Log(row);
        }
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
}