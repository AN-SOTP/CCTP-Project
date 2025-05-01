using System.Collections.Generic;
using MIConvexHull;
using UnityEngine;

public static class TetraAdjacency
{
    private const float quantize_tolerance = 1e-4f;

    public static Dictionary<TetraCell, List<TetraCell>> BuildAdjacencyGraph(DelaunayTriangulation<TetraVertex, TetraCell> mesh)
    {
        Dictionary<TetraCell, List<TetraCell>> adjacency = new Dictionary<TetraCell, List<TetraCell>>();
        Dictionary<string, List<TetraCell>> face_map = new Dictionary<string, List<TetraCell>>();

        foreach (var cell in mesh.Cells)
        {
            if (!adjacency.ContainsKey(cell))
                adjacency[cell] = new List<TetraCell>();

            TetraVertex[] verts = cell.Vertices;
            if (verts == null || verts.Length < 4)
            {
                continue;
            }

            AddFace(verts[0], verts[1], verts[2], cell, face_map);
            AddFace(verts[0], verts[1], verts[3], cell, face_map);
            AddFace(verts[0], verts[2], verts[3], cell, face_map);
            AddFace(verts[1], verts[2], verts[3], cell, face_map);
        }

        foreach (var kvp in face_map)
        {
            var cells_sharing_face = kvp.Value;
            if (cells_sharing_face.Count == 2)
            {
                TetraCell c0 = cells_sharing_face[0];
                TetraCell c1 = cells_sharing_face[1];
                adjacency[c0].Add(c1);
                adjacency[c1].Add(c0);
            }
        }
        return adjacency;
    }

    private static void AddFace(TetraVertex a, TetraVertex b, TetraVertex c, TetraCell cell, Dictionary<string, List<TetraCell>> face_map)
    {
        Vector3 A = Quantize(ToV3(a));
        Vector3 B = Quantize(ToV3(b));
        Vector3 C = Quantize(ToV3(c));
        Vector3[] tri = new Vector3[] { A, B, C };
        System.Array.Sort(tri, CompareVectors);
        string key = $"{tri[0].x:F4}_{tri[0].y:F4}_{tri[0].z:F4}_" +
                     $"{tri[1].x:F4}_{tri[1].y:F4}_{tri[1].z:F4}_" +
                     $"{tri[2].x:F4}_{tri[2].y:F4}_{tri[2].z:F4}";
        if (!face_map.ContainsKey(key))
        {
            face_map[key] = new List<TetraCell>();
        }
        face_map[key].Add(cell);
    }

    private static int CompareVectors(Vector3 v1, Vector3 v2)
    {
        if (v1.x != v2.x) return v1.x.CompareTo(v2.x);
        if (v1.y != v2.y) return v1.y.CompareTo(v2.y);
        return v1.z.CompareTo(v2.z);
    }

    private static Vector3 Quantize(Vector3 v)
    {
        return new Vector3(Mathf.Round(v.x / quantize_tolerance) * quantize_tolerance, Mathf.Round(v.y / quantize_tolerance) * quantize_tolerance, Mathf.Round(v.z / quantize_tolerance) * quantize_tolerance);
    }

    private static Vector3 ToV3(TetraVertex tv)
    {
        return new Vector3((float)tv.Position[0], (float)tv.Position[1], (float)tv.Position[2]);
    }
}
