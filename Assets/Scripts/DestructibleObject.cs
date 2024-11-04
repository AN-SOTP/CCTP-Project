using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public float strength = 50f; // Force threshold for fracturing
    private Mesh originalMesh;
    private MeshFilter meshFilter;
    Vector3[] vertices;
    int[] triangles;
    void Start()
    {
        // Try to retrieve the mesh filter and the mesh from the GameObject
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            originalMesh = meshFilter.mesh; // Get the original mesh of the object
        }
        else
        {
            Debug.LogError("No MeshFilter found on this GameObject!");
        }

        vertices = originalMesh.vertices;
        triangles = originalMesh.triangles;

        Debug.Log(vertices.Length);
        Debug.Log(triangles.Length);
    }
    
    void Update()
    {
     
    }
}