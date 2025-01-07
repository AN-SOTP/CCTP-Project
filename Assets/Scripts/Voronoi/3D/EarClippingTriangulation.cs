using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EarClippingTriangulation
{
    public static List<int> Triangulate(List<Vector3> verts, Vector3 faceNormal)
    {
        // (Optional) remove duplicates & collinear
        verts = RemoveDuplicateVerts(verts, 1e-6f);
        verts = RemoveCollinearVerts(verts, 1e-6f);

        // re-check count
        if (verts.Count < 3) return new List<int>();

        // (Optional) ensure truly planar by projecting to plane
        // This is only needed if you suspect mild 3D drift.
        // Then re-map them to 2D coords for robust ear clipping or do "3D ear clipping."

        List<int> polygonIndices = new List<int>(verts.Count);
        for (int i = 0; i < verts.Count; i++)
            polygonIndices.Add(i);

        // normal ear clip
        List<int> resultTriangles = new List<int>();
        int maxIters = 2 * polygonIndices.Count;
        int iter = 0;

        while (polygonIndices.Count > 3)
        {
            bool earFound = false;

            for (int i = 0; i < polygonIndices.Count; i++)
            {
                int prev = polygonIndices[(i - 1 + polygonIndices.Count) % polygonIndices.Count];
                int curr = polygonIndices[i];
                int next = polygonIndices[(i + 1) % polygonIndices.Count];

                if (IsEar(prev, curr, next, verts, polygonIndices, faceNormal))
                {
                    // Found ear
                    resultTriangles.Add(prev);
                    resultTriangles.Add(curr);
                    resultTriangles.Add(next);
                    polygonIndices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                // fallback or skip
                Debug.LogError("Ear clipping failed: no ear found. Attempting fallback fan.");
                // fallback fan:
                if (polygonIndices.Count >= 3)
                {
                    for (int t = 1; t < polygonIndices.Count - 1; t++)
                    {
                        resultTriangles.Add(polygonIndices[0]);
                        resultTriangles.Add(polygonIndices[t]);
                        resultTriangles.Add(polygonIndices[t + 1]);
                    }
                }
                break;
            }

            iter++;
            if (iter > maxIters)
            {
                Debug.LogError("Ear clipping infinite loop. Aborting.");
                break;
            }
        }

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
        // 1) Compute polygon centroid
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < verts.Count; i++)
            centroid += verts[i];
        centroid /= verts.Count;

        // 2) Orthonormal basis for plane
        Vector3 u = Vector3.Cross(faceNormal, Vector3.up);
        if (u.sqrMagnitude < 1e-6f)
            u = Vector3.Cross(faceNormal, Vector3.right);
        u.Normalize();
        Vector3 v = Vector3.Cross(faceNormal, u);

        // 3) Project each vertex into 2D
        List<Vector2> projected2D = new List<Vector2>(verts.Count);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 r = verts[i] - centroid;
            float x = Vector3.Dot(r, u);
            float y = Vector3.Dot(r, v);
            projected2D.Add(new Vector2(x, y));
        }

        // 4) Compute the signed area in 2D
        float area = 0f;
        for (int i = 0; i < projected2D.Count; i++)
        {
            Vector2 p0 = projected2D[i];
            Vector2 p1 = projected2D[(i + 1) % projected2D.Count];
            area += (p1.x - p0.x) * (p1.y + p0.y);
        }
        // area>0 => CCW
        return area > 0f;
    }

    private static List<Vector3> RemoveDuplicateVerts(List<Vector3> input, float eps)
    {
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < input.Count; i++)
        {
            bool foundDup = false;
            for (int j = 0; j < result.Count; j++)
            {
                if ((input[i] - result[j]).sqrMagnitude < eps * eps)
                {
                    foundDup = true;
                    break;
                }
            }
            if (!foundDup) result.Add(input[i]);
        }
        return result;
    }
    private static List<Vector3> RemoveCollinearVerts(List<Vector3> input, float eps)
    {
        if (input.Count < 3) return input;
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < input.Count; i++)
        {
            Vector3 prev = input[(i - 1 + input.Count) % input.Count];
            Vector3 curr = input[i];
            Vector3 next = input[(i + 1) % input.Count];

            // cross
            Vector3 cross = Vector3.Cross(next - curr, prev - curr);
            if (cross.sqrMagnitude < eps * eps)
            {
                // skip 'curr', it's collinear
            }
            else
            {
                result.Add(curr);
            }
        }
        return result;
    }
}   
