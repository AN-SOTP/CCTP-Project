using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

public static class TetraLumpPartitioner
{
    public static List<List<TetraCell>> PartitionTetraMesh(DelaunayTriangulation<TetraVertex, TetraCell> mesh, List<Vector3> seeds, int refine_iterations = 2)
    {
        var adjacency = BuildAdjacency(mesh);
        var all_cells = mesh.Cells.ToList();
        int cell_count = all_cells.Count;

        Vector3[] centroids = new Vector3[cell_count];
        Dictionary<TetraCell, int> cell_to_index = new Dictionary<TetraCell, int>(cell_count);
        for (int i = 0; i < cell_count; i++)
        {
            cell_to_index[all_cells[i]] = i;
            centroids[i] = ComputeCentroid(all_cells[i]);
        }

        int[] assigned = new int[cell_count];
        for (int i = 0; i < cell_count; i++) assigned[i] = -1;

        Queue<int>[] seed_queues = new Queue<int>[seeds.Count];
        for (int s = 0; s < seeds.Count; s++)
            seed_queues[s] = new Queue<int>();

        for (int i = 0; i < cell_count; i++)
        {
            int best_seed = NearestSeed(centroids[i], seeds);
            seed_queues[best_seed].Enqueue(i);
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int s = 0; s < seeds.Count; s++)
            {
                var q = seed_queues[s];
                int count = q.Count;
                while (count > 0)
                {
                    int c_idx = q.Dequeue();
                    count--;
                    if (assigned[c_idx] == -1)
                    {
                        assigned[c_idx] = s;
                        changed = true;
                        if (adjacency.TryGetValue(all_cells[c_idx], out var neighbors))
                        {
                            foreach (var ncell in neighbors)
                            {
                                int n_idx = cell_to_index[ncell];
                                if (assigned[n_idx] == -1)
                                {
                                    q.Enqueue(n_idx);
                                }
                            }
                        }
                    }
                }
            }
        }

        for (int it = 0; it < refine_iterations; it++)
        {
            bool any_update = false;
            for (int i = 0; i < cell_count; i++)
            {
                int old_seed = assigned[i];
                int best_seed = NearestSeed(centroids[i], seeds);
                if (best_seed != old_seed)
                {
                    assigned[i] = best_seed;
                    any_update = true;
                }
            }
            if (!any_update) break;
        }

        List<HashSet<int>> lumps_indices = new List<HashSet<int>>();
        for (int s = 0; s < seeds.Count; s++)
            lumps_indices.Add(new HashSet<int>());

        for (int i = 0; i < cell_count; i++)
            lumps_indices[assigned[i]].Add(i);

        List<List<TetraCell>> lumps = new List<List<TetraCell>>();
        for (int s = 0; s < seeds.Count; s++)
        {
            if (lumps_indices[s].Count > 0)
            {
                List<TetraCell> group = new List<TetraCell>();
                foreach (int idx in lumps_indices[s])
                    group.Add(all_cells[idx]);
                lumps.Add(group);
            }
        }
        return lumps;
    }

    private static Vector3 ComputeCentroid(TetraCell cell)
    {
        TetraVertex[] v = cell.Vertices;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            sum += new Vector3((float)v[i].Position[0], (float)v[i].Position[1], (float)v[i].Position[2]);
        }
        return sum * 0.25f;
    }

    private static int NearestSeed(Vector3 pos, List<Vector3> seeds)
    {
        float best = float.MaxValue;
        int index = -1;
        for (int i = 0; i < seeds.Count; i++)
        {
            float d = (pos - seeds[i]).sqrMagnitude;
            if (d < best)
            {
                best = d;
                index = i;
            }
        }
        return index;
    }

    private static Dictionary<TetraCell, List<TetraCell>> BuildAdjacency(DelaunayTriangulation<TetraVertex, TetraCell> mesh)
    {
        Dictionary<TetraCell, List<TetraCell>> adjacency = new Dictionary<TetraCell, List<TetraCell>>();
        Dictionary<string, List<TetraCell>> face_map = new Dictionary<string, List<TetraCell>>();
        foreach (var cell in mesh.Cells)
        {
            if (!adjacency.ContainsKey(cell))
                adjacency[cell] = new List<TetraCell>();
            var v = cell.Vertices;
            if (v == null || v.Length < 4) continue;
            AddFace(v[0], v[1], v[2], cell, face_map);
            AddFace(v[0], v[1], v[3], cell, face_map);
            AddFace(v[0], v[2], v[3], cell, face_map);
            AddFace(v[1], v[2], v[3], cell, face_map);
        }
        foreach (var kvp in face_map)
        {
            var list = kvp.Value;
            if (list.Count == 2)
            {
                adjacency[list[0]].Add(list[1]);
                adjacency[list[1]].Add(list[0]);
            }
        }
        return adjacency;
    }

    private static void AddFace(TetraVertex a, TetraVertex b, TetraVertex c, TetraCell cell, Dictionary<string, List<TetraCell>> map)
    {
        Vector3 A = new Vector3((float)a.Position[0], (float)a.Position[1], (float)a.Position[2]);
        Vector3 B = new Vector3((float)b.Position[0], (float)b.Position[1], (float)b.Position[2]);
        Vector3 C = new Vector3((float)c.Position[0], (float)c.Position[1], (float)c.Position[2]);
        Vector3[] arr = new[] { A, B, C };
        System.Array.Sort(arr, (p1, p2) => Compare(p1, p2));
        string key = $"{arr[0].x:F4}_{arr[0].y:F4}_{arr[0].z:F4}_{arr[1].x:F4}_{arr[1].y:F4}_{arr[1].z:F4}_{arr[2].x:F4}_{arr[2].y:F4}_{arr[2].z:F4}";
        if (!map.ContainsKey(key))
            map[key] = new List<TetraCell>();
        map[key].Add(cell);
    }

    private static int Compare(Vector3 v1, Vector3 v2)
    {
        if (v1.x != v2.x) return v1.x.CompareTo(v2.x);
        if (v1.y != v2.y) return v1.y.CompareTo(v2.y);
        return v1.z.CompareTo(v2.z);
    }
}
