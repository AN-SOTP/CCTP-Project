using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

public static class ProgressiveVoronoiCarver
{
    public static void ProgressiveCarve(DelaunayTriangulation<TetraVertex, TetraCell> tetra_mesh, Mesh source_mesh, ref List<List<TetraCell>> lumps, float fraction_of_size_allowed, int pass_count, int seeds_per_pass)
    {
        HashSet<TetraCell> unassigned = new HashSet<TetraCell>(tetra_mesh.Cells);

        //bounding box dimension
        Vector3 b_size = source_mesh.bounds.size;
        float obj_dim = Mathf.Max(b_size.x, b_size.y, b_size.z);
        float max_allowable_dim = fraction_of_size_allowed * obj_dim;

        for (int pass = 0; pass < pass_count; pass++)
        {
            if (unassigned.Count == 0)
            {
                break;
            }

            List<Vector3> seeds = VoronoiTetraPartitioner.SampleSeedsInsideMesh(source_mesh, seeds_per_pass);

            List<List<TetraCell>> pass_lumps = VoronoiPartitionUnassigned(unassigned, seeds);

            // finalize lumps below dimension limit
            foreach (var lump in pass_lumps)
            {
                if (lump.Count == 0) continue;

                Vector3 min_pos, max_pos;
                GetLumpMinMax(lump, out min_pos, out max_pos);
                Vector3 size = max_pos - min_pos;
                float largest_dim = Mathf.Max(size.x, size.y, size.z);

                if (largest_dim < max_allowable_dim)
                {
                    lumps.Add(lump);
                    foreach (var c in lump)
                    {
                        unassigned.Remove(c);
                    }
                }
                else
                {
                    
                }
            }
        }

        // final pass, anything leftover is forcibly lumped
        if (unassigned.Count > 0)
        {
            //optional final seeds
            List<Vector3> final_seeds = VoronoiTetraPartitioner.SampleSeedsInsideMesh(source_mesh, seeds_per_pass);
            List<List<TetraCell>> leftover_lumps = VoronoiPartitionUnassigned(unassigned, final_seeds);
            lumps.AddRange(leftover_lumps);
        }
    }

    private static List<List<TetraCell>> VoronoiPartitionUnassigned(HashSet<TetraCell> unassigned, List<Vector3> seeds)
    {
        List<List<TetraCell>> lumps = new List<List<TetraCell>>(seeds.Count);
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
                float d = (c - seeds[i]).sqrMagnitude;
                if (d < best_dist)
                {
                    best_dist = d;
                    best_seed = i;
                }
            }
            lumps[best_seed].Add(cell);
        }

        lumps.RemoveAll(x => x.Count == 0);
        return lumps;
    }

    private static Vector3 ComputeCentroid(TetraCell cell)
    {
        Vector3 sum = Vector3.zero;
        TetraVertex[] verts = cell.Vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            sum.x += (float)verts[i].Position[0];
            sum.y += (float)verts[i].Position[1];
            sum.z += (float)verts[i].Position[2];
        }
        return sum / verts.Length;
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
                Vector3 p = new Vector3((float)tv[j].Position[0], (float)tv[j].Position[1], (float)tv[j].Position[2]);
                if (p.x < min_pos.x) min_pos.x = p.x;
                if (p.y < min_pos.y) min_pos.y = p.y;
                if (p.z < min_pos.z) min_pos.z = p.z;
                if (p.x > max_pos.x) max_pos.x = p.x;
                if (p.y > max_pos.y) max_pos.y = p.y;
                if (p.z > max_pos.z) max_pos.z = p.z;
            }
        }
        if (lump.Count == 0)
        {
            min_pos = Vector3.zero;
            max_pos = Vector3.zero;
        }
    }
}
