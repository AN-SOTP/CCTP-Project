using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;

public static class ConvexHullChunkBuilder
{
    public static Mesh BuildConvexHullForLump(List<TetraCell> lump)
    {
        //collect all vertices from all tetrahedra in the lump
        List<DefaultVertex> all_points = new List<DefaultVertex>();
        HashSet<Vector3> unique_positions = new HashSet<Vector3>();

        for (int i = 0; i < lump.Count; i++)
        {
            TetraVertex[] tv = lump[i].Vertices;
            if (tv == null || tv.Length < 4)
            {
                continue;
            }

            for (int j = 0; j < tv.Length; j++)
            {
                Vector3 pos = ToV3(tv[j]);
                if (!unique_positions.Contains(pos))
                {
                    unique_positions.Add(pos);
                    all_points.Add(new DefaultVertex
                    {
                        Position = new double[] { pos.x, pos.y, pos.z }
                    });
                }
            }
        }

        //build the convex hull using MIConvexHull
        //var hull = ConvexHull.Create<DefaultVertex, DefaultConvexFace<DefaultVertex>>(all_points);
        var hull_result = ConvexHull.Create<DefaultVertex, DefaultConvexFace<DefaultVertex>>(all_points);

        if (hull_result.Outcome != ConvexHullCreationResultOutcome.Success)
        {
            Debug.LogError($"Convex hull creation failed: {hull_result.ErrorMessage}");
            return null;
        }

        //convert the hull faces to a mesh
        //return CreateMeshFromHull(hull_result.Result);
        Mesh chunk_mesh = CreateMeshFromHull(hull_result.Result);

        //to try prevent z-fighting (DONE AFTER CLIPPING IN VORONOITEST3D NOW)
        //InwardOffsetMesh(chunk_mesh, 0.55f);

        return chunk_mesh;
    }

    private static Mesh CreateMeshFromHull(ConvexHull<DefaultVertex, DefaultConvexFace<DefaultVertex>> hull)
    {
        List<Vector3> vertices = new List<Vector3>();
        Dictionary<Vector3, int> vert_to_index = new Dictionary<Vector3, int>();
        List<int> triangles = new List<int>();

        //collect hull points
        var hull_points = hull.Points;
        int index_counter = 0;

        foreach (var p in hull_points)
        {
            Vector3 v = new Vector3((float)p.Position[0], (float)p.Position[1], (float)p.Position[2]);
            if (!vert_to_index.ContainsKey(v))
            {
                vert_to_index[v] = index_counter;
                vertices.Add(v);
                index_counter++;
            }
        }

        //build triangles from each face
        foreach (var face in hull.Faces)
        {
            //3 vertices per face
            Vector3 va = new Vector3((float)face.Vertices[0].Position[0], (float)face.Vertices[0].Position[1], (float)face.Vertices[0].Position[2]);
            Vector3 vb = new Vector3((float)face.Vertices[1].Position[0], (float)face.Vertices[1].Position[1], (float)face.Vertices[1].Position[2]);
            Vector3 vc = new Vector3((float)face.Vertices[2].Position[0], (float)face.Vertices[2].Position[1], (float)face.Vertices[2].Position[2]);

            int ia = vert_to_index[va];
            int ib = vert_to_index[vb];
            int ic = vert_to_index[vc];

            triangles.Add(ia);
            triangles.Add(ib);
            triangles.Add(ic);
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void InwardOffsetMesh(Mesh mesh, float scale_factor)
    {
        Vector3[] verts = mesh.vertices;
        if (verts.Length == 0)
        {
            return;
        }

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < verts.Length; i++)
        {
            centroid += verts[i];
        }
        centroid /= verts.Length;

        // offset each vertex
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 offset = verts[i] - centroid;
            verts[i] = centroid + offset * scale_factor;
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private static Vector3 ToV3(TetraVertex tv)
    {
        return new Vector3((float)tv.Position[0], (float)tv.Position[1], (float)tv.Position[2]);
    }
}
