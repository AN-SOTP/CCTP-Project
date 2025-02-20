using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

public static class AdaptiveVoronoiPartitioner
{
    // Iteratively refine seeds so lumps do not exceed a bounding box dimension or tetra count.
    // 'maxIterations' prevents infinite loops.
    // 'initialSeeds' => how many seeds we start with (or pass in if you want fixed positions).
    public static List<List<TetraCell>> AdaptivePartition(DelaunayTriangulation<TetraVertex, TetraCell> tetraMesh, Mesh sourceMesh, int initialSeedCount, float maxDim,
        int maxTetraCount, int maxIterations, int maxTotalSeeds)
    {
        // 1. Generate initial seeds in the object. 
        //    Could also accept user-supplied seeds instead of random.
        List<Vector3> seeds = VoronoiTetraPartitioner.SampleSeedsInsideMesh(sourceMesh, initialSeedCount);

        List<List<TetraCell>> lumps = null;
        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 2. Partition the tetra mesh by nearest seed
            lumps = VoronoiTetraPartitioner.Partition(tetraMesh, seeds);

            // check lumps => see if any lumps exceed 'maxDim' or 'maxTetraCount'
            bool addedSeeds = false;
            for (int i = 0; i < lumps.Count; i++)
            {
                List<TetraCell> lump = lumps[i];
                if (lump.Count == 0)
                    continue;

                // measure bounding box or tetra count
                GetLumpMinMax(lump, out Vector3 minPos, out Vector3 maxPos);
                Vector3 size = maxPos - minPos;
                float largestDim = Mathf.Max(size.x, size.y, size.z);

                if (largestDim > maxDim || lump.Count > maxTetraCount)
                {
                    // We add more seeds inside this lump, e.g. 1 or 2 seeds
                    // Typically place them near lump's centroid or random points inside bounding box
                    Vector3 centroid = 0.5f * (minPos + maxPos);

                    // optional random offset
                    Vector3 randomWithinBox = new Vector3(
                        Random.Range(minPos.x, maxPos.x),
                        Random.Range(minPos.y, maxPos.y),
                        Random.Range(minPos.z, maxPos.z)
                    );

                    // Choose one or both
                    seeds.Add(centroid);
                    seeds.Add(randomWithinBox);
                    addedSeeds = true;
                }

                if (seeds.Count >= maxTotalSeeds)
                {
                    // We reached a seed limit => can't add more
                    break;
                }
            }

            if (!addedSeeds || seeds.Count >= maxTotalSeeds)
            {
                // no lumps triggered a new seed => stable
                break;
            }
        }

        // lumps is the final partition
        return lumps;
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
                Vector3 pos = new Vector3(
                    (float)tv[j].Position[0],
                    (float)tv[j].Position[1],
                    (float)tv[j].Position[2]
                );
                if (pos.x < minPos.x) minPos.x = pos.x;
                if (pos.y < minPos.y) minPos.y = pos.y;
                if (pos.z < minPos.z) minPos.z = pos.z;
                if (pos.x > maxPos.x) maxPos.x = pos.x;
                if (pos.y > maxPos.y) maxPos.y = pos.y;
                if (pos.z > maxPos.z) maxPos.z = pos.z;
            }
        }
        if (lump.Count == 0)
        {
            minPos = Vector3.zero;
            maxPos = Vector3.zero;
        }
    }
}
