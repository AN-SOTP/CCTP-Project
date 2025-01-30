using System.Collections.Generic;
using UnityEngine;

public static class EarClippingTriangulation2D
{
    public static List<int> Triangulate2D(List<Vector2> verts2D)
    {
        // Optionally remove duplicates or collinears in 2D
        // Then do standard ear clipping

        if (verts2D.Count < 3) return new List<int>();

        List<int> polygonIndices = new List<int>();
        for (int i = 0; i < verts2D.Count; i++)
            polygonIndices.Add(i);

        List<int> resultTriangles = new List<int>();

        int safety = 0;
        while (polygonIndices.Count > 3 && safety < 2 * polygonIndices.Count)
        {
            bool earFound = false;
            for (int i = 0; i < polygonIndices.Count; i++)
            {
                int prev = polygonIndices[(i - 1 + polygonIndices.Count) % polygonIndices.Count];
                int curr = polygonIndices[i];
                int next = polygonIndices[(i + 1) % polygonIndices.Count];

                if (IsEar2D(prev, curr, next, verts2D, polygonIndices))
                {
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
                // fallback or break
                Debug.LogWarning("2D Ear clipping failed => fallback triangulation");
                break;
            }
            safety++;
        }
        if (polygonIndices.Count == 3)
        {
            resultTriangles.Add(polygonIndices[0]);
            resultTriangles.Add(polygonIndices[1]);
            resultTriangles.Add(polygonIndices[2]);
        }
        return resultTriangles;
    }

    private static bool IsEar2D(int prev, int curr, int next, List<Vector2> verts2D, List<int> polygonIndices)
    {
        Vector2 A = verts2D[prev];
        Vector2 B = verts2D[curr];
        Vector2 C = verts2D[next];

        // check if ABC is convex in 2D (for CCW)
        if (!IsTriangleConvex2D(A, B, C))
            return false;

        // check if any point in polygon lies inside triangle ABC
        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int idx = polygonIndices[i];
            if (idx == prev || idx == curr || idx == next)
                continue;

            if (PointInTriangle2D(verts2D[idx], A, B, C))
                return false;
        }
        return true;
    }

    private static bool IsTriangleConvex2D(Vector2 A, Vector2 B, Vector2 C)
    {
        // for 2D, cross>0 => CCW => convex
        float cross = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
        return cross > 0f;
    }

    private static bool PointInTriangle2D(Vector2 p, Vector2 A, Vector2 B, Vector2 C)
    {
        // barycentric in 2D
        float areaABC = Mathf.Abs((B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x));
        float areaPBC = Mathf.Abs((B.x - p.x) * (C.y - p.y) - (B.y - p.y) * (C.x - p.x));
        float areaAPC = Mathf.Abs((p.x - A.x) * (C.y - A.y) - (p.y - A.y) * (C.x - A.x));
        float areaABP = Mathf.Abs((B.x - A.x) * (p.y - A.y) - (B.y - A.y) * (p.x - A.x));

        // consider floating errors
        float sum = areaPBC + areaAPC + areaABP;
        return Mathf.Abs(sum - areaABC) < 1e-5f;
    }
}