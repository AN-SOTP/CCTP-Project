using System.Collections.Generic;
using UnityEngine;

public class BooleanSubtraction
{
    private Plane cuttingPlane;

    public BooleanSubtraction(Plane plane)
    {
        cuttingPlane = plane;
    }

    public Mesh Subtract(Mesh originalMesh, Vector3 impactPoint)
    {
        // Step 1: Get mesh data
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;

        // Step 2: Lists to hold new geometry
        List<Vector3> newVerticesAbovePlane = new List<Vector3>();
        List<int> newTrianglesAbovePlane = new List<int>();

        // Step 3: Visualize the cutting plane and the original triangles
        VisualizeCuttingPlane(impactPoint);
        VisualizeOriginalTriangles(vertices, triangles);

        // Step 4: Iterate through the triangles of the mesh
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            // Step 5: Classify each vertex as being above or below the cutting plane
            bool isV0Above = IsPointAbovePlane(v0);
            bool isV1Above = IsPointAbovePlane(v1);
            bool isV2Above = IsPointAbovePlane(v2);

            // Step 6: Categorize the triangle
            if (isV0Above && isV1Above && isV2Above)
            {
                // All vertices are above the plane, keep the triangle as is
                AddTriangleToList(newVerticesAbovePlane, newTrianglesAbovePlane, v0, v1, v2);
            }
            else if (!isV0Above && !isV1Above && !isV2Above)
            {
                // All vertices are below the plane, ignore the triangle
                continue;
            }
            else
            {
                // Step 7: Handle the case where the triangle is split by the plane
                SplitTriangle(v0, v1, v2, isV0Above, isV1Above, isV2Above,
                              newVerticesAbovePlane, newTrianglesAbovePlane);
            }
        }

        // Step 8: Build the resulting mesh (only keep the above-plane geometry)
        return BuildMesh(newVerticesAbovePlane, newTrianglesAbovePlane);
    }

    // Visualize the cutting plane as a grid to make it more obvious
    private void VisualizeCuttingPlane(Vector3 impactPoint)
    {
        Vector3 planeNormal = cuttingPlane.normal;
        Vector3 planePoint = impactPoint;

        float gridSize = 2.0f; // Size of the grid in the plane
        int gridLines = 10; // Number of lines for the grid

        for (int i = -gridLines; i <= gridLines; i++)
        {
            for (int j = -gridLines; j <= gridLines; j++)
            {
                Vector3 point1 = planePoint + Vector3.right * i * gridSize + Vector3.forward * j * gridSize;
                Vector3 point2 = point1 + Vector3.forward * gridSize;
                Vector3 point3 = point1 + Vector3.right * gridSize;

                Debug.DrawLine(point1, point2, Color.red, 5f);
                Debug.DrawLine(point1, point3, Color.red, 5f);
            }
        }
    }

    // Visualize the original triangles
    private void VisualizeOriginalTriangles(Vector3[] vertices, int[] triangles)
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            Debug.DrawLine(v0, v1, Color.white, 5f);
            Debug.DrawLine(v1, v2, Color.white, 5f);
            Debug.DrawLine(v2, v0, Color.white, 5f);
        }
    }

    // Determine if a point is above the cutting plane
    private bool IsPointAbovePlane(Vector3 point)
    {
        return cuttingPlane.GetDistanceToPoint(point) > 0;
    }

    // Add triangle to a list, adjusting vertices indexing
    private void AddTriangleToList(List<Vector3> verticesList, List<int> trianglesList, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        int baseIndex = verticesList.Count;

        verticesList.Add(v0);
        verticesList.Add(v1);
        verticesList.Add(v2);

        trianglesList.Add(baseIndex);
        trianglesList.Add(baseIndex + 1);
        trianglesList.Add(baseIndex + 2);
    }

    // Split a triangle along the cutting plane
    private void SplitTriangle(Vector3 v0, Vector3 v1, Vector3 v2, bool isV0Above, bool isV1Above, bool isV2Above,
                               List<Vector3> verticesAbove, List<int> trianglesAbove)
    {
        // Find the intersection points of the triangle with the cutting plane
        Vector3 intersect01, intersect02;

        if (isV0Above)
        {
            if (isV1Above)
            {
                // V2 is below, so split on V0-V2 and V1-V2
                intersect01 = FindIntersection(v0, v2);
                intersect02 = FindIntersection(v1, v2);
                AddTriangleToList(verticesAbove, trianglesAbove, v0, v1, intersect01);
                AddTriangleToList(verticesAbove, trianglesAbove, intersect01, v1, intersect02);

                // Visualize intersection lines
                Debug.DrawLine(intersect01, v2, Color.yellow, 5f);
                Debug.DrawLine(intersect02, v2, Color.yellow, 5f);
            }
            else if (isV2Above)
            {
                // V1 is below, so split on V0-V1 and V2-V1
                intersect01 = FindIntersection(v0, v1);
                intersect02 = FindIntersection(v2, v1);
                AddTriangleToList(verticesAbove, trianglesAbove, v0, intersect01, v2);
                AddTriangleToList(verticesAbove, trianglesAbove, v2, intersect01, intersect02);

                // Visualize intersection lines
                Debug.DrawLine(intersect01, v1, Color.yellow, 5f);
                Debug.DrawLine(intersect02, v1, Color.yellow, 5f);
            }
            else
            {
                // V1 and V2 are below, so split on V0-V1 and V0-V2
                intersect01 = FindIntersection(v0, v1);
                intersect02 = FindIntersection(v0, v2);
                AddTriangleToList(verticesAbove, trianglesAbove, v0, intersect01, intersect02);

                // Visualize intersection lines
                Debug.DrawLine(intersect01, v1, Color.yellow, 5f);
                Debug.DrawLine(intersect02, v2, Color.yellow, 5f);
            }
        }
        else if (isV1Above)
        {
            if (isV2Above)
            {
                // V0 is below, so split on V1-V0 and V2-V0
                intersect01 = FindIntersection(v1, v0);
                intersect02 = FindIntersection(v2, v0);
                AddTriangleToList(verticesAbove, trianglesAbove, v1, intersect01, v2);
                AddTriangleToList(verticesAbove, trianglesAbove, v2, intersect01, intersect02);

                // Visualize intersection lines
                Debug.DrawLine(intersect01, v0, Color.yellow, 5f);
                Debug.DrawLine(intersect02, v0, Color.yellow, 5f);
            }
            else
            {
                // V0 and V2 are below, so split on V1-V0 and V1-V2
                intersect01 = FindIntersection(v1, v0);
                intersect02 = FindIntersection(v1, v2);
                AddTriangleToList(verticesAbove, trianglesAbove, v1, intersect01, intersect02);

                // Visualize intersection lines
                Debug.DrawLine(intersect01, v0, Color.yellow, 5f);
                Debug.DrawLine(intersect02, v2, Color.yellow, 5f);
            }
        }
    }

    // Find the intersection point between an edge and the plane
    private Vector3 FindIntersection(Vector3 pointAbove, Vector3 pointBelow)
    {
        float distanceAbove = cuttingPlane.GetDistanceToPoint(pointAbove);
        float distanceBelow = cuttingPlane.GetDistanceToPoint(pointBelow);
        float t = distanceAbove / (distanceAbove - distanceBelow);
        return Vector3.Lerp(pointAbove, pointBelow, t);
    }

    // Build a mesh from the vertices and triangles lists
    private Mesh BuildMesh(List<Vector3> vertices, List<int> triangles)
    {
        Mesh newMesh = new Mesh();
        newMesh.vertices = vertices.ToArray();
        newMesh.triangles = triangles.ToArray();
        newMesh.RecalculateNormals();
        return newMesh;
    }
}