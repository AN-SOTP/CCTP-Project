using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

public static class ProgressiveVoronoiCarver
{
    public static void ProgressiveCarve(
        DelaunayTriangulation<TetraVertex, TetraCell> tetraMesh,
        Mesh sourceMesh,
        ref List<List<TetraCell>> lumps,
        float fractionOfSizeAllowed,
        int passCount,
        int seedsPerPass
    )
    {
        HashSet<TetraCell> unassigned = new HashSet<TetraCell>(tetraMesh.Cells);

        // bounding box dimension
        Vector3 bSize = sourceMesh.bounds.size;
        float objDim = Mathf.Max(bSize.x, bSize.y, bSize.z);
        float maxAllowableDim = fractionOfSizeAllowed * objDim;

        for (int pass = 0; pass < passCount; pass++)
        {
            if (unassigned.Count == 0) break;

            // sample seeds
            List<Vector3> seeds = VoronoiTetraPartitioner.SampleSeedsInsideMesh(sourceMesh, seedsPerPass);

            // partition unassigned by nearest seed
            List<List<TetraCell>> passLumps = VoronoiPartitionUnassigned(unassigned, seeds);

            // lumps that are below dimension limit => finalize them
            foreach (var lump in passLumps)
            {
                if (lump.Count == 0) continue;

                Vector3 minPos, maxPos;
                GetLumpMinMax(lump, out minPos, out maxPos);
                Vector3 size = maxPos - minPos;
                float largestDim = Mathf.Max(size.x, size.y, size.z);

                if (largestDim < maxAllowableDim)
                {
                    lumps.Add(lump);
                    foreach (var c in lump)
                    {
                        unassigned.Remove(c);
                    }
                }
                else
                {
                    // remain in leftover => next pass seeds might subdivide them further
                }
            }
        }

        // final pass: anything leftover is forcibly lumps
        if (unassigned.Count > 0)
        {
            // optional final seeds
            List<Vector3> finalSeeds = VoronoiTetraPartitioner.SampleSeedsInsideMesh(sourceMesh, seedsPerPass);
            List<List<TetraCell>> leftoverLumps = VoronoiPartitionUnassigned(unassigned, finalSeeds);
            lumps.AddRange(leftoverLumps);
        }
    }

    private static List<List<TetraCell>> VoronoiPartitionUnassigned(
        HashSet<TetraCell> unassigned,
        List<Vector3> seeds
    )
    {
        List<List<TetraCell>> lumps = new List<List<TetraCell>>(seeds.Count);
        for (int i = 0; i < seeds.Count; i++)
        {
            lumps.Add(new List<TetraCell>());
        }

        foreach (var cell in unassigned)
        {
            Vector3 c = ComputeCentroid(cell);

            int bestSeed = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < seeds.Count; i++)
            {
                float d = (c - seeds[i]).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestSeed = i;
                }
            }
            lumps[bestSeed].Add(cell);
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

    private static void GetLumpMinMax(List<TetraCell> lump, out Vector3 minPos, out Vector3 maxPos)
    {
        minPos = Vector3.positiveInfinity;
        maxPos = Vector3.negativeInfinity;
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
                if (p.x < minPos.x) minPos.x = p.x;
                if (p.y < minPos.y) minPos.y = p.y;
                if (p.z < minPos.z) minPos.z = p.z;
                if (p.x > maxPos.x) maxPos.x = p.x;
                if (p.y > maxPos.y) maxPos.y = p.y;
                if (p.z > maxPos.z) maxPos.z = p.z;
            }
        }
        if (lump.Count == 0)
        {
            minPos = Vector3.zero;
            maxPos = Vector3.zero;
        }
    }
}
