using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

//tetra vertex and tetra cell are needed data structures for MIConvexHull's DelaunayTriangulation in 3D.
public class TetraVertex : IVertex
{
    public double[] Position { get; set; }

    public TetraVertex(double x, double y, double z)
    {
        Position = new double[] { x, y, z };
    }
}

public class TetraCell : TriangulationCell<TetraVertex, TetraCell>
{
    //inherits from TriangulationCell.
}

public class VolumetricTetraBuilder
{
    public static List<Vector3> debug_sample_points = new List<Vector3>();

    //method demonstrates how to: sample interior points of a mesh, build a Delaunay triangulation of those points, return the tetrahedral data structure
    public DelaunayTriangulation<TetraVertex, TetraCell> BuildTetraMesh(Mesh source_mesh, int sample_count)
    {
        List<Vector3> inside_points = SampleInsidePoints(source_mesh, sample_count);
        if (inside_points.Count < 4)
        {
            Debug.LogWarning("Not enough inside points to form a 3D triangulation.");
            return null;
        }

        //convert to TetraVertex
        List<TetraVertex> tetra_vertices = new List<TetraVertex>(inside_points.Count);
        for (int i = 0; i < inside_points.Count; i++)
        {
            Vector3 p = inside_points[i];
            tetra_vertices.Add(new TetraVertex(p.x, p.y, p.z));
        }

        float scale = Mathf.Max(source_mesh.bounds.size.x, source_mesh.bounds.size.y, source_mesh.bounds.size.z);
        float tolerance = 1e-7f * scale;

        //create 3D Delaunay triangulation
        DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh = null;
        try
        {
            tetra_mesh = DelaunayTriangulation<TetraVertex, TetraCell>.Create(tetra_vertices, tolerance);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error building Delaunay triangulation: " + ex.Message);
        }

        //tetra_mesh now contains a volumetric set of tetrahedra.
        //each tetra_cell has Vertices that are TetraVertex objects.
        return tetra_mesh;
    }

    //a simplified method to randomly sample points inside the mesh volume.
    //checks if a point is inside using a simple raycast approach or anything else you prefer.
    //can be improved for uniformity or other distributions.
    private List<Vector3> SampleInsidePoints(Mesh mesh, int count)
    {
        List<Vector3> result = new List<Vector3>();
        Bounds b = mesh.bounds;
        int max_attempts = count * 10; // some extra attempts in case we fail to find enough

        int attempts = 0;
        while (result.Count < count && attempts < max_attempts)
        {
            attempts++;
            // Pick a random point in bounding box (local coords)
            float rx = Random.Range(b.min.x, b.max.x);
            float ry = Random.Range(b.min.y, b.max.y);
            float rz = Random.Range(b.min.z, b.max.z);
            Vector3 candidate = new Vector3(rx, ry, rz);

            // Check if inside. This uses a simple "ray crossing" method.
            if (IsPointInsideMesh(candidate, mesh))
            {
                result.Add(candidate);
                debug_sample_points.Add(candidate);
            }
        }

        return result;
    }

    //build off initial sampling with Lloyd Relaxation
    private List<Vector3> SampleInsidePointsUniform(Mesh mesh, int count)
    {
        List<Vector3> points = SampleInsidePoints(mesh, count);
        int iterations = 5;
        for (int i = 0; i < iterations; i++)
        {
            points = LloydRelaxation(points, mesh.bounds);
        }
        return points;
    }

    //Lloyd relaxation, using fixed radius for neighbours
    private List<Vector3> LloydRelaxation(List<Vector3> points, Bounds bounds)
    {
        List<Vector3> new_points = new List<Vector3>();
        float radius_sq = 1f; // need to tune based on mesh scale
        foreach (var p in points)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var q in points)
            {
                if ((p - q).sqrMagnitude < radius_sq)
                {
                    sum += q;
                    count++;
                }
            }
            if (count > 0)
            {
                Vector3 centroid = sum / count;
                centroid = Vector3.Max(bounds.min, Vector3.Min(bounds.max, centroid));
                new_points.Add(centroid);
            }
            else
            {
                new_points.Add(p);
            }
        }
        return new_points;
    }

    //"point in mesh" test using a ray intersection count in local space
    private bool IsPointInsideMesh(Vector3 point, Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        int hit_count = 0;

        //cast along +X direction
        Vector3 ray_dir = Vector3.right * 10000f;

        //optional small epsilon offset along ray direction
        Vector3 ray_origin = point + ray_dir * 1e-4f;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v0 = verts[tris[i]];
            Vector3 v1 = verts[tris[i + 1]];
            Vector3 v2 = verts[tris[i + 2]];

            if (RayTriangleIntersect(point, ray_dir, v0, v1, v2))
            {
                hit_count++;
            }
        }

        return (hit_count % 2 == 1);
    }

    //Möller–Trumbore intersection
    private bool RayTriangleIntersect(Vector3 ray_origin, Vector3 ray_dir, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 p = Vector3.Cross(ray_dir, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-8f)
        {
            return false;
        }

        float inv_det = 1f / det;
        Vector3 t = ray_origin - v0;
        float u = Vector3.Dot(t, p) * inv_det;
        if (u < 0f || u > 1f)
        {
            return false;
        }

        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(ray_dir, q) * inv_det;
        if (v < 0f || u + v > 1f) return false;

        float dist = Vector3.Dot(e2, q) * inv_det;
        if (dist < 0f)
        {
            return false;
        }

        return true;
    }

    public static Mesh BuildTetraDebugMesh(DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh)
    {
        if (tetra_mesh == null || tetra_mesh.Cells.Count() == 0)
        {
            Debug.LogWarning("No tetrahedra to build debug mesh from.");
            return new Mesh();
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int vert_index = 0;

        //for each tetrahedron cell: 4 vertices => 4 faces
        foreach (TetraCell cell in tetra_mesh.Cells)
        {
            TetraVertex[] v = cell.Vertices;
            if (v == null || v.Length < 4) continue;

            Vector3 p0 = ToV3(v[0]);
            Vector3 p1 = ToV3(v[1]);
            Vector3 p2 = ToV3(v[2]);
            Vector3 p3 = ToV3(v[3]);

            //add the 4 faces of the tetrahedron:
            //Face #1: (p0, p1, p2)
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            triangles.Add(vert_index + 0);
            triangles.Add(vert_index + 1);
            triangles.Add(vert_index + 2);
            vert_index += 3;

            //Face #2: (p0, p2, p3)
            vertices.Add(p0);
            vertices.Add(p2);
            vertices.Add(p3);
            triangles.Add(vert_index + 0);
            triangles.Add(vert_index + 1);
            triangles.Add(vert_index + 2);
            vert_index += 3;

            //Face #3: (p0, p1, p3)
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p3);
            triangles.Add(vert_index + 0);
            triangles.Add(vert_index + 1);
            triangles.Add(vert_index + 2);
            vert_index += 3;

            //Face #4: (p1, p2, p3)
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            triangles.Add(vert_index + 0);
            triangles.Add(vert_index + 1);
            triangles.Add(vert_index + 2);
            vert_index += 3;
        }

        //build final mesh
        Mesh debug_mesh = new Mesh();
        debug_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        //in case it exceeds 65k~ vertices

        debug_mesh.vertices = vertices.ToArray();
        debug_mesh.triangles = triangles.ToArray();
        debug_mesh.RecalculateNormals();
        debug_mesh.RecalculateBounds();

        return debug_mesh;
    }

    public static Mesh BuildMeshForLump(List<TetraCell> lump)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        int vert_index = 0;

        foreach (TetraCell cell in lump)
        {
            TetraVertex[] v = cell.Vertices;
            if (v == null || v.Length < 4) continue;

            //4 faces per tetrahedron:
            //Face1 (0,1,2), Face2 (0,2,3), Face3 (0,1,3), Face4 (1,2,3)
            Vector3 p0 = ToV3(v[0]);
            Vector3 p1 = ToV3(v[1]);
            Vector3 p2 = ToV3(v[2]);
            Vector3 p3 = ToV3(v[3]);

            AddTriangle(vertices, triangles, ref vert_index, p0, p1, p2);
            AddTriangle(vertices, triangles, ref vert_index, p0, p2, p3);
            AddTriangle(vertices, triangles, ref vert_index, p0, p1, p3);
            AddTriangle(vertices, triangles, ref vert_index, p1, p2, p3);
        }

        Mesh lump_mesh = new Mesh();
        lump_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        lump_mesh.vertices = vertices.ToArray();
        lump_mesh.triangles = triangles.ToArray();
        lump_mesh.RecalculateNormals();
        lump_mesh.RecalculateBounds();

        return lump_mesh;
    }

    private static void AddTriangle(List<Vector3> verts, List<int> tris, ref int base_index, Vector3 a, Vector3 b, Vector3 c)
    {
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        tris.Add(base_index);
        tris.Add(base_index + 1);
        tris.Add(base_index + 2);
        base_index += 3;
    }


    private static Vector3 ToV3(TetraVertex tv)
    {
        return new Vector3((float)tv.Position[0], (float)tv.Position[1], (float)tv.Position[2]);
    }
}