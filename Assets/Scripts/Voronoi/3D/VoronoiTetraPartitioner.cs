using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;
using Unity.VisualScripting;

public static class VoronoiTetraPartitioner
{
    //regular direct partition, no carve-out no dimension checks
    public static List<List<TetraCell>> Partition(DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh, List<Vector3> seeds)
    {
        List<List<TetraCell>> lumps = new List<List<TetraCell>>();
        for (int i = 0; i < seeds.Count; i++)
        {
            lumps.Add(new List<TetraCell>());
        }

        foreach (TetraCell cell in tetra_mesh.Cells)
        {
            Vector3 c = ComputeCentroid(cell);
            int best_seed = -1;
            float best_dist = float.MaxValue;

            for (int i = 0; i < seeds.Count; i++)
            {
                float dist = (c - seeds[i]).sqrMagnitude;
                if (dist < best_dist)
                {
                    best_dist = dist;
                    best_seed = i;
                }
            }
            if (best_seed >= 0)
            {
                lumps[best_seed].Add(cell);
            }
        }

        lumps.RemoveAll(l => l.Count == 0);
        return lumps;
    }

    //carve-out approach with dimension check
    //we do multiple seed groups, multiple passes
    //only finalize lumps that are below dimension threshold
    //all final lumps are removed from leftover, so no double counting
    public static List<List<TetraCell>> PartitionCarveOut(DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh, List<List<Vector3>> seed_groups, Mesh source_mesh, float max_dimension_fraction)
    {
        //leftover tetra
        HashSet<TetraCell> unassigned = new HashSet<TetraCell>(tetra_mesh.Cells);

        List<List<TetraCell>> final_lumps = new List<List<TetraCell>>();

        Vector3 bounds_size = source_mesh.bounds.size;
        float object_dimension = Mathf.Max(bounds_size.x, bounds_size.y, bounds_size.z);
        float max_allowed_dimension = object_dimension * max_dimension_fraction;

        foreach (var seeds in seed_groups)
        {
            if (unassigned.Count == 0)
            {
                break;
            }

            //partition leftover for this pass
            List<List<TetraCell>> pass_lumps = PartitionSubSet(unassigned, seeds);

            //finalize lumps that are under dimension limit
            foreach (var lump in pass_lumps)
            {
                if (lump.Count == 0)
                {
                    continue;
                }

                //measure bounding box of current lump
                Vector3 min_pos, max_pos;
                GetLumpMinMax(lump, out min_pos, out max_pos);
                Vector3 size = max_pos - min_pos;
                float largest_dimension = Mathf.Max(size.x, size.y, size.z);

                //if lumps are smaller than threshold finalize them
                if (largest_dimension < max_allowed_dimension)
                {
                    final_lumps.Add(lump);
                    foreach (var c in lump)
                    {
                        unassigned.Remove(c);
                    }
                }
                else
                {
                    //lumps remain in leftover for next passes
                }
            }
        }

        //after final pass, forcibly finalize everything leftover
        if (unassigned.Count > 0)
        {
            //optional last pass with some seeds
            List<Vector3> final_seeds = SampleSeedsInsideMesh(source_mesh, 8);
            List<List<TetraCell>> leftover_lumps = PartitionSubSet(unassigned, final_seeds);

            final_lumps.AddRange(leftover_lumps);
            unassigned.Clear();
        }

        return final_lumps;
    }

    public static List<List<TetraCell>> PartitionCarveOutAdaptive(DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh, Mesh source_mesh, int seeds_per_pass, float dimension_fraction, int max_iterations)
    {
        HashSet<TetraCell> unassigned = new HashSet<TetraCell>(tetra_mesh.Cells);
        List<List<TetraCell>> final_lumps = new List<List<TetraCell>>();

        Vector3 bounds_size = source_mesh.bounds.size;
        float object_dimension = Mathf.Max(bounds_size.x, bounds_size.y, bounds_size.z);
        float max_allowed_dimension = object_dimension * dimension_fraction;

        int iteration_count = 0;
        while (unassigned.Count > 0 && iteration_count < max_iterations)
        {
            iteration_count++;

            //generate seeds for the leftover 
            //List<Vector3> seeds = SampleSeedsInsideMesh(source_mesh, seeds_per_pass);
            List<Vector3> seeds = SampleWeightedSeedsInsideMesh(source_mesh, seeds_per_pass, 0.4f);
            List<List<TetraCell>> pass_lumps = PartitionSubSet(unassigned, seeds);

            bool any_large_lump = false;
            foreach (var lump in pass_lumps)
            {
                if (lump.Count == 0) continue;

                Vector3 min_pos, max_pos;
                GetLumpMinMax(lump, out min_pos, out max_pos);
                Vector3 size = max_pos - min_pos;
                float largest_dimension = Mathf.Max(size.x, size.y, size.z);

                //if lump within dimension limit, finalize
                if (largest_dimension < max_allowed_dimension)
                {
                    final_lumps.Add(lump);
                    foreach (var c in lump)
                    {
                        unassigned.Remove(c);
                    }
                }
                else
                {
                    //this lump remains large, keep it in leftover for next iteration
                    any_large_lump = true;
                }
            }

            //if no lumps were large done subdividing
            if (!any_large_lump) break;
        }

        //forcibly finalize leftover after max_iterations
        if (unassigned.Count > 0)
        {
            //last pass lumps
            List<Vector3> final_seeds = SampleSeedsInsideMesh(source_mesh, seeds_per_pass);
            List<List<TetraCell>> leftover = PartitionSubSet(unassigned, final_seeds);
            final_lumps.AddRange(leftover);
            unassigned.Clear();
        }

        return final_lumps;
    }

    //partition only the leftover tetra
    private static List<List<TetraCell>> PartitionSubSet(HashSet<TetraCell> unassigned, List<Vector3> seeds)
    {
        List<List<TetraCell>> lumps = new List<List<TetraCell>>();
        for (int i = 0; i < seeds.Count; i++)
        {
            lumps.Add(new List<TetraCell>());
        }

        foreach (var cell in unassigned)
        {
            Vector3 c = ComputeCentroid(cell);
            int best_seed = -1;
            float best_dist = float.MaxValue;
            for (int i = 0; i < seeds.Count; i++)
            {
                float dist = (c - seeds[i]).sqrMagnitude;
                if (dist < best_dist)
                {
                    best_dist = dist;
                    best_seed = i;
                }
            }
            lumps[best_seed].Add(cell);
        }

        lumps.RemoveAll(l => l.Count == 0);
        return lumps;
    }

    private static void GetLumpMinMax(List<TetraCell> lump, out Vector3 min_pos, out Vector3 max_pos)
    {
        min_pos = Vector3.positiveInfinity;
        max_pos = Vector3.negativeInfinity;

        for (int i = 0; i < lump.Count; i++)
        {
            TetraVertex[] tv = lump[i].Vertices;
            for (int j = 0; j < tv.Length; j++)
            {
                Vector3 p = new Vector3(
                    (float)tv[j].Position[0],
                    (float)tv[j].Position[1],
                    (float)tv[j].Position[2]
                );

                if (p.x < min_pos.x)
                {
                    min_pos.x = p.x;
                }
                if (p.y < min_pos.y)
                {
                    min_pos.y = p.y;
                }
                if (p.z < min_pos.z)
                {
                    min_pos.z = p.z;
                }
                if (p.x > max_pos.x)
                {
                    max_pos.x = p.x;
                }
                if (p.y > max_pos.y)
                {
                    max_pos.y = p.y;
                }
                if (p.z > max_pos.z)
                {
                    max_pos.z = p.z;
                }
            }
        }

        if (lump.Count == 0)
        {
            min_pos = Vector3.zero;
            max_pos = Vector3.zero;
        }
    }

    //automatically pick random seeds inside the mesh:
    public static List<Vector3> SampleSeedsInsideMesh(Mesh source_mesh, int seed_count)
    {
        List<Vector3> seeds = new List<Vector3>();
        Bounds bounds = source_mesh.bounds;
        int attempts = 0;
        int max_attempts = seed_count * 10;

        while (seeds.Count < seed_count && attempts < max_attempts)
        {
            attempts++;
            float rx = Random.Range(bounds.min.x, bounds.max.x);
            float ry = Random.Range(bounds.min.y, bounds.max.y);
            float rz = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 candidate = new Vector3(rx, ry, rz);

            if (IsPointInsideMesh(candidate, source_mesh))
            {
                seeds.Add(candidate);
            }
        }

        return seeds;
    }

    public static List<Vector3> SampleWeightedSeedsInsideMesh(Mesh source_mesh, int total_seed_count, float surface_seed_ratio)  // e.g. 0.4f => 40% seeds on surface
    {
        //clamp ratio
        if (surface_seed_ratio < 0f) surface_seed_ratio = 0f;
        if (surface_seed_ratio > 1f) surface_seed_ratio = 1f;

        int surface_count = Mathf.RoundToInt(total_seed_count * surface_seed_ratio);
        int interior_count = total_seed_count - surface_count;

        //collect seeds
        List<Vector3> seeds = new List<Vector3>();

        //surface seeds
        if (surface_count > 0)
        {
            List<Vector3> surface_seeds = SampleSurfacePoints(source_mesh, surface_count);
            seeds.AddRange(surface_seeds);
        }

        //nterior seeds
        if (interior_count > 0)
        {
            List<Vector3> interior_seeds = SampleSeedsInsideMesh(source_mesh, interior_count);
            seeds.AddRange(interior_seeds);
        }

        return seeds;
    }

    //picks random triangles weighted by area, then picks a random barycentric coordinate in that triangle 
    private static List<Vector3> SampleSurfacePoints(Mesh mesh, int count)
    {
        List<Vector3> results = new List<Vector3>();
        //get mesh data
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        //build an array of cumulative triangle areas
        float[] cumulative_areas = new float[tris.Length / 3];
        float total_area = 0f;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v0 = verts[tris[i]];
            Vector3 v1 = verts[tris[i + 1]];
            Vector3 v2 = verts[tris[i + 2]];

            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            total_area += area;
            cumulative_areas[i / 3] = total_area;
        }

        //pick random triangles by area
        for (int s = 0; s < count; s++)
        {
            float r = Random.value * total_area;

            int tri_index = System.Array.BinarySearch(cumulative_areas, r);
            if (tri_index < 0)
            {
                tri_index = ~tri_index;
            }

            int i_tri = tri_index * 3;
            if (i_tri + 2 >= tris.Length)
            {
                //fallback
                i_tri = tris.Length - 3;
            }

            Vector3 v0 = verts[tris[i_tri]];
            Vector3 v1 = verts[tris[i_tri + 1]];
            Vector3 v2 = verts[tris[i_tri + 2]];

            //pick random barycentric coords inside the triangle
            Vector2 r_bc = RandomInTriangle();
            //convert barycentric to position
            Vector3 pos = v0 + (v1 - v0) * r_bc.x + (v2 - v0) * r_bc.y;
            results.Add(pos);
        }

        return results;
    }

    private static Vector2 RandomInTriangle()
    {
        //pick barycentric coords (u, v) with u+v <= 1
        float u = Random.value;
        float v = Random.value;
        if (u + v > 1f)
        {
            u = 1f - u;
            v = 1f - v;
        }
        return new Vector2(u, v);
    }


    //tetra centroid = average of 4 vertices
    private static Vector3 ComputeCentroid(TetraCell cell)
    {
        TetraVertex[] tv = cell.Vertices;
        if (tv == null || tv.Length < 4)
        {
            return Vector3.zero;
        }

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
        int hit_count = 0;
        Vector3 ray_direction = Vector3.right * 10000f;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v0 = verts[tris[i]];
            Vector3 v1 = verts[tris[i + 1]];
            Vector3 v2 = verts[tris[i + 2]];

            if (RayTriangleIntersect(point, ray_direction, v0, v1, v2))
            {
                hit_count++;
            }
        }
        return (hit_count % 2 == 1);
    }

    private static bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 ray_direction, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 p = Vector3.Cross(ray_direction, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-8f) return false;

        float invDet = 1f / det;
        Vector3 t = rayOrigin - v0;
        float u = Vector3.Dot(t, p) * invDet;
        if (u < 0f || u > 1f) return false;

        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(ray_direction, q) * invDet;
        if (v < 0f || u + v > 1f) return false;

        float dist = Vector3.Dot(e2, q) * invDet;
        if (dist < 0f) return false;

        return true;
    }
}
