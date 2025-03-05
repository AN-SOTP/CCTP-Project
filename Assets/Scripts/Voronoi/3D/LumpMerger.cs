using System.Collections.Generic;
using UnityEngine;

public static class LumpMerger
{
    public static List<List<TetraCell>> MergeOverlappingLumpsSinglePass(List<List<TetraCell>> lumps, float overlap_ratio_threshold)
    {
        int n = lumps.Count;
        if (n <= 1)
        {
            return lumps;
        }

        Bounds[] lump_bounds = new Bounds[n];
        for (int i = 0; i < n; i++)
        {
            lump_bounds[i] = ComputeBoundsOfLump(lumps[i]);
        }

        bool[,] adjacency = new bool[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float ratio = BoundsOverlapRatio(lump_bounds[i], lump_bounds[j]);
                if (ratio > overlap_ratio_threshold)
                {
                    adjacency[i, j] = adjacency[j, i] = true;
                }
            }
        }

        bool[] visited = new bool[n];
        List<List<int>> components = new List<List<int>>();

        for (int start = 0; start < n; start++)
        {
            if (!visited[start])
            {
                List<int> comp = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(start);
                visited[start] = true;

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    comp.Add(cur);

                    for (int other = 0; other < n; other++)
                    {
                        if (!visited[other] && adjacency[cur, other])
                        {
                            visited[other] = true;
                            queue.Enqueue(other);
                        }
                    }
                }

                components.Add(comp);
            }
        }

        List<List<TetraCell>> final_lumps = new List<List<TetraCell>>(components.Count);

        foreach (var comp in components)
        {
            List<TetraCell> merged = new List<TetraCell>();
            foreach (int idx in comp)
            {
                merged.AddRange(lumps[idx]);
            }
            final_lumps.Add(merged);
        }

        return final_lumps;
    }

    private static Bounds ComputeBoundsOfLump(List<TetraCell> lump)
    {
        Vector3 min_pos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max_pos = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < lump.Count; i++)
        {
            TetraVertex[] vertices = lump[i].Vertices;
            for (int k = 0; k < vertices.Length; k++)
            {
                Vector3 position = new Vector3(
                    (float)vertices[k].Position[0],
                    (float)vertices[k].Position[1],
                    (float)vertices[k].Position[2]
                );
                if (position.x < min_pos.x) min_pos.x = position.x;
                if (position.y < min_pos.y) min_pos.y = position.y;
                if (position.z < min_pos.z) min_pos.z = position.z;
                if (position.x > max_pos.x) max_pos.x = position.x;
                if (position.y > max_pos.y) max_pos.y = position.y;
                if (position.z > max_pos.z) max_pos.z = position.z;
            }
        }
        Bounds bounds = new Bounds((min_pos + max_pos) * 0.5f, Vector3.zero);
        bounds.SetMinMax(min_pos, max_pos);
        return bounds;
    }

    private static float BoundsOverlapRatio(Bounds A, Bounds B)
    {
        float overlap_x = Mathf.Min(A.max.x, B.max.x) - Mathf.Max(A.min.x, B.min.x);
        float overlap_y = Mathf.Min(A.max.y, B.max.y) - Mathf.Max(A.min.y, B.min.y);
        float overlap_z = Mathf.Min(A.max.z, B.max.z) - Mathf.Max(A.min.z, B.min.z);

        if (overlap_x <= 0f || overlap_y <= 0f || overlap_z <= 0f)
        {
            return 0f;
        }
        float overlap_vol = overlap_x * overlap_y * overlap_z;

        float vol_A = A.size.x * A.size.y * A.size.z;
        float vol_B = B.size.x * B.size.y * B.size.z;
        float union_vol = vol_A + vol_B - overlap_vol;
        if (union_vol <= 0f)
        {
            return 1f; //if bounding boxes are exactly same
        }
        float ratio = overlap_vol / union_vol;
        return ratio;
    }
}
