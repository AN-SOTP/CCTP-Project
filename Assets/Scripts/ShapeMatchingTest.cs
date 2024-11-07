using UnityEngine;

public class ShapeMatchingTest : MonoBehaviour
{
    public float strength = 50f; // Force threshold for fracturing
    private Mesh originalMesh;
    
    //probuilder meshfilter just a regular meshfilter? seems to work
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
        current_vertices_position = originalMesh.vertices;
        triangles = originalMesh.triangles;

        Debug.Log(original_vertices_position.Length);
        Debug.Log(triangles.Length);
    }
    
    void Update()
    {
     
    }
}