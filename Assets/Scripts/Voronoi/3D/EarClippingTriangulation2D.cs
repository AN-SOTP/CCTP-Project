using System.Collections.Generic;
using UnityEngine;

public static class EarClippingTriangulation2D
{
    public static List<int> Triangulate2D(List<Vector2> verts2D)
    {

        if (verts2D.Count < 3)
        {
            return new List<int>();
        }

        List<int> polygon_indices = new List<int>();
        for (int i = 0; i < verts2D.Count; i++)
            polygon_indices.Add(i);

        List<int> result_triangles = new List<int>();

        int safety = 0;
        while (polygon_indices.Count > 3 && safety < 2 * polygon_indices.Count)
        {
            bool ear_found = false;
            for (int i = 0; i < polygon_indices.Count; i++)
            {
                int prev = polygon_indices[(i - 1 + polygon_indices.Count) % polygon_indices.Count];
                int curr = polygon_indices[i];
                int next = polygon_indices[(i + 1) % polygon_indices.Count];

                if (IsEar2D(prev, curr, next, verts2D, polygon_indices))
                {
                    result_triangles.Add(prev);
                    result_triangles.Add(curr);
                    result_triangles.Add(next);
                    polygon_indices.RemoveAt(i);
                    ear_found = true;
                    break;
                }
            }
            if (!ear_found)
            {
                Debug.LogWarning("2D Ear clipping failed, attempting fallback triangulation");
                break;
            }
            safety++;
        }
        if (polygon_indices.Count == 3)
        {
            result_triangles.Add(polygon_indices[0]);
            result_triangles.Add(polygon_indices[1]);
            result_triangles.Add(polygon_indices[2]);
        }
        return result_triangles;
    }

    private static bool IsEar2D(int prev, int curr, int next, List<Vector2> verts2D, List<int> polygon_indices)
    {
        Vector2 A = verts2D[prev];
        Vector2 B = verts2D[curr];
        Vector2 C = verts2D[next];

        if (!IsTriangleConvex2D(A, B, C))
        {
            return false;
        }

        for (int i = 0; i < polygon_indices.Count; i++)
        {
            int idx = polygon_indices[i];
            if (idx == prev || idx == curr || idx == next)
            {
                continue;
            }

            if (PointInTriangle2D(verts2D[idx], A, B, C))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsTriangleConvex2D(Vector2 A, Vector2 B, Vector2 C)
    {
        float cross = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
        return cross > 0f;
    }

    private static bool PointInTriangle2D(Vector2 p, Vector2 A, Vector2 B, Vector2 C)
    {
        //barycentric in 2D
        float areaABC = Mathf.Abs((B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x));
        float areaPBC = Mathf.Abs((B.x - p.x) * (C.y - p.y) - (B.y - p.y) * (C.x - p.x));
        float areaAPC = Mathf.Abs((p.x - A.x) * (C.y - A.y) - (p.y - A.y) * (C.x - A.x));
        float areaABP = Mathf.Abs((B.x - A.x) * (p.y - A.y) - (B.y - A.y) * (p.x - A.x));

        //take into account floating point errors
        float sum = areaPBC + areaAPC + areaABP;
        return Mathf.Abs(sum - areaABC) < 1e-5f;
    }
}