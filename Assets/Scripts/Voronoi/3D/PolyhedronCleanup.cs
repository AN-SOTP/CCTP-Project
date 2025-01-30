using System.Collections.Generic;
using UnityEngine;

public static class PolyhedronCleanup
{
    public static void FinalizePolyhedron(Polyhedron poly, float epsilon = 1e-3f) //1e-5f, 1e-6f, 1e-4f
    {
        RemoveDegeneratePolygons(poly, epsilon);
        RemoveDuplicateFaces(poly, epsilon);
        UnifyFaceOrientation(poly);
    }

    public static void RemoveDegeneratePolygons(Polyhedron poly, float epsilon)
    {
        var cleaned = new List<Polygon3D>();
        foreach (var face in poly.faces)
        {
            if (face.vertices.Count < 3) continue;

            // Project vertices to make the face planar
            face.vertices = ProjectVerticesToPlane(face.vertices, ComputeFaceNormal(face));

            float area = ComputePolygonArea3D(face);
            if (area < epsilon) continue;
            cleaned.Add(face);
        }
        poly.faces = cleaned;
    }

    private static List<Vector3> ProjectVerticesToPlane(List<Vector3> vertices, Vector3 normal)
    {
        Plane plane = new Plane(normal, vertices[0]);
        List<Vector3> projected = new List<Vector3>();
        foreach (var v in vertices)
        {
            plane.Raycast(new Ray(v, plane.normal), out float distance);
            Vector3 projectedPoint = v - plane.normal * distance;
            projected.Add(projectedPoint);
        }
        return projected;
    }

    public static void RemoveDuplicateFaces(Polyhedron poly, float epsilon)
    {
        var seen = new HashSet<string>();
        var cleaned = new List<Polygon3D>();
        foreach (var face in poly.faces)
        {
            if (face.vertices.Count < 3) continue;
            string sig = GetPolygonSignature(face, epsilon);
            if (!seen.Contains(sig))
            {
                seen.Add(sig);
                cleaned.Add(face);
            }
        }
        poly.faces = cleaned;
    }

    public static void UnifyFaceOrientation(Polyhedron poly)
    {
        Vector3 polyCenter = ComputePolyhedronCentroid(poly);
        foreach (var face in poly.faces)
        {
            Vector3 n = ComputeFaceNormal(face);
            if (n == Vector3.zero) continue;

            // face centroid
            Vector3 faceCenter = Vector3.zero;
            foreach (var v in face.vertices) faceCenter += v;
            faceCenter /= face.vertices.Count;

            Vector3 outwardDir = faceCenter - polyCenter;
            if (Vector3.Dot(n, outwardDir) < 0f)
            {
                face.vertices.Reverse();
            }
        }
    }

    // -------------- HELPER METHODS -------------- //

    private static float ComputePolygonArea3D(Polygon3D face)
    {
        if (face.vertices.Count < 3) return 0f;
        Vector3 anchor = face.vertices[0];
        float areaSum = 0f;
        for (int i = 1; i < face.vertices.Count - 1; i++)
        {
            Vector3 v1 = face.vertices[i] - anchor;
            Vector3 v2 = face.vertices[i + 1] - anchor;
            Vector3 cross = Vector3.Cross(v1, v2);
            areaSum += cross.magnitude * 0.5f;
        }
        return areaSum;
    }

    private static string GetPolygonSignature(Polygon3D face, float epsilon)
    {
        var clone = new List<Vector3>(face.vertices);
        clone.Sort((a, b) => CompareVectors(a, b, epsilon));
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < clone.Count; i++)
        {
            sb.AppendFormat("{0:F6}|{1:F6}|{2:F6}-", clone[i].x, clone[i].y, clone[i].z);
        }
        return sb.ToString();
    }
    private static int CompareVectors(Vector3 a, Vector3 b, float eps)
    {
        if (Mathf.Abs(a.x - b.x) > eps) return (a.x < b.x) ? -1 : 1;
        if (Mathf.Abs(a.y - b.y) > eps) return (a.y < b.y) ? -1 : 1;
        if (Mathf.Abs(a.z - b.z) > eps) return (a.z < b.z) ? -1 : 1;
        return 0;
    }

    private static Vector3 ComputePolyhedronCentroid(Polyhedron poly)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var face in poly.faces)
        {
            foreach (var v in face.vertices)
            {
                sum += v;
                count++;
            }
        }
        if (count == 0) return Vector3.zero;
        return sum / (float)count;
    }

    public static Vector3 ComputeFaceNormal(Polygon3D face)
    {
        if (face.vertices.Count < 3) return Vector3.zero;
        Vector3 anchor = face.vertices[0];
        Vector3 normal = Vector3.zero;
        for (int i = 1; i < face.vertices.Count - 1; i++)
        {
            Vector3 v1 = face.vertices[i] - anchor;
            Vector3 v2 = face.vertices[i + 1] - anchor;
            normal += Vector3.Cross(v1, v2);
        }
        return normal.normalized;
    }
}
