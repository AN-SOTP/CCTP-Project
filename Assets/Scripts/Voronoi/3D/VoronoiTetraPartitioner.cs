using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

public static class VoronoiTetraPartitioner
{
    //partition the tetra mesh into lumps by nearest seed
    //each seed is a Vector3 in local space
    //we assume "adjacency" is not strictly needed here, because we do a direct assignment by distance
    public static List<List<TetraCell>> Partition(DelaunayTriangulation<TetraVertex, TetraCell> tetraMesh, List<Vector3> seeds)
    {
        List<List<TetraCell>> lumps = new List<List<TetraCell>>();
        for (int i = 0; i < seeds.Count; i++)
        {
            lumps.Add(new List<TetraCell>());
        }

        //for each tetra cell, find its centroid and pick closest seed
        foreach (TetraCell cell in tetraMesh.Cells)
        {
            Vector3 c = ComputeCentroid(cell);
            int bestSeed = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < seeds.Count; i++)
            {
                float dist = (c - seeds[i]).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSeed = i;
                }
            }

            if (bestSeed >= 0)
            {
                lumps[bestSeed].Add(cell);
            }
        }

        //remove any empty lumps if seeds had no assigned tetra
        lumps.RemoveAll(l => l.Count == 0);

        return lumps;
    }

    //if you want to automatically pick random seeds inside the mesh:
    public static List<Vector3> SampleSeedsInsideMesh(Mesh sourceMesh, int seedCount)
    {
        List<Vector3> seeds = new List<Vector3>();
        Bounds b = sourceMesh.bounds;
        int attempts = 0;
        int maxAttempts = seedCount * 10;

        while (seeds.Count < seedCount && attempts < maxAttempts)
        {
            attempts++;
            float rx = Random.Range(b.min.x, b.max.x);
            float ry = Random.Range(b.min.y, b.max.y);
            float rz = Random.Range(b.min.z, b.max.z);
            Vector3 candidate = new Vector3(rx, ry, rz);

            if (IsPointInsideMesh(candidate, sourceMesh))
            {
                seeds.Add(candidate);
            }
        }

        return seeds;
    }

    //tetra centroid = average of 4 vertices
    private static Vector3 ComputeCentroid(TetraCell cell)
    {
        TetraVertex[] tv = cell.Vertices;
        if (tv == null || tv.Length < 4)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            sum.x += (float)tv[i].Position[0];
            sum.y += (float)tv[i].Position[1];
            sum.z += (float)tv[i].Position[2];
        }
        return sum * 0.25f;
    }

    private static bool IsPointInsideMesh(Vector3 point, Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        int hitCount = 0;
        Vector3 rayDir = Vector3.right * 10000f;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v0 = verts[tris[i]];
            Vector3 v1 = verts[tris[i + 1]];
            Vector3 v2 = verts[tris[i + 2]];

            if (RayTriangleIntersect(point, rayDir, v0, v1, v2))
            {
                hitCount++;
            }
        }
        return (hitCount % 2 == 1);
    }

    private static bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 p = Vector3.Cross(rayDir, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-8f) return false;

        float invDet = 1f / det;
        Vector3 t = rayOrigin - v0;
        float u = Vector3.Dot(t, p) * invDet;
        if (u < 0f || u > 1f) return false;

        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(rayDir, q) * invDet;
        if (v < 0f || u + v > 1f) return false;

        float dist = Vector3.Dot(e2, q) * invDet;
        if (dist < 0f) return false;

        return true;
    }
}
