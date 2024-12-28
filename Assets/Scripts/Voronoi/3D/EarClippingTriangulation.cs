using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EarClippingTriangulation
{
    /// <summary>
    /// Triangulate a single planar polygon using ear clipping. 
    /// 'verts' should be in CCW order for faceNormal, or we must ensure it.
    /// 'faceNormal' is used to check "left turn" for convex testing.
    /// Returns a list of triangle indices local to 'verts'.
    /// </summary>
    public static List<int> Triangulate(List<Vector3> verts, Vector3 faceNormal)
    {
        List<int> polygonIndices = new List<int>();
        for (int i = 0; i < verts.Count; i++)
            polygonIndices.Add(i);

        List<int> resultTriangles = new List<int>();

        while (polygonIndices.Count > 3)
        {
            bool earFound = false;

            // Attempt to find an ear
            for (int i = 0; i < polygonIndices.Count; i++)
            {
                int prev = polygonIndices[(i - 1 + polygonIndices.Count) % polygonIndices.Count];
                int curr = polygonIndices[i];
                int next = polygonIndices[(i + 1) % polygonIndices.Count];

                if (IsEar(prev, curr, next, verts, polygonIndices, faceNormal))
                {
                    // We found an ear, add that triangle
                    resultTriangles.Add(prev);
                    resultTriangles.Add(curr);
                    resultTriangles.Add(next);

                    // Remove the ear vertex from the polygon
                    polygonIndices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                // If we can't find any ear, the polygon might be self-intersecting 
                // or floating-point issues. We'll break to avoid infinite loop
                Debug.LogError("Ear clipping failed: no ear found. Possibly polygon is degenerate or concave in unexpected ways.");
                break;
            }
        }

        // If exactly three vertices remain, they form the final triangle
        if (polygonIndices.Count == 3)
        {
            resultTriangles.Add(polygonIndices[0]);
            resultTriangles.Add(polygonIndices[1]);
            resultTriangles.Add(polygonIndices[2]);
        }

        return resultTriangles;
    }

    /// <summary>
    /// Checks if the set of three consecutive vertices forms a valid "ear."
    /// </summary>
    private static bool IsEar(int prev, int curr, int next, List<Vector3> verts, List<int> polygonIndices, Vector3 faceNormal)
    {
        Vector3 A = verts[prev];
        Vector3 B = verts[curr];
        Vector3 C = verts[next];

        // 1) Check if ABC is convex given the face normal
        if (!IsTriangleConvex(A, B, C, faceNormal))
            return false;

        // 2) Check if any other vertex lies within triangle ABC
        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int idx = polygonIndices[i];
            if (idx == prev || idx == curr || idx == next)
                continue;

            if (PointInTriangle(verts[idx], A, B, C))
                return false;
        }

        return true;
    }

    /// <summary>
    /// For a CCW polygon, the triangle ABC is convex if cross((B - A),(C - A)) 
    /// aligns with faceNormal.
    /// </summary>
    private static bool IsTriangleConvex(Vector3 A, Vector3 B, Vector3 C, Vector3 faceNormal)
    {
        Vector3 cross = Vector3.Cross(B - A, C - A);
        return Vector3.Dot(cross, faceNormal) > 0f;
    }

    /// <summary>
    /// Barycentric check if point P is inside triangle ABC.
    /// </summary>
    private static bool PointInTriangle(Vector3 P, Vector3 A, Vector3 B, Vector3 C)
    {
        Vector3 v0 = C - A;
        Vector3 v1 = B - A;
        Vector3 v2 = P - A;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0f) && (v >= 0f) && (u + v <= 1f);
    }

    // Helper to determine if polygon is CCW relative to faceNormal
    public static bool IsCCW(List<Vector3> verts, Vector3 faceNormal)
    {
        // Simple approach: project polygon to a plane, or assume near XY if faceNormal ~Z.
        // We'll do a minimal area check in XY after applying faceNormal sign.
        // A more robust approach would project to faceNormal plane, but for brevity:
        float area = 0f;
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 v0 = verts[i];
            Vector3 v1 = verts[(i + 1) % verts.Count];
            // Summation of cross product in XY
            area += (v1.x - v0.x) * (v1.y + v0.y);
        }
        // area>0 => CCW in XY. If your faceNormal is pointing "up," this is fine.
        // For an arbitrary faceNormal, a proper plane projection might be needed.
        return (area > 0f);
    }
}